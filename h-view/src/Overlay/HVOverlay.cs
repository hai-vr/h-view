using System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using Hai.HView.Gui;
using Valve.VR;
using Veldrid;

namespace Hai.HView.Overlay;

public class HVOverlay : InputSnapshot
{
    private const string OverlayKey = "hview-overlay";
    private HmdMatrix34_t _identity = new HmdMatrix34_t {
        m0 = 1f, m1 = 0f, m2 = 0f, m3 = 0f,
        m4 = 0f, m5 = 1f, m6 = 0f, m7 = 0f,
        m8 = 0f, m9 = 0f, m10 = 1f, m11 = 0f
    };
    
    private readonly HVInnerWindow _innerWindow;
    private ulong _handle;
    private Texture_t _vrTexture;
    
    private bool _exitRequested;

    public HVOverlay(HVInnerWindow innerWindow)
    {
        _innerWindow = innerWindow;
    }

    public bool Start()
    {
        EVRInitError _err = EVRInitError.None;
        do
        {
            OpenVR.Init(ref _err, EVRApplicationType.VRApplication_Background);
            if (_err != EVRInitError.None)
            {
                Console.WriteLine($"Application could not start. Is SteamVR running?");
                Console.WriteLine($"        SteamVR error: {_err}");
                if (_exitRequested)
                {
                    Console.WriteLine($"Exiting application per user request");
                    return false;
                }

                Console.WriteLine($"Application will try again in 5 seconds...");
                Thread.Sleep(5000);
            }
        } while (_err != EVRInitError.None);

        return true;
    }

    public void Run()
    {
        var bounds = new VRTextureBounds_t
        {
            uMin = 0, uMax = 1,
            vMin = 0, vMax = 1
        };
        
        OpenVR.Overlay.CreateOverlay(OverlayKey, HVApp.AppTitle, ref _handle);
        OpenVR.Overlay.SetOverlayTextureBounds(_handle, ref bounds);
        OpenVR.Overlay.SetOverlayWidthInMeters(_handle, 1f);
        OpenVR.Overlay.SetOverlayAlpha(_handle, 1f);
        OpenVR.Overlay.SetOverlayColor(_handle, 1f, 1f, 1f);
        OpenVR.Overlay.SetOverlayInputMethod(_handle, VROverlayInputMethod.None); // TODO: Overlay inputs (poll those events!)
        OpenVR.Overlay.SetOverlayTransformAbsolute(_handle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref _identity);

        _vrTexture = new Texture_t
        {
            handle = _innerWindow.GetOverlayTexturePointer(),
            eType = ETextureType.DirectX,
            eColorSpace = EColorSpace.Auto
        };
        
        var stopwatch = new Stopwatch();
        while (!_exitRequested) // TODO: Nothing changes the state of _exitRequested
        {
            Iterate(stopwatch);
        }

        OpenVR.Overlay.DestroyOverlay(_handle);

        // TODO: We have to clean our overlays when the app shuts down
        // TODO: We have to listen to OpenVR's own shutting down event
    }

    private void Iterate(Stopwatch stopwatch)
    {
        // FIXME: For now, this prevents the window (that we're not even using) from freezing.
        // Now that the window is hidden, maybe this is no longer necessary? Not sure, if the task manager or other
        // system relies on us pulling those events.
        var snapshot = _innerWindow.DoPumpEvents();
        
        _innerWindow.UpdateAndRender(stopwatch, snapshot);
        
        OpenVR.Overlay.SetOverlayTexture(_handle, ref _vrTexture);
        OpenVR.Overlay.ShowOverlay(_handle);
        
        // TODO: Proper frame sync
        OpenVR.Overlay.WaitFrameSync(1000 / 30);
    }

    // TODO: Overlay inputs (poll those events!)
    public bool IsMouseDown(MouseButton button) => false;
    public IReadOnlyList<KeyEvent> KeyEvents => new List<KeyEvent>();
    public IReadOnlyList<MouseEvent> MouseEvents => new List<MouseEvent>();
    public IReadOnlyList<char> KeyCharPresses => new List<char>();
    public Vector2 MousePosition => new Vector2(0, 0);
    public float WheelDelta => 0f;
}