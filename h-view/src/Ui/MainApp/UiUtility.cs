using System.Numerics;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.OSC;
using ImGuiNET;

namespace Hai.HView.Ui.MainApp;

internal class UiUtility
{
    private readonly ImGuiVRCore VrGui;
    private readonly UiScrollManager _scrollManager;
    private readonly HVRoutine _routine;
    private readonly UiProcessing _processingTab;
    private readonly SavedData _config;

    private const string KeysTabLabel = "Keys";
    private readonly Dictionary<int, bool> _utilityClick = new Dictionary<int, bool>();
    private string[] _thirdPartyLateInit;

    public UiUtility(ImGuiVRCore vrGui, UiScrollManager scrollManager, HVRoutine routine, UiProcessing processingTab, SavedData config)
    {
        VrGui = vrGui;
        _scrollManager = scrollManager;
        _routine = routine;
        _processingTab = processingTab;
        _config = config;
    }

    public void UtilityTab(Dictionary<string, HOscItem> oscMessages)
    {
        ImGui.BeginTabBar("##tabs_keys");
        if (_config.modeVrc) _scrollManager.MakeTab(KeysTabLabel, () => KeysTab(oscMessages));
        _scrollManager.MakeTab(HLocalizationPhrase.ProcessingTabLabel, () => _processingTab.ProcessingTab());
        ImGui.EndTabBar();
    }

    private void KeysTab(Dictionary<string, HOscItem> oscMessages)
    {
        var id = 0;

        var size = new Vector2(ImGui.GetWindowWidth() / 2, 40);
        VrGui.HapticButton(HLocalizationPhrase.QuickMenuLeftLabel, size);
        SimplePressEvent(ref id, "/input/QuickMenuToggleLeft");
        ImGui.SameLine();
        VrGui.HapticButton(HLocalizationPhrase.QuickMenuRightLabel, size);
        SimplePressEvent(ref id, "/input/QuickMenuToggleRight");
        
        ImGui.Text("");

        if (oscMessages.TryGetValue("/avatar/parameters/MuteSelf", out var item))
        {
            var isMuted = item.Values[0] is bool ? (bool)item.Values[0] : false;
            
            VrGui.HapticButton($"Voice is {(isMuted ? "OFF" : "ON")}###voiceToggle", size);
            SimplePressEvent(ref id, "/input/Voice");
            
            var size2 = new Vector2(ImGui.GetWindowWidth() / 5, 40);
            ImGui.SameLine();

            _utilityClick.TryGetValue(id, out var offPressed);
            ImGui.BeginDisabled(isMuted && !offPressed);
            VrGui.HapticButton("Turn OFF", size2);
            SimplePressEvent(ref id, "/input/Voice");
            ImGui.EndDisabled();
            
            ImGui.SameLine();

            _utilityClick.TryGetValue(id, out var onPressed);
            ImGui.BeginDisabled(!isMuted && !onPressed);
            VrGui.HapticButton("Turn ON", size2);
            SimplePressEvent(ref id, "/input/Voice");
            ImGui.EndDisabled();
        }
    }

    private void SimplePressEvent(ref int identifier, string address)
    {
        _utilityClick.TryGetValue(identifier, out var wasPressed);
        var isPressed = ImGui.IsItemActive();
        if (wasPressed != isPressed)
        {
            _utilityClick[identifier] = isPressed;
            _routine.UpdateMessage(address, isPressed);
        }

        identifier++;
    }
}