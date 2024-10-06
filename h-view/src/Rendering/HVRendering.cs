using System.Diagnostics;
using Hai.HView.Core;
using Hai.HView.Data;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Vector2 = System.Numerics.Vector2;

namespace Hai.HView.Rendering;

public class HVRendering
{
    public event SubmitUi OnSubmitUi;
    public delegate void SubmitUi(CustomImGuiController controller, Sdl2Window window);
    
    private const int RefreshFramesPerSecondWhenUnfocused = 100;
    private const int RefreshEventPollPerSecondWhenMinimized = 15;

    private readonly bool _isWindowlessStyle;
    private readonly HVImageLoader _imageLoader;
    private readonly SavedData _config;

    private Sdl2Window _window;
    private GraphicsDevice _gd;
    private CommandList _cl;

    private CustomImGuiController _controller;

    // UI state
    private readonly RgbaFloat _transparentClearColor = new RgbaFloat(0f, 0f, 0f, 0f);
    
    // Overlay only
    private Texture _overlayTexture;
    private Texture _depthTexture;
    private Framebuffer _overlayFramebuffer;
    
    // Tabs
    private readonly int _windowWidth;
    private readonly int _windowHeight;
    
    public HVRendering(bool isWindowlessStyle, int windowWidth, int windowHeight, HVImageLoader imageLoader, SavedData config)
    {
        _isWindowlessStyle = isWindowlessStyle;

        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
        
        _imageLoader = imageLoader;
        _config = config;
    }
    
    private void SubmitUI()
    {
        OnSubmitUi?.Invoke(_controller, _window);
    }

    public bool UpdateIteration(Stopwatch stopwatch)
    {
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.PumpEvents();
            return true;
        }
        SetAsActiveContext();

        var deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
        var snapshot = _window.PumpEvents();
        if (!_window.Exists) return false;

        _controller.Update(deltaTime, snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui.

        SubmitUI();

        _cl.Begin();
        _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
        DoClearColor();
        _controller.Render(_gd, _cl);
        _cl.End();
        _gd.SubmitCommands(_cl);
        _gd.SwapBuffers(_gd.MainSwapchain);
            
        return true;
    }

