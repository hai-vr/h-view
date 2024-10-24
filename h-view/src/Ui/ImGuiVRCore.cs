using System.Numerics;
using ImGuiNET;

namespace Hai.HView.Ui;

public class ImGuiVRCore
{
    public event ButtonEvent OnHoverChanged;
    public event ButtonEvent OnButtonPressed;
    public delegate void ButtonEvent();
    
    private string _hovered;
    private string _prevHover;
    
    public void Begin()
    {
        _prevHover = _hovered;
    }

    public void End()
    {
        if (_hovered != _prevHover)
        {
            OnHoverChanged?.Invoke();
        }
    }

    public bool HapticCheckbox(string label, ref bool value)
    {
        var clicked = ImGui.Checkbox(label, ref value);
        CheckHapticElement(label);
        return clicked;
    }

    public bool HapticButton(string label)
    {
        var clicked = ImGui.Button(label);
        CheckHapticElement(label);
        return clicked;
    }

    public bool HapticImageButton(string label, IntPtr textureId, Vector2 size)
    {
        var clicked = ImGui.ImageButton(label, textureId, size);
        CheckHapticElement(label);
        return clicked;
    }

    public bool HapticButton(string label, Vector2 size)
    {
        var clicked = ImGui.Button(label, size);
        CheckHapticElement(label);
        return clicked;
    }

    public T MakeAction<T>(string label, Func<T> operation)
    {
        var result = operation.Invoke();
        CheckHapticElement(label);
        return result;
    }

    private void CheckHapticElement(string label)
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