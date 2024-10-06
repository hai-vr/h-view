using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Hai.ExternalExpressionsMenu;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.Gui.Tab;
using Hai.HView.OSC;
using Hai.HView.Ui;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Vector2 = System.Numerics.Vector2;

namespace Hai.HView.Gui;

public partial class HVInnerWindow : IDisposable
{
    public event ButtonEvent OnHoverChanged;
    public event ButtonEvent OnButtonPressed;
    public delegate void ButtonEvent();
    
    private const int BorderWidth = 0;
    private const int BorderHeight = BorderWidth;
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize;
    private const ImGuiWindowFlags WindowFlagsNoCollapse = WindowFlags | ImGuiWindowFlags.NoCollapse;

    private const int RefreshFramesPerSecondWhenUnfocused = 100;
    private const int RefreshEventPollPerSecondWhenMinimized = 15;

    private const bool ShowFrameCounter = false;

    private readonly HVRoutine _routine;
    private readonly bool _isWindowlessStyle;
    private readonly HVImageLoader _imageLoader;

    private Sdl2Window _window;
    private GraphicsDevice _gd;
    private CommandList _cl;

    private CustomImGuiController _controller;

    public EMManifest ManifestNullable { get; private set; }
    private readonly ConcurrentQueue<Action> _queuedForUi = new ConcurrentQueue<Action>();
    internal Dictionary<string, bool> _isLocal = new Dictionary<string, bool>();
    private bool _eyeTrackingMenuActiveLastFrame;
    
    // Externally set
    internal bool usingEyeTracking;
    private bool _isBeingViewedThroughHandOverlay;

    // UI state
    private readonly RgbaFloat _transparentClearColor = new RgbaFloat(0f, 0f, 0f, 0f);
    
    // Overlay only
    private Texture _overlayTexture;
    private Texture _depthTexture;
    private Framebuffer _overlayFramebuffer;
    
    // Tabs
    private readonly UiScrollManager _scrollManager = new UiScrollManager();
    private readonly UiExpressions _expressionsTab;
    private readonly UiCostumes _costumesTab;
    private readonly UiNetworking _networkingTabOptional;
    private readonly UiEyeTrackingMenu _eyeTrackingMenu;
    private readonly UiHardware _hardwareTab;
    private readonly UiOptions _optionsTab;
    private readonly UiUtility _utilityTab;
    private readonly int _windowWidth;
    private readonly int _windowHeight;
    private readonly SavedData _config;
    private readonly int _trimWidth;
    private readonly int _trimHeight;
    
    // Debug
    private long frameNumber;
    private HPanel _panel = HPanel.Shortcuts;
    private bool _debugTransparency;
    private string _hovered;
    private readonly UiOscQuery _oscQueryTab;

    public HVInnerWindow(HVRoutine routine, bool isWindowlessStyle, int windowWidth, int windowHeight, int innerWidth, int innerHeight, SavedData config)
    {
        _routine = routine;
        _isWindowlessStyle = isWindowlessStyle;
        routine.OnManifestChanged += OnManifestChanged;

        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
        _config = config;
        _trimWidth = (windowWidth - innerWidth) / 2;
        _trimHeight = (windowHeight - innerHeight) / 2;
        
        _imageLoader = new HVImageLoader();

        var oscQueryTab = new UiOscQuery(this, _routine, _scrollManager);
        _expressionsTab = new UiExpressions(this, _routine, _imageLoader, oscQueryTab);
        _costumesTab = new UiCostumes(this, _routine, _scrollManager, isWindowlessStyle, _imageLoader);
        _oscQueryTab = oscQueryTab;
        _networkingTabOptional = ConditionalCompilation.IncludesSteamworks ? new UiNetworking(_routine) : null;
        _eyeTrackingMenu = new UiEyeTrackingMenu(this, isWindowlessStyle, _imageLoader);
        _hardwareTab = new UiHardware(this, _routine, _config);
        _optionsTab = new UiOptions(this, _routine, _config, _isWindowlessStyle, _scrollManager);
        _utilityTab = new UiUtility(_scrollManager, _routine);
    }