    public void UiLoop()
    {
        // Create window, GraphicsDevice, and all resources necessary for the demo.
        var width = _windowWidth;
        var height = _windowHeight;
        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, width, height, WindowState.Normal, $"{HVApp.AppTitle}"),
            new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            out _window,
            out _gd);
        if (_isWindowlessStyle)
        {
            _window.Resizable = false;
        }
        _window.Resized += () =>
        {
            _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
            _controller.WindowResized(_window.Width, _window.Height);
        };
        _cl = _gd.ResourceFactory.CreateCommandList();
        _controller = new CustomImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
        
        _imageLoader.Provide(_gd, _controller);

        var timer = Stopwatch.StartNew();
        timer.Start();
        var stopwatch = Stopwatch.StartNew();
        var deltaTime = 0f;
        // Main application loop
        while (_window.Exists)
        {
            if (!_window.Focused)
            {
                Thread.Sleep(1000 / RefreshFramesPerSecondWhenUnfocused);
            }
            // else: Do not limit framerate.
            
            while (_window.WindowState == WindowState.Minimized)
            {
                Thread.Sleep(1000 / RefreshEventPollPerSecondWhenMinimized);
                
                // TODO: We need to know when the window is no longer minimized.
                // How to properly poll events while minimized?
                _window.PumpEvents();
            }
            
            deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
            stopwatch.Restart();
            var snapshot = _window.PumpEvents();
            if (!_window.Exists) break;
            _controller.Update(deltaTime,
                snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui.

            SubmitUI();

            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            DoClearColor();
            _controller.Render(_gd, _cl);
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers(_gd.MainSwapchain);
        }

        // Clean up Veldrid resources
        _gd.WaitForIdle();
        _controller.Dispose();
        _cl.Dispose();
        _gd.Dispose();
    }

    #region Support for SteamVR Overlay
    
    public void SetupUi(bool actuallyWindowless)
    {
        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, _windowWidth, _windowHeight, actuallyWindowless ? WindowState.Hidden : WindowState.Normal, actuallyWindowless ? $"{HVApp.AppTitle}-Windowless" : HVApp.AppTitle),
            new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            // I am forcing this to Direct3D11, because my current implementation requires
            // that GetOverlayTexturePointer / GetTexturePointer would get the IntPtr from D3D11.
            // It may be possible later to use OpenGL or something else as long as we make sure that
            // the GetOverlayTexturePointer can follow along.
            GraphicsBackend.Direct3D11,
            out _window,
            out _gd);
        _window.Resizable = !actuallyWindowless;
        _window.Resized += () =>
        {
            // FIXME: It might not be necessary to resize the swapchain, since we don't use that.
            // Actually, we don't ever resize the window either.
            _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);

            if (actuallyWindowless)
            {
                TeardownFramebuffer();
                SetupFramebuffer();
            }
            _controller.WindowResized(_window.Width, _window.Height);
        };
        _cl = _gd.ResourceFactory.CreateCommandList();

        if (actuallyWindowless)
        {
            SetupFramebuffer();
        }

        // I've wasted several hours of dev because I forgot to pass our own framebuffer OutputDescription to this thing.
        _controller = new CustomImGuiController(_gd, actuallyWindowless ? _overlayFramebuffer.OutputDescription : _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
        
        _imageLoader.Provide(_gd, _controller);
    }

    private void SetupFramebuffer()
    {
        // I am creating a new framebuffer, because I think the _gd.MainSwapchain uses format B8_G8_R8_A8_UNorm,
        // instead of R8_G8_B8_A8_UNorm, which causes OpenVR.SetOverlayTexture to return InvalidTexture?
        // Not sure if there's a better way to do this that wouldn't require creating our own custom framebuffer.
        // There's a lot of trial and error involved below, I'm new to this framebuffer business.
        _overlayTexture = _gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            width: (uint)_window.Width,
            height: (uint)_window.Height,
            mipLevels: 1, 
            arrayLayers: 1, 
            // format: PixelFormat.B8_G8_R8_A8_UNorm, 
            format: PixelFormat.R8_G8_B8_A8_UNorm,
            usage: TextureUsage.RenderTarget | TextureUsage.Sampled
        ));
        _depthTexture = _gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            width: (uint)_window.Width,
            height: (uint)_window.Height,
            mipLevels: 1, 
            arrayLayers: 1, 
            format: PixelFormat.R16_UNorm, 
            usage: TextureUsage.DepthStencil
        ));
        _overlayFramebuffer = _gd.ResourceFactory.CreateFramebuffer(new FramebufferDescription(_depthTexture, _overlayTexture));
    }

    private void TeardownFramebuffer()
    {
        // Not sure if that's the correct way to dispose of those resources, I'm winging it.
        _overlayTexture.Dispose();
        _depthTexture.Dispose();
        _overlayFramebuffer.Dispose();
    }

    public IntPtr GetOverlayTexturePointer()
    {
        // For more info, check https://github.com/ValveSoftware/openvr/blob/master/headers/openvr.h#L126C30-L126C40 (ETextureType definition)
        // - TextureType_DirectX = 0, // Handle is an ID3D11Texture
        return _gd.GetD3D11Info().GetTexturePointer(_overlayTexture);
        
        // TODO: Support other graphics backends
        // - TextureType_OpenGL = 1,  // Handle is an OpenGL texture name or an OpenGL render buffer name, depending on submit flags
        // - TextureType_Vulkan = 2, // Handle is a pointer to a VRVulkanTextureData_t structure
    }

    public InputSnapshot DoPumpEvents()
    {
        return _window.PumpEvents();
    }

    public void UpdateAndRender(Stopwatch stopwatch, InputSnapshot snapshot)
    {
        var deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
        
        _controller.Update(deltaTime, snapshot);
        
        SubmitUI();
        
        _cl.Begin();
        _cl.SetFramebuffer(_overlayFramebuffer);
        DoClearColor();
        _controller.Render(_gd, _cl);
        _cl.End();
        _gd.SubmitCommands(_cl);
        _gd.SwapBuffers(_gd.MainSwapchain);
    }

    public void TeardownWindowlessUi(bool actuallyWindowless)
    {
        if (actuallyWindowless)
        {
            TeardownFramebuffer();
        }
        
        // Clean up Veldrid resources
        _gd.WaitForIdle();
        _controller.Dispose();
        _cl.Dispose();
        _gd.Dispose();
    }
    
    #endregion

    public Vector2 WindowSize()
    {
        return new Vector2(_window.Width, _window.Height);
    }

    public void SetAsActiveContext()
    {
        _controller.SetAsActiveContext();
    }

    public bool HandleSleep()
    {
        if (_window.WindowState == WindowState.Minimized)
        {
            Thread.Sleep(1000 / RefreshEventPollPerSecondWhenMinimized);
                
            // TODO: We need to know when the window is no longer minimized.
            // How to properly poll events while minimized?
            _window.PumpEvents();

            return false;
        }

        if (!_window.Focused)
        {
            Thread.Sleep(1000 / RefreshFramesPerSecondWhenUnfocused);
        }
        // else: Do not limit framerate.
        
        return true;
    }

    private void DoClearColor()
    {
        // This makes the background transparent for the eye tracking menus.
        _cl.ClearColorTarget(0, _transparentClearColor);

        if (_config.devTools__TestTransparency)
        {
            _cl.ClearColorTarget(0, new RgbaFloat(1f, 0f, 0f, 1f));
        }
    }
}