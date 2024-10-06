using System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using Hai.HView.Gui;
using Hai.HView.OVR;
using Valve.VR;

namespace Hai.HView.Overlay;

public class HHandOverlay : IOverlayable
{
    private readonly HVRoutine _routine;
    private readonly HVImGuiOverlay _overlay;
    private readonly Stopwatch _stopwatch;
    private readonly bool _useLeftHand;

    public HHandOverlay(HVImGuiManagement imGuiManagement, HVInnerWindow innerWindow, float windowRatio, HVRoutine routine, bool useLeftHand)
    {
        _routine = routine;
        _useLeftHand = useLeftHand;
        _overlay = new HVImGuiOverlay(imGuiManagement, innerWindow, "costumes", false, windowRatio);
        _stopwatch = new Stopwatch();
    }

    public void Start()
    {
        _overlay.Start();
        _stopwatch.Start();
    }

    public void MoveToInitialPosition(HVPoseData poseData)
    {
        var deviceIndex = GetHandedDeviceIndex(poseData);
        if (!OpenVRUtils.IsValidDeviceIndex(deviceIndex)) return;
        
        var pos = new Vector3(0f, -0.01f, -0.2f);
        var angles = new Vector3(65, 0, 0);
        var rot = HVGeofunctions.QuaternionFromAngles(angles, HVRotationMulOrder.YZX);
        var handToOverlayPlace = HVGeofunctions.TR(pos, rot);
                    
        var absToHandPlace = HVOvrGeofunctions.OvrToOvrnum(poseData.Poses[deviceIndex].mDeviceToAbsoluteTracking);
        var absToOverlayPlace = HVOvrGeofunctions.OvrnumToOvr(absToHandPlace * handToOverlayPlace);

        OpenVR.Overlay.SetOverlayTransformAbsolute(_overlay.GetOverlayHandle(), OpenVR.Compositor.GetTrackingSpace(), ref absToOverlayPlace);
        OpenVR.Overlay.SetOverlayFlag(_overlay.GetOverlayHandle(), VROverlayFlags.MakeOverlaysInteractiveIfVisible, true);
    }

    public void Teardown()
    {
        OpenVR.Overlay.SetOverlayFlag(_overlay.GetOverlayHandle(), VROverlayFlags.MakeOverlaysInteractiveIfVisible, false);
        _overlay.Teardown();
    }

    public void ProvidePoseData(HVPoseData poseData)
    {
        _overlay.ProvidePoseData(poseData);
        HandleIntersection(poseData);
    }

    private void HandleIntersection(HVPoseData poseData)
    {
        var whichHand = GetHandedDeviceIndex(poseData);
        if (OpenVRUtils.IsValidDeviceIndex(whichHand))
        {
            // var isIntersecting = IsHandLaserIntersecting(poseData, whichHand);
            // OpenVR.Overlay.SetOverlayFlag(_overlay.GetOverlayHandle(), VROverlayFlags.MakeOverlaysInteractiveIfVisible, isIntersecting);
            var isIntersecting = OpenVR.Overlay.IsHoverTargetOverlay(_overlay.GetOverlayHandle());
            if (isIntersecting)
            {
                _stopwatch.Restart();
            }
        }
    }

    private uint GetHandedDeviceIndex(HVPoseData poseData)
    {
        return _useLeftHand ? poseData.LeftHandDeviceIndex : poseData.RightHandDeviceIndex;
    }

    private bool IsHandLaserIntersecting(HVPoseData poseData, uint whichHand)
    {
        // FIXME: ~~None of this works, unsure why. Intersection is offset, or has wrong angle or something. It feels squished vertically.~~
        // FIXME: After working on another part of the program, check out HEyeTrackingOverlay.cs,
        // as it might be that the quaternion of the hand pose (or the matrix itself) needs to be inverted before being passed to the intersection params.
        // FIXME: It still doesn't seem to work properly, probably the laser angle or origin point needs to be sourced from one of the controller poses.
        
        var handPose = poseData.Poses[whichHand].mDeviceToAbsoluteTracking;
        HVGeofunctions.ToPosRotV3(HVOvrGeofunctions.OvrToOvrnum(handPose), out var pos, out var rot);
        var intersection = new VROverlayIntersectionParams_t
        {
            eOrigin = OpenVR.Compositor.GetTrackingSpace(),
            vSource = HVOvrGeofunctions.Vec(pos),
            vDirection = HVOvrGeofunctions.Vec(Vector3.Transform(new Vector3(0, 0, -1), Quaternion.Inverse(rot)))
        };
        return OpenVRUtils.ComputeOverlayIntersectionStrictUVs(_overlay.GetOverlayHandle(), intersection, out _);
    }

    public void ProcessThatOverlay(Stopwatch stopwatch)
    {
        _overlay.ProcessThatOverlay(stopwatch);
        OpenVR.Overlay.SetOverlayAlpha(_overlay.GetOverlayHandle(), (float)Math.Sqrt(1 - _stopwatch.ElapsedMilliseconds / 1000f));
        if (_stopwatch.ElapsedMilliseconds > 1000)
        {
            _routine.EjectUserFromCostumeMenu();
            
            // We eject and also hide, because we want this to work even when VRC is not running,
            // and also when VRC is going into a loading screen.
            _routine.HideCostumes();
        }
    }

    public void ProvideEyeTracking(Vector3 eyePos, Quaternion eyeGaze)
    {
        _overlay.ProvideEyeTracking(eyePos, eyeGaze);
        OpenVR.Overlay.SetOverlayWidthInMeters(_overlay.GetOverlayHandle(), 0.5f);
    }
}