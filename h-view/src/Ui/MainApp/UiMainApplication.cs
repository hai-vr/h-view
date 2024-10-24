using System.Collections.Concurrent;
using System.Diagnostics;
using Hai.ExternalExpressionsMenu;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.Gui;
using Hai.HView.OSC;
using Hai.HView.Rendering;
using ImGuiNET;
using Veldrid.Sdl2;
using Vector2 = System.Numerics.Vector2;

namespace Hai.HView.Ui.MainApp;

internal class UiMainApplication : IDisposable, IEyeTrackingCapable
{
    private const int BorderWidth = 0;
    private const int BorderHeight = BorderWidth;
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize;
    private const ImGuiWindowFlags WindowFlagsNoCollapse = WindowFlags | ImGuiWindowFlags.NoCollapse;

    private const bool ShowFrameCounter = false;
    private const int SidePanelWidth = 165;

    private readonly HVRoutine _routine;
    private readonly bool _isWindowlessStyle;
    private readonly HVImageLoader _imageLoader;
    private readonly UiThemeUpdater _themeUpdater;

    private readonly ConcurrentQueue<Action> _queuedForUi = new ConcurrentQueue<Action>();
    private bool _eyeTrackingMenuActiveLastFrame;
    
    // Externally set
    private bool _isBeingViewedThroughHandOverlay;

    // UI state
    private readonly ImGuiVRCore VrGui;
    private readonly UiSharedData _sharedData;
    private readonly UiScrollManager _scrollManager = new UiScrollManager();
    
    // Tabs
    private readonly UiExpressions _expressionsTab;
    private readonly UiCostumes _costumesTab;
    private readonly UiOscQuery _oscQueryTab;
    private readonly UiNetworking _networkingTabOptional;
    private readonly UiEyeTrackingMenu _eyeTrackingMenu;
    private readonly UiHardware _hardwareTab;
    private readonly UiOptions _optionsTab;
    private readonly UiProcessing _processingTab;
    private readonly UiUtility _utilityTab;
    private readonly SavedData _config;
    private readonly int _trimWidth;
    private readonly int _trimHeight;
    
    // Debug
    private long frameNumber;
    private HPanel _panel;

    public UiMainApplication(HVRoutine routine, bool isWindowlessStyle, int windowWidth, int windowHeight, int innerWidth, int innerHeight, SavedData config, HVImageLoader imageLoader, UiThemeUpdater themeUpdater)
    {
        _routine = routine;
        _isWindowlessStyle = isWindowlessStyle;
        routine.OnManifestChanged += OnManifestChanged;

        _config = config;
        _imageLoader = imageLoader;
        _themeUpdater = themeUpdater;
        _trimWidth = (windowWidth - innerWidth) / 2;
        _trimHeight = (windowHeight - innerHeight) / 2;
        
        _sharedData = new UiSharedData();
        VrGui = new ImGuiVRCore();

        var oscQueryTab = new UiOscQuery(VrGui, _routine, _sharedData);
        _expressionsTab = new UiExpressions(VrGui, _routine, _imageLoader, oscQueryTab, _sharedData);
        _costumesTab = new UiCostumes(VrGui, _routine, _scrollManager, isWindowlessStyle, _imageLoader);
        _oscQueryTab = oscQueryTab;
        _networkingTabOptional = ConditionalCompilation.IncludesSteamworks ? new UiNetworking(VrGui, _routine, _config) : null;
        _eyeTrackingMenu = new UiEyeTrackingMenu(VrGui, isWindowlessStyle, _imageLoader, _sharedData);
        _hardwareTab = new UiHardware(VrGui, _routine, _config);
        _optionsTab = new UiOptions(VrGui, SwitchPanel, _routine, _config, _isWindowlessStyle, _scrollManager);
        _processingTab = new UiProcessing(VrGui, _routine);
        _utilityTab = new UiUtility(VrGui, _scrollManager, _routine, _processingTab, _config);
        _panel = _config.modeVrc ? HPanel.Shortcuts : HPanel.None;
    }

