using System.Collections.Concurrent;
using System.Diagnostics;
using Hai.ExternalExpressionsMenu;
using Hai.HView.Core;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Hai.HView.Gui;

public partial class HVInnerWindow : IDisposable
{
    private const int BorderWidth = 0;
    private const int BorderHeight = BorderWidth;
    private const int TotalWindowWidth = 600;
    private const int TotalWindowHeight = 510;
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize;
    private const ImGuiWindowFlags WindowFlagsNoCollapse = WindowFlags | ImGuiWindowFlags.NoCollapse;
    private const string AvatarTabLabel = "Avatar";
    private const string ContactsTabLabel = "Contacts";
    private const string FaceTrackingTabLabel = "Face Tracking";
    private const string InputTabLabel = "Input";
    private const string MenuTabLabel = "Menu";
    private const string PhysBonesLabel = "PhysBones";
    private const string ShortcutsTabLabel = "Shortcuts";
    private const string TrackingTabLabel = "Tracking";
    private const string UtilityTabLabel = "Utility";

    private readonly HVRoutine _routine;

    private Sdl2Window _window;
    private GraphicsDevice _gd;
    private CommandList _cl;

    private CustomImGuiController _controller;

    private EMManifest _manifestNullable;
    private readonly ConcurrentQueue<Action> _queuedForUi = new ConcurrentQueue<Action>();
    private Dictionary<string, bool> _isLocal = new Dictionary<string, bool>();

    // UI state
    private readonly Vector3 _clearColor = new(0.45f, 0.55f, 0.6f);
    private byte[] _chatboxBuffer = new byte[10_000];
    private bool _chatboxB;
    private bool _chatboxN;
    
    // Overlay only
    private Texture _overlayTexture;
    private Texture _depthTexture;
    private Framebuffer _overlayFramebuffer;

    public HVInnerWindow(HVRoutine routine)
    {
        _routine = routine;
        routine.OnManifestChanged += OnManifestChanged;
    }

    public void Dispose()
    {
        // TODO: This class may need a setup/teardown.
        _routine.OnManifestChanged -= OnManifestChanged;
    }

    private void OnManifestChanged(EMManifest newManifest) => _queuedForUi.Enqueue(() =>
    {
        _manifestNullable = newManifest;
        FreeImagesFromMemory();
        RebuildManifestAsShortcuts(newManifest);
        BuildIsLocalTable(newManifest);
    });

    private void BuildIsLocalTable(EMManifest manifest)
    {
        foreach (var param in manifest.expressionParameters)
        {
            _isLocal[param.parameter] = !param.synced;
        }
    }

    private void SubmitUI()
    {
        while (_queuedForUi.TryDequeue(out var action))
        {
            action.Invoke();
        }
        
        var windowHeight = _window.Height - BorderHeight * 2;
        ImGui.SetNextWindowPos(new Vector2(BorderWidth, BorderHeight), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(_window.Width - BorderWidth * 2, windowHeight), ImGuiCond.Always);
        ImGui.Begin($"{HVApp.AppTitleTab} {VERSION.version}", WindowFlagsNoCollapse);
        ImGui.BeginTabBar("##tabs");
        var oscMessages = _routine.UiOscMessages();
        MakeTab(AvatarTabLabel, false, () => AvatarTab(oscMessages));
        MakeTab(FaceTrackingTabLabel, false, () => FaceTrackingTab(oscMessages));
        MakeTab(InputTabLabel, false, () => InputTab(oscMessages));
        MakeTab(TrackingTabLabel, false, () => TrackingTab(oscMessages));
        MakeTab(MenuTabLabel, true, () => ExpressionsTab(oscMessages));
        MakeTab(ShortcutsTabLabel, false, () => ShortcutsTab(oscMessages));
        MakeTab(ContactsTabLabel, false, () => ContactsTab(oscMessages));
        MakeTab(PhysBonesLabel, false, () => PhysBonesTab(oscMessages));
        MakeTab(UtilityTabLabel, false, () => UtilityTab(oscMessages));
        ImGui.End();

        // 3. Show the ImGui demo window. Most of the sample code is in ImGui.ShowDemoWindow(). Read its code to learn more about Dear ImGui!
        if (false)
        {
            // Normally user code doesn't need/want to call this because positions are saved in .ini file anyway.
            // Here we just want to make the demo initial state a bit more friendly!
            ImGui.SetNextWindowPos(new Vector2(650, 20), ImGuiCond.FirstUseEver);
            var _showImGuiDemoWindow = false;
            ImGui.ShowDemoWindow(ref _showImGuiDemoWindow);
        }
    }

