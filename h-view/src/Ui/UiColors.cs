using System.Numerics;
using Hai.HView.Data;
using ImGuiNET;

namespace Hai.HView.Ui;

public static class UiColors
{
    private static Vector4 AccessibilityOrange = new(1, 0.48f, 0, 1);
    private static Vector4 AccessibilityBlue = new(0.27f, 0.79f, 1f, 1f);
    
    public static readonly Vector4 DEFAULT_TrackingLost = AccessibilityOrange;
    public static readonly Vector4 DEFAULT_TrackingRecovered = AccessibilityBlue;
    public static readonly Vector4 DEFAULT_ActiveButton = new(0.9764706F, 0.078431375F, 0.6901961F, 0.75f);
    public static readonly Vector4 DEFAULT_StaleParameter = AccessibilityOrange;
    public static readonly Vector4 DEFAULT_SecondaryTheme = new(0, 0, 0, 0.75f);

    public static readonly Vector4 ErroringRed = new(1f, 0f, 0f, 1f);
    public static Vector4 StaleParameter => ConfigOr(_config.colorStaleParameter, DEFAULT_StaleParameter);
    public static Vector4 ActiveButton => ConfigOr(_config.colorActiveButton, DEFAULT_ActiveButton);
    public static Vector4 SecondaryTheme => ConfigOr(_config.colorSecondaryTheme, DEFAULT_SecondaryTheme);
    public static readonly Vector4 FakeButtonBlackInvisible = new(0, 0, 0, 0);
    public static readonly Vector4 UnhandledOscCompositeTypeDarkRed = new(0.75f, 0.5f, 0.5f, 1);
    public static readonly Vector4 HardwareIsWorkingLighthouse = new(0.627451F, 0.627451F, 0.627451F, 1);
    public static readonly Vector4 HardwareLostVeryDarkGray = new(0.3764706F, 0.3764706F, 0.3764706F, 1);
    public static Vector4 TrackingLost => ConfigOr(_config.colorTrackingLost, DEFAULT_TrackingLost);
    public static Vector4 TrackingRecovered => ConfigOr(_config.colorTrackingRecovered, DEFAULT_TrackingRecovered);
    public static readonly Vector4 RegularWhite = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 IsDefaultGray = new(0.627451F, 0.627451F, 0.627451F, 1);
    
    private static SavedData _config; // Not a fan of this global state, but it makes referencing colors easier

    public static void ProvideConfig(SavedData data)
    {
        _config = data;
    }

    private static Vector4 ConfigOr(SavedData.ColorReplacement replacement, Vector4 defaultValue)
    {
        if (replacement.use) return V4(replacement.color, defaultValue.W);
        
        return defaultValue;
    }

    public static Vector3 V3(Vector4 v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    } 

    private static Vector4 V4(Vector3 v, float alpha)
    {
        return new Vector4(v.X, v.Y, v.Z, alpha);
    }

    public static bool ColoredBackground(bool useColor, Func<bool> func)
    {
        if (useColor) ImGui.PushStyleColor(ImGuiCol.Button, ActiveButton);
        var result = func.Invoke();
        if (useColor) ImGui.PopStyleColor();
        
        return result;
    }

    public static bool Colored(bool useColor, ImGuiCol element, Func<bool> func)
    {
        if (useColor) ImGui.PushStyleColor(element, ActiveButton);
        var result = func.Invoke();
        if (useColor) ImGui.PopStyleColor();
        
        return result;
    }

    public static void Colored(bool useColor, ImGuiCol element, Action action)
    {
        if (useColor) ImGui.PushStyleColor(element, ActiveButton);
        action.Invoke();
        if (useColor) ImGui.PopStyleColor();
    }

    public static void Colored(bool useColor, ImGuiCol element, Vector4 color, Action action)
    {
        if (useColor) ImGui.PushStyleColor(element, color);
        action.Invoke();
        if (useColor) ImGui.PopStyleColor();
    }
}