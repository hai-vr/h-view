using System.Numerics;
using Hai.HView.OSC;
using ImGuiNET;

namespace Hai.HView.Gui;

public partial class HVInnerWindow
{
    private readonly Dictionary<int, bool> _utilityClick = new Dictionary<int, bool>();
    
    private void UtilityTab(Dictionary<string, HOscItem> oscMessages)
    {
        var id = 0;

        var size = new Vector2(ImGui.GetWindowWidth() / 2, 40);
        ImGui.Button("Quick Menu Left", size);
        SimplePressEvent(ref id, "/input/QuickMenuToggleLeft");
        ImGui.SameLine();
        ImGui.Button("Quick Menu Right", size);
        SimplePressEvent(ref id, "/input/QuickMenuToggleRight");
        
        ImGui.Text("");

        if (oscMessages.TryGetValue("/avatar/parameters/MuteSelf", out var item))
        {
            var isMuted = item.Values[0] is bool ? (bool)item.Values[0] : false;
            
            ImGui.Button($"Voice is {(isMuted ? "OFF" : "ON")}###voiceToggle", size);
            SimplePressEvent(ref id, "/input/Voice");
            
            var size2 = new Vector2(ImGui.GetWindowWidth() / 5, 40);
            ImGui.SameLine();

            _utilityClick.TryGetValue(id, out var offPressed);
            ImGui.BeginDisabled(isMuted && !offPressed);
            ImGui.Button("Turn OFF", size2);
            SimplePressEvent(ref id, "/input/Voice");
            ImGui.EndDisabled();
            
            ImGui.SameLine();

            _utilityClick.TryGetValue(id, out var onPressed);
            ImGui.BeginDisabled(!isMuted && !onPressed);
            ImGui.Button("Turn ON", size2);
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