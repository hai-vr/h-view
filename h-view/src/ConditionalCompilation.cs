namespace Hai.HView;

// ReSharper disable UnusedAutoPropertyAccessor.Local
public static class ConditionalCompilation
{
// Use static bool properties to avoid triggering "Heuristically Unreachable Code" warnings in editor,
// but use constants in releases.
#if USE_CONSTS_IN_RELEASE
    public const bool IncludesOpenVR = _IncludesOpenVR;
    public const bool RegisterManifest = _RegisterManifest;
    public const bool IncludesSteamworks = _IncludesSteamworks;
    public const bool CookiesSupported = _CookiesSupported;
    public const bool EnableFakeVrcOsc = _EnableFakeVrcOsc;
#else
    public static bool IncludesOpenVR => _IncludesOpenVR;
    public static bool RegisterManifest => _RegisterManifest;
    public static bool IncludesSteamworks => _IncludesSteamworks;
    public static bool CookiesSupported => _CookiesSupported;
    public static bool EnableFakeVrcOsc => _EnableFakeVrcOsc;
    public static bool IncludesOCR => _IncludesOCR;
#endif
    
#if INCLUDES_OPENVR
    private const bool _IncludesOpenVR = true;
#else
    private const bool _IncludesOpenVR = false;
#endif
    
#if REGISTER_MANIFEST
    private const bool _RegisterManifest = true;
#else
    private const bool _RegisterManifest = false;
#endif
    
#if INCLUDES_STEAMWORKS
    private const bool _IncludesSteamworks = true;
#else
    private const bool _IncludesSteamworks = false;
#endif
    
#if COOKIES_SUPPORTED
    private const bool _CookiesSupported = true;
#else
    private const bool _CookiesSupported = false;
#endif
    
#if ENABLE_FAKE_VRC_OSC
    private const bool _EnableFakeVrcOsc = true;
#else
    private const bool _EnableFakeVrcOsc = false;
#endif
    
#if INCLUDES_OCR
    private const bool _IncludesOCR = true;
#else
    private const bool _IncludesOCR = false;
#endif
}