    public void Dispose()
    {
        // TODO: This class may need a setup/teardown.
        _routine.OnManifestChanged -= OnManifestChanged;
    }

    private void OnManifestChanged(EMManifest newManifest) => _queuedForUi.Enqueue(() =>
    {
        ManifestNullable = newManifest;
        _imageLoader.FreeImagesFromMemory();
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

    internal bool HapticButton(string label)
    {
        var clicked = ImGui.Button(label);
        CheckHapticButton(label);
        return clicked;
    }

    internal bool HapticImageButton(string label, IntPtr textureId, Vector2 size)
    {
        var clicked = ImGui.ImageButton(label, textureId, size);
        CheckHapticButton(label);
        return clicked;
    }

    internal bool HapticButton(string label, Vector2 size)
    {
        var clicked = ImGui.Button(label, size);
        CheckHapticButton(label);
        return clicked;
    }

    private void CheckHapticButton(string label)
    {
        if (ImGui.IsItemHovered())
        {
            _hovered = label;
        }

        if (ImGui.IsItemClicked())
        {
            OnButtonPressed?.Invoke();
        }
    }

    public enum HPanel
    {
        Shortcuts,
        Costumes,
        Networking,
        Parameters,
        Hardware,
        Options,
        Tabs,
        Thirdparty,
        DevTools
    }

    private void SubmitUI()
    {
        var sw = new Stopwatch();
        sw.Start();
        
        var smallFont = _isWindowlessStyle ? _config.useSmallFontVR : _config.useSmallFontDesktop;

        ImGui.PushFont(smallFont ? _controller.SmallFont : _controller.MainFont);
        while (_queuedForUi.TryDequeue(out var action))
        {
            action.Invoke();
        }

        var windowHeight = _window.Height - BorderHeight * 2;

        var useTabs = false;
        var prevHover = _hovered;

        int sidePanel;
        if (useTabs)
        {
            sidePanel = 0;
        }
        else
        {
            sidePanel = 165;

            ImGui.SetNextWindowPos(new Vector2(BorderWidth + _trimWidth, BorderHeight + _trimHeight), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(sidePanel - _trimWidth * 2, windowHeight - _trimHeight * 2), ImGuiCond.Always);

            ImGui.Begin("###sidepanel", WindowFlagsNoCollapse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
            
            var buttonSize = new Vector2(ImGui.GetWindowWidth() - 16, 35);
            ShowSidebarButton(buttonSize, HLocalizationPhrase.ShortcutsTabLabel, HPanel.Shortcuts);
            ShowSidebarButton(buttonSize, HLocalizationPhrase.CostumesTabLabel, HPanel.Costumes);
            if (_networkingTabOptional != null) ShowSidebarButton(buttonSize, HLocalizationPhrase.NetworkingTabLabel, HPanel.Networking);
            ShowSidebarButton(buttonSize, HLocalizationPhrase.ParametersTabLabel, HPanel.Parameters);
            if (ConditionalCompilation.IncludesOpenVR) ShowSidebarButton(buttonSize, HLocalizationPhrase.HardwareTabLabel, HPanel.Hardware);
            ShowSidebarButton(buttonSize, HLocalizationPhrase.OptionsTabLabel, HPanel.Options);
            ShowSidebarButton(buttonSize, HLocalizationPhrase.TabsTabLabel, HPanel.Tabs);
            
            var languages = HLocalization.GetLanguages();
            var desiredY = ImGui.GetWindowHeight() - ImGui.GetTextLineHeight() * 2f - (buttonSize.Y + 6) * languages.Count;
            if (ImGui.GetCursorPosY() < desiredY) // Only show the language selector if we have room for it
            {
                ImGui.SetCursorPosY(desiredY);
                for (var languageIndex = 0; languageIndex < languages.Count; languageIndex++)
                {
                    var language = languages[languageIndex].Replace(" (ChatGPT)" , " GPT");
                    if (ImGui.Button(language, buttonSize))
                    {
                        _routine.SetLocale(HLocalization.GetLanguageCodes()[languageIndex]);
                        HLocalization.SwitchLanguage(languageIndex);
                    }
                }
            }
            else
            {
                ImGui.SetCursorPosY(ImGui.GetWindowHeight() - ImGui.GetTextLineHeight() * 3);
                if (ImGui.Button(UiOptions.LanguagesNonTranslated, buttonSize))
                {
                    _panel = HPanel.Options;
                }
            }

            var overdata = $"{VERSION.miniVersion}";
            ImGui.SetCursorPosX(16);
            ImGui.SetCursorPosY(ImGui.GetWindowHeight() - ImGui.GetTextLineHeight() - 8);
            ImGui.Text(overdata);

            ImGui.End();
        }

        var windowWidth = _window.Width - BorderWidth * 2 - sidePanel;
        ImGui.SetNextWindowPos(new Vector2(sidePanel + BorderWidth + _trimWidth, BorderHeight + _trimHeight), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(windowWidth - _trimWidth * 2, windowHeight - _trimHeight * 2), ImGuiCond.Always);
        var flags = WindowFlagsNoCollapse | ImGuiWindowFlags.NoTitleBar;

        var isEyeTrackingMenuBeingViewedThroughHandOverlay = _isWindowlessStyle && _isBeingViewedThroughHandOverlay && _eyeTrackingMenuActiveLastFrame;
        if (isEyeTrackingMenuBeingViewedThroughHandOverlay)
        {
            flags |= ImGuiWindowFlags.NoBackground;
        }

        frameNumber++;
        ImGui.Begin($"{HVApp.AppTitleTab} {VERSION.version}", flags);
        var oscMessages = _routine.UiOscMessages();
        if (useTabs)
        {
            DisplayAsTabs(isEyeTrackingMenuBeingViewedThroughHandOverlay, oscMessages);
        }
        else
        {
            switch (_panel)
            {
                case HPanel.Shortcuts:
                {
                    _scrollManager.MakeScroll(() => _expressionsTab.ShortcutsTab(oscMessages));
                    break;
                }
                case HPanel.Costumes:
                {
                    _scrollManager.MakeScroll(() => _costumesTab.CostumesTab(oscMessages));
                    break;
                }
                case HPanel.Networking:
                {
                    _scrollManager.MakeScroll(() => _networkingTabOptional?.NetworkingTab());
                    break;
                }
                case HPanel.Parameters:
                {
                    _scrollManager.MakeScroll(() => ParametersTab(oscMessages));
                    break;
                }
                case HPanel.Hardware:
                {
                    _scrollManager.MakeScroll(() => _hardwareTab.HardwareTab());
                    break;
                }
                case HPanel.Options:
                {
                    _scrollManager.MakeScroll(() => _optionsTab.OptionsTab());
                    break;
                }
                case HPanel.Tabs:
                {
                    DisplayAsTabs(isEyeTrackingMenuBeingViewedThroughHandOverlay, oscMessages);
                    break;
                }
                case HPanel.Thirdparty:
                {
                    _optionsTab.ThirdPartyTab();
                    break;
                }
                case HPanel.DevTools:
                {
                    _optionsTab.DevToolsTab();
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _scrollManager.StoreIfAnyItemHovered();
        }
        
        if (_isWindowlessStyle && _hovered != prevHover)
        {
            OnHoverChanged?.Invoke();
        }

        ImGui.End();
        ImGui.PopFont();

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

    private void ShowSidebarButton(Vector2 buttonSize, string label, HPanel target)
    {
        if (ColoredBg(_panel == target, () => HapticButton(label, buttonSize))) _panel = target;
    }

    private bool ColoredBg(bool useColor, Func<bool> func)
    {
        if (useColor) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 1, 1, 0.75f));
        var result = func.Invoke();
        if (useColor) ImGui.PopStyleColor();
        
        return result;
    }

    private void DisplayAsTabs(bool isEyeTrackingMenuBeingViewedThroughHandOverlay, Dictionary<string, HOscItem> oscMessages)
    {
        if (ShowFrameCounter)
        {
            ImGui.Text($"[frame {frameNumber}, delta {(ImGui.GetIO().DeltaTime * 1000):0}ms]");
        }

        if (!isEyeTrackingMenuBeingViewedThroughHandOverlay)
        {
            ImGui.BeginTabBar("##tabs");
            _scrollManager.MakeUnscrollableTab(HLocalizationPhrase.AvatarTabLabel, () =>
            {
                ImGui.BeginTabBar("##tabs_menulike");
                _scrollManager.MakeTab(HLocalizationPhrase.ShortcutsTabLabel, () => _expressionsTab.ShortcutsTab(oscMessages));
                _scrollManager.MakeUnscrollableTab(HLocalizationPhrase.CostumesTabLabel, () => _costumesTab.CostumesTab(oscMessages));
                if (ImGui.BeginTabItem(HLocalizationPhrase.ParametersTabLabel))
                {
                    ParametersTab(oscMessages);
                    ImGui.EndTabItem();
                }

                _eyeTrackingMenuActiveLastFrame = false;
                _scrollManager.MakeUnscrollableTab(HLocalizationPhrase.EyeTrackingTabLabel, () =>
                {
                    _eyeTrackingMenuActiveLastFrame = true;
                    _eyeTrackingMenu.EyeTrackingMenuTab();
                });
                ImGui.EndTabBar();
            });
            if (_networkingTabOptional != null) _scrollManager.MakeTab(HLocalizationPhrase.NetworkingTabLabel, () => _networkingTabOptional.NetworkingTab());
            _scrollManager.MakeUnscrollableTab(HLocalizationPhrase.UtilityTabLabel, () => _utilityTab.UtilityTab(oscMessages));
            _scrollManager.MakeUnscrollableTab(HLocalizationPhrase.HardwareTabLabel, () => _hardwareTab.HardwareTab());
            _scrollManager.MakeUnscrollableTab(HLocalizationPhrase.OptionsTabLabel, () => _optionsTab.OptionsTab());
            ImGui.EndTabBar();
        }
        else
        {
            _eyeTrackingMenuActiveLastFrame = true;
            _eyeTrackingMenu.EyeTrackingMenuTab();
        }

        _scrollManager.StoreIfAnyItemHovered();
    }

    private void ParametersTab(Dictionary<string, HOscItem> oscMessages)
    {
        ImGui.BeginTabBar("##tabs_parameters");
        _scrollManager.MakeTab(HLocalizationPhrase.DefaultTabLabel, () => _oscQueryTab.AvatarTab(oscMessages));
        _scrollManager.MakeTab(HLocalizationPhrase.FaceTrackingTabLabel, () => _oscQueryTab.FaceTrackingTab(oscMessages));
        _scrollManager.MakeTab(HLocalizationPhrase.InputTabLabel, () => _oscQueryTab.InputTab(oscMessages));
        _scrollManager.MakeTab(HLocalizationPhrase.TrackingTabLabel, () => _oscQueryTab.TrackingTab(oscMessages));
        _scrollManager.MakeTab(HLocalizationPhrase.ContactsTabLabel, () => _expressionsTab.ContactsTab(oscMessages));
        _scrollManager.MakeTab(HLocalizationPhrase.PhysBonesLabel, () => _expressionsTab.PhysBonesTab(oscMessages));
        _scrollManager.MakeTab(HLocalizationPhrase.MenuTabLabel, () => _expressionsTab.ExpressionsTab(oscMessages));
        ImGui.EndTabBar();
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

    public void SetEyeTracking(bool usingEyeTracking)
    {
        this.usingEyeTracking = usingEyeTracking;
    }

    public void SetIsHandOverlay(bool isHandOverlay)
    {
        _isBeingViewedThroughHandOverlay = isHandOverlay;
    }

    private void DoClearColor()
    {
        // This makes the background transparent for the eye tracking menus.
        _cl.ClearColorTarget(0, _transparentClearColor);

        if (_debugTransparency)
        {
            _cl.ClearColorTarget(0, new RgbaFloat(1f, 0f, 0f, 1f));
        }
    }

    public void SwitchPanel(HPanel newPanel)
    {
        _panel = newPanel;
    }
}