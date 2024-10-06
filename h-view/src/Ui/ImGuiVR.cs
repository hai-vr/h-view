using System.Numerics;
using ImGuiNET;

namespace Hai.HView.Ui;

public class ImGuiVR
{
    public event ButtonEvent OnHoverChanged;
    public event ButtonEvent OnButtonPressed;
    public delegate void ButtonEvent();
    
    private readonly bool _isWindowlessStyle;
    private string _hovered;
    private string _prevHover;
    
    public void Begin()
    {
        _prevHover = _hovered;
    }

    public void End()
    {
        if (_isWindowlessStyle && _hovered != _prevHover)
        {
            OnHoverChanged?.Invoke();
        }
    }

    public ImGuiVR(bool isWindowlessStyle)
    {
        _isWindowlessStyle = isWindowlessStyle;
    }

    public bool HapticButton(string label)
    {
        var clicked = ImGui.Button(label);
        CheckHapticButton(label);
        return clicked;
    }

    public bool HapticImageButton(string label, IntPtr textureId, Vector2 size)
    {
        var clicked = ImGui.ImageButton(label, textureId, size);
        CheckHapticButton(label);
        return clicked;
    }

    public bool HapticButton(string label, Vector2 size)
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
}