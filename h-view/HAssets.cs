using System.Reflection;

namespace Hai.HView;

public static class HAssets
{
    private static readonly string _directoryName;
    
    // Text
    public static readonly HAsset ThirdParty = new("THIRDPARTY.md");

    // UI
    public static readonly HAsset K14JaFont = new("HAssets/fonts/JF-Dot-K14.ttf"); // https://devforum.play.date/t/japanese-pixel-fonts-with-kanji-support/1807
    public static readonly HAsset FredokaEnFont = new("HAssets/fonts/Fredoka-Regular.ttf");
    public static readonly HAsset KiwiMaruJaFont = new("HAssets/fonts/KiwiMaru-Medium.ttf");
    public static readonly HAsset ClickAudio = new("HAssets/audio/click.wav");
    
    // OpenVR
    public static readonly HAsset ApplicationManifest = new("HAssets/openvr/manifest.vrmanifest");
    public static readonly HAsset ActionManifest = new("HAssets/openvr/h_view_actions.json");
    
    // Overlays
    public static readonly HAsset DashboardThumbnail = new("HAssets/img/DashboardThumb.png"); // Also used in README
    public static readonly HAsset EyeTrackingCursor = new("HAssets/img/EyeTrackingCursor.png");

    static HAssets()
    {
        // https://stackoverflow.com/questions/837488/how-can-i-get-the-applications-path-in-a-net-console-application
        _directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    public static string MakeAbsoluteToApplicationPath(string relativeAssetPath)
    {
        return Path.Combine(_directoryName, relativeAssetPath);
    }
}

public class HAsset
{
    private readonly string _relativePath;

    public HAsset(string relativePath)
    {
        _relativePath = relativePath;
    }

    public string Absolute()
    {
        return HAssets.MakeAbsoluteToApplicationPath(_relativePath);
    }

    public string Relative()
    {
        return _relativePath;
    }
}