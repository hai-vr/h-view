using System.Reflection;

namespace Hai.HView;

public static class HAssets
{
    private static readonly string _directoryName;
    
    // Text
    public static readonly HAsset ThirdParty = new("THIRDPARTY.md");
    public static readonly HAsset ThirdPartyLookup = new("HThirdParty/thirdparty-lookup.json");

    // UI
    public static readonly HAsset K14JaFont = new("HAssets/fonts/JF-Dot-K14.ttf"); // https://devforum.play.date/t/japanese-pixel-fonts-with-kanji-support/1807
    public static readonly HAsset FredokaEnFont = new("HAssets/fonts/Fredoka-Regular.ttf");
    public static readonly HAsset KiwiMaruJaFont = new("HAssets/fonts/KiwiMaru-Medium.ttf");
    public static readonly HAsset ClickAudio = new("HAssets/audio/click.wav");
    
    // OpenVR
    // 2024-10: I was not able to make the Action manifest work when placed in HAssets/openvr/, so I put both
    // the Application manifest and Action manifest files at the root. Maybe this could be revisited at a later time.
    // Putting the asset at another location would switch the bindings to legacy mode.
    public static readonly HAsset ApplicationManifest = new("manifest.vrmanifest");
    public static readonly HAsset ActionManifest = new("h_view_actions.json");
    
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

    public static HAsset HThirdPartyLicense(string fullLicenseTextFile)
    {
        return new HAsset($"HThirdParty/THIRDPARTY-LICENSES/{fullLicenseTextFile}");
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