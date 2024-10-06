using System.Numerics;
using ImGuiNET;

namespace Hai.HView.Ui;

public static class UiColors
{
    public static readonly Vector4 ErroringRed = new(1f, 0f, 0f, 1f);
    public static readonly Vector4 UnusedParameterColor = new(1, 0, 0, 0.75f);
    public static readonly Vector4 EnabledButtonTransparentCyan = new(0, 1, 1, 0.75f);
    public static readonly Vector4 FakeButtonBlackInvisible = new(0, 0, 0, 0);
    public static readonly Vector4 UnhandledOscCompositeTypeDarkRed = new(0.75f, 0.5f, 0.5f, 1);
    public static readonly Vector4 HardwareIsWorkingLighthouse = new(0.627451F, 0.627451F, 0.627451F, 1);
    public static readonly Vector4 HardwareLostVeryDarkGray = new(0.3764706F, 0.3764706F, 0.3764706F, 1);
    public static readonly Vector4 TrackingLostYellow = new(1, 1, 0, 1);
    public static readonly Vector4 TrackingRecoveredGreen = new(0f, 1f, 0f, 1f);
    public static readonly Vector4 RegularWhite = new(1f, 1f, 1f, 1f);

    public static bool ColoredBackground(bool useColor, Func<bool> func)
    {
        if (useColor) ImGui.PushStyleColor(ImGuiCol.Button, EnabledButtonTransparentCyan);
        var result = func.Invoke();
        if (useColor) ImGui.PopStyleColor();
        
        return result;
    }

    public static bool Colored(bool useColor, ImGuiCol element, Func<bool> func)
    {
        if (useColor) ImGui.PushStyleColor(element, EnabledButtonTransparentCyan);
        var result = func.Invoke();
        if (useColor) ImGui.PopStyleColor();
        
        return result;
    }

    public static void Colored(bool useColor, ImGuiCol element, Action action)
    {
        if (useColor) ImGui.PushStyleColor(element, EnabledButtonTransparentCyan);
        action.Invoke();
        if (useColor) ImGui.PopStyleColor();
    }
}