    private void MakeTab(string tabLabel, bool dragScrolls, Action action)
    {
        if (ImGui.BeginTabItem(tabLabel))
        {
            ImGui.BeginChild("scroll");
            action.Invoke();
            if (dragScrolls)
            {
                HandleScrollOnDrag(ImGui.GetIO().MouseDelta, ImGuiMouseButton.Left);
            }
            ImGui.EndChild();
            ImGui.EndTabItem();
        }
    }

    public void UiLoop()
    {
        // Create window, GraphicsDevice, and all resources necessary for the demo.
        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, TotalWindowWidth, TotalWindowHeight, WindowState.Normal, $"{HVApp.AppTitle}"),
            new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            out _window,
            out _gd);
        _window.Resized += () =>
        {
            _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
            _controller.WindowResized(_window.Width, _window.Height);
        };
        _cl = _gd.ResourceFactory.CreateCommandList();
        _controller = new CustomImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width,
            _window.Height);

        var timer = Stopwatch.StartNew();
        timer.Start();
        var stopwatch = Stopwatch.StartNew();
        var deltaTime = 0f;
        // Main application loop
        while (_window.Exists)
        {
            deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
            stopwatch.Restart();
            var snapshot = _window.PumpEvents();
            if (!_window.Exists) break;
            _controller.Update(deltaTime,
                snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui.

            SubmitUI();

            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
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
    
    public void SetupWindowlessUi()
    {
        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, TotalWindowWidth, TotalWindowHeight, WindowState.Hidden, $"{HVApp.AppTitle}"),
            new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            // I am forcing this to Direct3D11, because my current implementation requires
            // that GetOverlayTexturePointer / GetTexturePointer would get the IntPtr from D3D11.
            // It may be possible later to use OpenGL or something else as long as we make sure that
            // the GetOverlayTexturePointer can follow along.
            GraphicsBackend.Direct3D11,
            out _window,
            out _gd);
        _window.Resized += () =>
        {
            // FIXME: It might not be necessary to resize the swapchain, since we don't use that.
            // Actually, we don't ever resize the window either.
            _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
            
            TeardownFramebuffer();
            SetupFramebuffer();
            _controller.WindowResized(_window.Width, _window.Height);
        };
        _cl = _gd.ResourceFactory.CreateCommandList();

        SetupFramebuffer();

        // I've wasted several hours of dev because I forgot to pass our own framebuffer OutputDescription to this thing.
        _controller = new CustomImGuiController(_gd, _overlayFramebuffer.OutputDescription, _window.Width, _window.Height);
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
        return _gd.GetD3D11Info().GetTexturePointer(_overlayTexture);
    }

    public InputSnapshot DoPumpEvents()
    {
        return _window.PumpEvents();
    }

    public void UpdateAndRender(Stopwatch stopwatch, InputSnapshot snapshot)
    {
        var deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
        stopwatch.Restart();
        
        _controller.Update(deltaTime, snapshot);
        
        SubmitUI();
        
        _cl.Begin();
        _cl.SetFramebuffer(_overlayFramebuffer);
        _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
        _controller.Render(_gd, _cl);
        _cl.End();
        _gd.SubmitCommands(_cl);
    }

    public void TeardownWindowlessUi()
    {
        TeardownFramebuffer();
        
        // Clean up Veldrid resources
        _gd.WaitForIdle();
        _controller.Dispose();
        _cl.Dispose();
        _gd.Dispose();
    }
    
    #endregion
}