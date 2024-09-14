using  System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using Valve.VR;

namespace Hai.HView.Overlay;

public class HEyeTrackingOverlay : IOverlayable
{
    private readonly HVRoutine _routine;
    private const string Name = "eyetracking";
    
    private ulong _handle;
    private HVPoseData _poseData;

    public HEyeTrackingOverlay(HVRoutine routine)
    {
        _routine = routine;
    }

    public void Start()
    {
        OpenVR.Overlay.CreateOverlay($"{HVOverlayInstance.OverlayKey}-{Name}", HVApp.AppTitle, ref _handle);
        OpenVR.Overlay.SetOverlayFromFile(_handle, Path.GetFullPath("DashboardThumb.png"));
        OpenVR.Overlay.SetOverlayAlpha(_handle, 1f);
        OpenVR.Overlay.SetOverlayColor(_handle, 1f, 1f, 1f);
        OpenVR.Overlay.SetOverlayWidthInMeters(_handle, 0.2f);
        OpenVR.Overlay.ShowOverlay(_handle);
    }

    public void ProvidePoseData(HVPoseData poseData)
    {
        _poseData = poseData;
    }

    public void ProcessThatOverlay(Stopwatch stopwatch)
    {
        var eyeTracking = _routine.EyeTracking;
        var xx = (float)(Math.Asin(eyeTracking.XAvg) * (180 / Math.PI));
        var yy = (float)(Math.Asin(-eyeTracking.Y) * (180 / Math.PI));
        var eyeRot = HVGeofunctions.QuaternionFromAngles(new Vector3(yy, xx, 0), HVRotationMulOrder.YZX);

        var absToHead = HVOvrGeofunctions.OvrToOvrnum(_poseData.Poses[0].mDeviceToAbsoluteTracking);
        var headToEyeTrackingFocus = HVOvrGeofunctions.OvrToOvrnum(HVOvrGeofunctions.OvrTR(new Vector3(0, 0, 0), eyeRot));
        var rot180 = HVOvrGeofunctions.OvrToOvrnum(HVOvrGeofunctions.OvrTR(new Vector3(0, 0, -10), Quaternion.Identity));
        
        var final = HVOvrGeofunctions.OvrnumToOvr(absToHead * headToEyeTrackingFocus * rot180);

        OpenVR.Overlay.SetOverlayTransformAbsolute(_handle, OpenVR.Compositor.GetTrackingSpace(), ref final);
    }

    public void Teardown()
    {
        OpenVR.Overlay.DestroyOverlay(_handle);
    }
}