    public void Dispose()
    {
        // TODO: This class may need a setup/teardown.
        _routine.OnManifestChanged -= OnManifestChanged;
    }

    private void OnManifestChanged(EMManifest newManifest) => _queuedForUi.Enqueue(() =>
    {
        _sharedData.ManifestNullable = newManifest;
        _imageLoader.FreeImagesFromMemory();
        _sharedData.ShortcutsNullable = ShortcutResolver.RebuildManifestAsShortcuts(newManifest);
        BuildIsLocalTable(newManifest);
    });

    private void BuildIsLocalTable(EMManifest manifest)
    {
        foreach (var param in manifest.expressionParameters)
        {
            _sharedData.isLocal[param.parameter] = !param.synced;
        }
    }

    public enum HPanel
    {
        None,
        Shortcuts,
        Costumes,
        Networking,
        Parameters,
        Hardware,
        Processing,
        Options,
        Tabs,
        Thirdparty,
        DevTools
    }

    public void SubmitUI(CustomImGuiController controller, Sdl2Window window)
    {
        _themeUpdater.ApplyStyleAdjustments(_isWindowlessStyle);
        
        var sw = new Stopwatch();
        sw.Start();

        if (true)
        {
            _themeUpdater.OverrideStyleWithTheme(UiColors.V3(UiColors.ActiveButton));
        }
        else
        {
            _themeUpdater.Reset();
        }

        var smallFont = _isWindowlessStyle ? _config.useSmallFontVR : _config.useSmallFontDesktop;

        ImGui.PushFont(smallFont ? controller.SmallFont : controller.MainFont);
        while (_queuedForUi.TryDequeue(out var action))
        {
            action.Invoke();
        }

        var windowHeight = window.Height - BorderHeight * 2;

        VrGui.Begin();

        ImGui.SetNextWindowPos(new Vector2(BorderWidth + _trimWidth, BorderHeight + _trimHeight), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(SidePanelWidth - _trimWidth * 2, windowHeight - _trimHeight * 2), ImGuiCond.Always);

        ImGui.Begin("###sidepanel", WindowFlagsNoCollapse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
        
        var buttonSize = new Vector2(ImGui.GetWindowWidth() - 16, 35);
        if (_config.modeVrc) ShowSidebarButton(buttonSize, HLocalizationPhrase.ShortcutsTabLabel, HPanel.Shortcuts);
        if (_config.modeVrc) ShowSidebarButton(buttonSize, HLocalizationPhrase.CostumesTabLabel, HPanel.Costumes);
        if (_networkingTabOptional != null) ShowSidebarButton(buttonSize, HLocalizationPhrase.NetworkingTabLabel, HPanel.Networking);
        if (_config.modeVrc) ShowSidebarButton(buttonSize, HLocalizationPhrase.ParametersTabLabel, HPanel.Parameters);
        if (ConditionalCompilation.IncludesOpenVR) ShowSidebarButton(buttonSize, HLocalizationPhrase.HardwareTabLabel, HPanel.Hardware);
        ShowSidebarButton(buttonSize, HLocalizationPhrase.OptionsTabLabel, HPanel.Options);
        ShowSidebarButton(buttonSize, HLocalizationPhrase.TabsTabLabel, HPanel.Tabs);
        // ShowSidebarButton(buttonSize, HLocalizationPhrase.ProcessingTabLabel, HPanel.Processing);
        
        var languages = HLocalization.GetLanguages();
        var desiredY = ImGui.GetWindowHeight() - ImGui.GetTextLineHeight() * 2f - (buttonSize.Y + 6) * languages.Count;
        if (ImGui.GetCursorPosY() < desiredY) // Only show the language selector if we have room for it
        {
            ImGui.SetCursorPosY(desiredY);
            for (var languageIndex = 0; languageIndex < languages.Count; languageIndex++)
            {
                var language = languages[languageIndex].Replace(" (ChatGPT)" , " GPT");
                if (VrGui.HapticButton(language, buttonSize))
                {
                    _routine.SetLocale(HLocalization.GetLanguageCodes()[languageIndex]);
                    HLocalization.SwitchLanguage(languageIndex);
                }
            }
        }
        else
        {
            ImGui.SetCursorPosY(ImGui.GetWindowHeight() - ImGui.GetTextLineHeight() * 3);
            if (VrGui.HapticButton(UiOptions.LanguagesNonTranslated, buttonSize))
            {
                _panel = HPanel.Options;
            }
        }

        var overdata = $"{VERSION.miniVersion}";
        ImGui.SetCursorPosX(16);
        ImGui.SetCursorPosY(ImGui.GetWindowHeight() - ImGui.GetTextLineHeight() - 8);
        ImGui.Text(overdata);

        ImGui.End();

        var windowWidth = window.Width - BorderWidth * 2 - SidePanelWidth;
        ImGui.SetNextWindowPos(new Vector2(SidePanelWidth + BorderWidth + _trimWidth, BorderHeight + _trimHeight), ImGuiCond.Always);
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
        switch (_panel)
        {
            case HPanel.Shortcuts:
                _scrollManager.MakeScroll(() => _expressionsTab.ShortcutsTab(oscMessages));
                break;
            case HPanel.Costumes:
                _scrollManager.MakeScroll(() => _costumesTab.CostumesTab(oscMessages));
                break;
            case HPanel.Networking:
                _scrollManager.MakeScroll(() => _networkingTabOptional?.NetworkingTab());
                break;
            case HPanel.Parameters:
                _scrollManager.MakeScroll(() => ParametersTab(oscMessages));
                break;
            case HPanel.Hardware:
                _scrollManager.MakeScroll(() => _hardwareTab.HardwareTab());
                break;
            case HPanel.Options:
                _scrollManager.MakeScroll(() => _optionsTab.OptionsTab());
                break;
            case HPanel.Processing:
                _scrollManager.MakeScroll(() => _processingTab.ProcessingTab());
                break;
            case HPanel.Tabs:
                DisplayAsTabs(isEyeTrackingMenuBeingViewedThroughHandOverlay, oscMessages);
                break;
            case HPanel.Thirdparty:
                _optionsTab.ThirdPartyTab();
                break;
            case HPanel.DevTools:
                _optionsTab.DevToolsTab();
                break;
            case HPanel.None:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _scrollManager.StoreIfAnyItemHovered();

        VrGui.End();

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
        if (UiColors.ColoredBackground(_panel == target, () => VrGui.HapticButton(label, buttonSize))) _panel = target;
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
            if (_config.modeVrc) _scrollManager.MakeUnscrollableTab(HLocalizationPhrase.AvatarTabLabel, () =>
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

    public void SetEyeTracking(bool usingEyeTracking)
    {
        _sharedData.usingEyeTracking = usingEyeTracking;
    }

    public void SetIsHandOverlay(bool isHandOverlay)
    {
        _isBeingViewedThroughHandOverlay = isHandOverlay;
    }

    private void SwitchPanel(HPanel newPanel)
    {
        _panel = newPanel;
    }

    public void RegisterHoverChanged(ImGuiVRCore.ButtonEvent buttonEvent)
    {
        VrGui.OnHoverChanged += buttonEvent;
    }

    public void RegisterButtonPressed(ImGuiVRCore.ButtonEvent buttonEvent)
    {
        VrGui.OnButtonPressed += buttonEvent;
    }

    internal static void OpenVrUnavailableBlinker(Stopwatch time)
    {
        var elapsedMilliseconds = time.ElapsedMilliseconds;
        var switchEveryHalfSecond = (2 * elapsedMilliseconds / 1000) % 2 == 0;
        ImGui.PushStyleColor(ImGuiCol.Text, switchEveryHalfSecond ? UiColors.ErroringRed : UiColors.RegularWhite);
        ImGui.TextWrapped(HLocalizationPhrase.MsgOpenVrIsNotRunning);
        ImGui.PopStyleColor();
    }
}