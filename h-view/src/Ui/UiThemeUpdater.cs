using System.Numerics;
using ImGuiNET;

namespace Hai.HView.Ui.MainApp;

public class UiThemeUpdater
{
    private readonly ImGuiCol[] _sampled = {
        ImGuiCol.Button,
        ImGuiCol.ButtonHovered,
        ImGuiCol.ButtonActive,
        ImGuiCol.Tab,
        ImGuiCol.TabHovered,
        ImGuiCol.TabSelected,
        ImGuiCol.CheckMark,
        ImGuiCol.FrameBg,
        ImGuiCol.FrameBgHovered,
        ImGuiCol.FrameBgActive
    };
    private readonly Dictionary<int, Vector4> _originalColors = new Dictionary<int, Vector4>();
    private bool _isOriginalSampled;
    private float _scrollbarSize;
    private float _scrollbarRounding;

    public void OverrideStyleWithTheme(Vector3 themeColor)
    {
        EnsureOriginalSampled();

        var style = ImGui.GetStyle();
        var themeWithAlpha = new Vector4(themeColor.X, themeColor.Y, themeColor.Z, UiColors.DEFAULT_ActiveButton.W);
        var blackSameAlpha = new Vector4(0, 0, 0, UiColors.DEFAULT_ActiveButton.W);
        var buttonBg = Vector4.Lerp(themeWithAlpha, blackSameAlpha, 0.5f);
        var buttonBgHovered = Vector4.Lerp(themeWithAlpha, blackSameAlpha, 0.25f);
        var buttonBgActive = Vector4.Lerp(themeWithAlpha, blackSameAlpha, 0.1f);
        
        style.Colors[(int)ImGuiCol.Button] = buttonBg;
        style.Colors[(int)ImGuiCol.ButtonHovered] = buttonBgHovered;
        style.Colors[(int)ImGuiCol.ButtonActive] = buttonBgActive;
        style.Colors[(int)ImGuiCol.Tab] = buttonBg;
        style.Colors[(int)ImGuiCol.TabHovered] = buttonBgHovered;
        style.Colors[(int)ImGuiCol.TabSelected] = buttonBgActive;
        style.Colors[(int)ImGuiCol.CheckMark] = themeWithAlpha;
        style.Colors[(int)ImGuiCol.FrameBg] = Vector4.Lerp(themeWithAlpha, blackSameAlpha, 0.6f);
        style.Colors[(int)ImGuiCol.FrameBgHovered] = Vector4.Lerp(themeWithAlpha, blackSameAlpha, 0.4f);
        style.Colors[(int)ImGuiCol.FrameBgActive] = Vector4.Lerp(themeWithAlpha, blackSameAlpha, 0.25f);
    }

    public void Reset()
    {
        EnsureOriginalSampled();
        
        var target = ImGui.GetStyle().Colors;
        foreach (var col in _sampled)
        {
            target[(int)col] = _originalColors[(int)col];
        }
    }

    public void ApplyStyleAdjustments(bool isWindowlessStyle)
    {
        EnsureOriginalSampled();
        
        var style = ImGui.GetStyle();
        if (isWindowlessStyle)
        {
            style.ScrollbarSize = 40f;
            style.ScrollbarRounding = 2f;
        }
        else
        {
            style.ScrollbarSize = _scrollbarSize;
            style.ScrollbarRounding = _scrollbarRounding;
        }
    }

    private void EnsureOriginalSampled()
    {
        if (_isOriginalSampled) return;
        _isOriginalSampled = true;

        var style = ImGui.GetStyle();
        _scrollbarSize = style.ScrollbarSize;
        _scrollbarRounding = style.ScrollbarRounding;
        
        var sourceColors = style.Colors;
        foreach (var col in _sampled)
        {
            _originalColors[(int)col] = sourceColors[(int)col];
        }
    }
}