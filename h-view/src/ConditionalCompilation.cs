namespace Hai.HView;

// ReSharper disable UnusedAutoPropertyAccessor.Local
public static class ConditionalCompilation
{
    public static bool IncludesOpenVR { get; private set; }
    public static bool RegisterManifest { get; private set; }
    public static bool IncludesSteamworks { get; private set; }
    public static bool CookiesSupported { get; private set; }
    
    static ConditionalCompilation()
    {
#if INCLUDES_OPENVR
        IncludesOpenVR = true;
#endif
#if REGISTER_MANIFEST
        RegisterManifest = true;
#endif
#if INCLUDES_STEAMWORKS
        IncludesSteamworks = true;
#endif
#if COOKIES_SUPPORTED
        CookiesSupported = true;
#endif
    }
}