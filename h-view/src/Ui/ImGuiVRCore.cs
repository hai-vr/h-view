﻿using System.Numerics;
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