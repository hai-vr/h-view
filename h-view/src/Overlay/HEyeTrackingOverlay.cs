using  System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using Valve.VR;

namespace Hai.HView.Overlay;

public class HEyeTrackingOverlay : IOverlayable
{
    private readonly HVRoutine _routine;
    private readonly HVImGuiOverlay _dashboard;
    private HHandOverlay _handOverlayNullable;
    private const string Name = "eyetracking";

    private ulong _handle;
    private HVPoseData _poseData;
    
    private HmdMatrix34_t _overlayPlace;
    private Vector3 _eyePos = Vector3.Zero;
    private Quaternion _eyeGaze = Quaternion.Identity;

    public Vector3 EyePos => _eyePos;
    public Quaternion EyeGaze => _eyeGaze;

    public HEyeTrackingOverlay(HVRoutine routine, HVImGuiOverlay dashboard)
    {
        _routine = routine;
        _dashboard = dashboard;
    }

    public void Start()
    {
        OpenVR.Overlay.CreateOverlay($"{HVImGuiOverlay.OverlayKey}-{Name}", HVApp.AppTitle, ref _handle);
        OpenVR.Overlay.SetOverlayFromFile(_handle, HAssets.EyeTrackingCursor.Absolute());
        OpenVR.Overlay.SetOverlayAlpha(_handle, 1f);
        OpenVR.Overlay.SetOverlayColor(_handle, 1f, 1f, 1f);
        OpenVR.Overlay.SetOverlayWidthInMeters(_handle, 0.05f);
        OpenVR.Overlay.ShowOverlay(_handle);
    }

    public void ProvidePoseData(HVPoseData poseData)
    {
        _poseData = poseData;
        
        var eyeTracking = _routine.EyeTracking;
        var xx = (float)(Math.Asin(eyeTracking.XAvg) * (180 / Math.PI));
        var yy = (float)(Math.Asin(-eyeTracking.Y) * (180 / Math.PI));
        var eyeRot = HVGeofunctions.QuaternionFromAngles(new Vector3(yy, xx, 0), HVRotationMulOrder.YZX);

        var absToHead = HVOvrGeofunctions.OvrToOvrnum(_poseData.Poses[0].mDeviceToAbsoluteTracking);
        var headToEyeTrackingFocus = HVGeofunctions.TR(new Vector3(0, 0, 0), eyeRot);
        var move = HVGeofunctions.TR(new Vector3(0, 0, -1), Quaternion.Identity);
        
        _overlayPlace = HVOvrGeofunctions.OvrnumToOvr(absToHead * headToEyeTrackingFocus * move);
        HVGeofunctions.ToPosRotV3(absToHead * headToEyeTrackingFocus, out _eyePos, out _eyeGaze);

        _dashboard.ProvideEyeTracking(_eyePos, _eyeGaze);
        _handOverlayNullable?.ProvideEyeTracking(_eyePos, _eyeGaze);
    }

    public void ProcessThatOverlay(Stopwatch stopwatch)
    {
        OpenVR.Overlay.SetOverlayTransformAbsolute(_handle, OpenVR.Compositor.GetTrackingSpace(), ref _overlayPlace);
    }

    public void Teardown()
    {
        OpenVR.Overlay.DestroyOverlay(_handle);
    }

    public void SetHandOverlay(HHandOverlay handOverlayIncludingNull)
    {
        _handOverlayNullable = handOverlayIncludingNull;
    }
}