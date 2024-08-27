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
    private const string InputTabLabel = "Input";
    private const string TrackingTabLabel = "Tracking";
    private const string MenuTabLabel = "Menu";
    private const string ContactsTabLabel = "Contacts";
    private const string PhysBonesLabel = "PhysBones";

    private readonly HVRoutine _routine;

    private Sdl2Window _window;
    private GraphicsDevice _gd;
    private CommandList _cl;

    private CustomImGuiController _controller;

    private EMManifest _manifestNullable;
    private readonly ConcurrentQueue<Action> _queuedForUi = new ConcurrentQueue<Action>();

    // UI state
    private readonly Vector3 _clearColor = new(0.45f, 0.55f, 0.6f);
    private byte[] _chatboxBuffer = new byte[10_000];
    private bool _chatboxB;
    private bool _chatboxN;
        
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
    });

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
        if (ImGui.BeginTabItem(AvatarTabLabel))
        {
            ImGui.BeginChild("scroll");
            AvatarTab(oscMessages);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Face Tracking"))
        {
            ImGui.BeginChild("scroll");
            FaceTrackingTab(oscMessages);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(InputTabLabel))
        {
            ImGui.BeginChild("scroll");
            InputTab(oscMessages);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(TrackingTabLabel))
        {
            ImGui.BeginChild("scroll");
            TrackingTab(oscMessages);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(MenuTabLabel))
        {
            ImGui.BeginChild("scroll");
            ExpressionsTab(oscMessages);
            HandleScrollOnDrag(ImGui.GetIO().MouseDelta, ImGuiMouseButton.Left);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Shortcuts"))
        {
            ImGui.BeginChild("scroll");
            ShortcutsTab(oscMessages);
            // ScrollWhenDraggingOnVoid(ImGui.GetIO().MouseDelta, ImGuiMouseButton.Left);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(ContactsTabLabel))
        {
            ImGui.BeginChild("scroll");
            ContactsTab(oscMessages);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(PhysBonesLabel))
        {
            ImGui.BeginChild("scroll");
            PhysBonesTab(oscMessages);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }
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

    public void Run()
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
}