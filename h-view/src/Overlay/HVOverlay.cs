using System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using Hai.HView.Gui;
using Valve.VR;
using Veldrid;

namespace Hai.HView.Overlay;

public class HVOverlay
{
    private readonly uint _sizeOfVrEvent = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t));
    
    // Specific to this overlay window
    private const string OverlayKey = "hview-overlay";
    private readonly HVInnerWindow _innerWindow;
    private readonly HOverlayInputSnapshot _inputSnapshot;
    private readonly HVOverlayMovement _movement;
    
    private ulong _handle;
    private Texture_t _vrTexture;
    
    // Overlay management
    private readonly HVPoseData _mgtPoseData = new HVPoseData
    {
        Poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount]
    };
    private bool _exitRequested;

    public HVOverlay(HVInnerWindow innerWindow)
    {
        _innerWindow = innerWindow;
        _inputSnapshot = new HOverlayInputSnapshot();
        _movement = new HVOverlayMovement();
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
        OpenVR.Overlay.CreateOverlay(OverlayKey, HVApp.AppTitle, ref _handle);
        ApplyOverlayProps();

        _vrTexture = new Texture_t
        {
            handle = _innerWindow.GetOverlayTexturePointer(),
            eType = ETextureType.DirectX,
            eColorSpace = EColorSpace.Auto
        };
        
        var stopwatch = new Stopwatch();
        while (!_exitRequested) // TODO: Nothing changes the state of _exitRequested
        {
            ProcessOverlayManagement();
            ProcessThatOverlay(stopwatch);
            if (OpenVR.Applications.GetSceneApplicationState() == EVRSceneApplicationState.Running)
            {
                OpenVR.Overlay.WaitFrameSync(1000 / 30);
            }
            else
            {
                Thread.Sleep(1000 / 60);
            }
        }

        OpenVR.Overlay.DestroyOverlay(_handle);
        
        OpenVR.Shutdown();
    }

    private void ApplyOverlayProps()
    {
        var bounds = new VRTextureBounds_t
        {
            uMin = 0, uMax = 1,
            vMin = 0, vMax = 1
        };
        OpenVR.Overlay.SetOverlayTextureBounds(_handle, ref bounds);
        OpenVR.Overlay.SetOverlayWidthInMeters(_handle, 0.35f);
        OpenVR.Overlay.SetOverlayAlpha(_handle, 1f);
        OpenVR.Overlay.SetOverlayColor(_handle, 1f, 1f, 1f);
        OpenVR.Overlay.SetOverlayInputMethod(_handle, VROverlayInputMethod.Mouse);
        OpenVR.Overlay.SetOverlayFlag(_handle, VROverlayFlags.SendVRSmoothScrollEvents, true);
        OpenVR.Overlay.SetOverlayCurvature(_handle, 0.2f);
        
        OpenVR.Overlay.ShowOverlay(_handle);
    }

    private void ProcessOverlayManagement()
    {
        // TODO: These are related to the management of overlays (plural) in general.
        // Eventually we'll have to separate the concept of managing overlays, and managing that specific overlay bound to that ImGui window.
        OverlayManagementPollVREvents();
        OverlayManagementFillPose();
    }

    private void ProcessThatOverlay(Stopwatch stopwatch)
    {
#if HV_DEBUG
        // In HV_DEBUG mode (applied when the config is a debug build), apply the overlay properties all the time for live editing.
        // Comment this out if not needed in debug mode.
        ApplyOverlayProps();
#endif
        _movement.Evaluate(_handle, _mgtPoseData);
        
        // FIXME: For now, this prevents the window (that we're not even using) from freezing.
        // Now that the window is hidden, maybe this is no longer necessary? Not sure, if the task manager or other
        // system relies on us pulling those events.
        var snapshot = _innerWindow.DoPumpEvents();

        _inputSnapshot.Deaccumulate();
        _inputSnapshot.SetWindowSize(_innerWindow.WindowSize());
        PollOverlayEvents();
        // TODO: Open the VR keyboard whenever a text field in ImGui asks for input capture.
        // TODO: Figure out how to make third-party keyboard apps like XSOverlay still able to write text into our windowless instance.
        
        _innerWindow.UpdateAndRender(stopwatch, _inputSnapshot);
        
        OpenVR.Overlay.SetOverlayTexture(_handle, ref _vrTexture);
        
        // TODO: Proper frame sync
        // FIXME: This seems to cause a slow, 1 second overlay texture refresh rate when there is no main application running.
        // OpenVR.Overlay.WaitFrameSync(1000 / 30);
        
        // FIXME: We don't want to have unlimited refresh rate for that overlay, add some throttling (see above).
    }

    private void OverlayManagementFillPose()
    {
        // Fill the pose data
            
        // TODO: Proper predicted seconds info
        var fPredictedSecondsToPhotonsFromNow = 0f;
        // TODO: Proper tracking universe
        OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, fPredictedSecondsToPhotonsFromNow, _mgtPoseData.Poses);
        _mgtPoseData.LeftHandDeviceIndex = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
        _mgtPoseData.RightHandDeviceIndex = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
    }

    private void OverlayManagementPollVREvents()
    {
        VREvent_t evt = default;
        while (OpenVR.System.PollNextEvent(ref evt, _sizeOfVrEvent))
        {
            var type = (EVREventType)evt.eventType;
            
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (type)
            {
                case EVREventType.VREvent_Quit:
                {
                    _exitRequested = true;
                    return; // Don't bother processing more events.
                }
            }
        }
    }

    private void PollOverlayEvents()
    {
        VREvent_t evt = default;
        
        // TODO: Keyboard events
        while (OpenVR.Overlay.PollNextOverlayEvent(_handle, ref evt, _sizeOfVrEvent))
        {
            // https://github.com/ValveSoftware/openvr/blob/master/headers/openvr.h#L773
            var type = (EVREventType)evt.eventType;
            
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (type)
            {
                case EVREventType.VREvent_MouseMove:
                {
                    var data = evt.data.mouse;
                    _inputSnapshot.MouseMove(new Vector2(data.x, 1 - data.y));
                    break;
                }
                case EVREventType.VREvent_ScrollDiscrete:
                {
                    var data = evt.data.scroll;
                    _inputSnapshot.Scrolling(data.ydelta);
                    break;
                }
                case EVREventType.VREvent_ScrollSmooth:
                {
                    var data = evt.data.scroll;
                    _inputSnapshot.Scrolling(data.ydelta);
                    break;
                }
                case EVREventType.VREvent_MouseButtonDown:
                {
                    if (TryAsVeldrid(evt.data.mouse.button, out var veldridButton)) _inputSnapshot.MouseDown(veldridButton);
                    break;
                }
                case EVREventType.VREvent_MouseButtonUp:
                {
                    if (TryAsVeldrid(evt.data.mouse.button, out var veldridButton)) _inputSnapshot.MouseUp(veldridButton);
                    break;
                }
            }
        }
    }

    private bool TryAsVeldrid(uint vrButton, out MouseButton veldridButton)
    {
        // EVRMouseButton
        switch (vrButton)
        {
            case 1:
                veldridButton = MouseButton.Left;
                return true;
            case 2:
                veldridButton = MouseButton.Right;
                return true;
            case 3:
                veldridButton = MouseButton.Middle;
                return true;
            default:
                veldridButton = MouseButton.LastButton;
                return false;
        }
    }
}
