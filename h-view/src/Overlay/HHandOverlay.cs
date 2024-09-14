using System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using Hai.HView.Gui;
using Valve.VR;

namespace Hai.HView.Overlay;

public class HHandOverlay : IOverlayable
{
    private readonly HVRoutine _routine;
    private readonly HVOverlayInstance _overlay;
    private readonly Stopwatch _stopwatch;
    private readonly bool _useLeftHand;

    public HHandOverlay(HVInnerWindow innerWindow, float windowRatio, HVRoutine routine, bool useLeftHand)
    {
        _routine = routine;
        _useLeftHand = useLeftHand;
        _overlay = new HVOverlayInstance(innerWindow, "costumes", false, windowRatio);
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
        if (!HVOverlayMovement.IsValidDeviceIndex(deviceIndex)) return;
        
        var pos = new Vector3(0f, -0.01f, -0.2f);
        var angles = new Vector3(65, 0, 0);
        var rot = HVGeofunctions.QuaternionFromAngles(angles, HVRotationMulOrder.YZX);
        var handToOverlayPlace = HVOvrGeofunctions.OvrToOvrnum(HVOvrGeofunctions.OvrTRS(pos, rot, Vector3.One));
                    
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
        if (HVOverlayMovement.IsValidDeviceIndex(whichHand))
        {
            // var isIntersecting = IsIntersecting(poseData, whichHand);
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

    private bool IsIntersecting(HVPoseData poseData, uint whichHand)
    {
        // FIXME: None of this works, unsure why. Intersection is offset, or has wrong angle or something. It feels squished vertically.
        
        var handPose = poseData.Poses[whichHand].mDeviceToAbsoluteTracking;
        var move = new Vector3(0, -0.2f, 0);
        var absToOverHand = HVOvrGeofunctions.OvrToOvrnum(HVOvrGeofunctions.OvrTranslate(move)) * HVOvrGeofunctions.OvrToOvrnum(handPose);
        var pos = HVOvrGeofunctions.PosOvr(HVOvrGeofunctions.OvrnumToOvr(absToOverHand));
        var intersection = new VROverlayIntersectionParams_t
        {
            eOrigin = OpenVR.Compositor.GetTrackingSpace(),
            vSource = pos,
            vDirection = HVOvrGeofunctions.ForwardOvr(handPose)
        };
            
        VROverlayIntersectionResults_t results = default;
        var isIntersecting = OpenVR.Overlay.ComputeOverlayIntersection(_overlay.GetOverlayHandle(), ref intersection, ref results);
            
        // HACK: somehow ComputeOverlayIntersection doesn't return false when it's not intersecting
        if (results.vPoint is { v0: 0, v1: 0, v2: 0 }) isIntersecting = false;
        Console.WriteLine($"IsIntersecting? {isIntersecting} {results.vPoint.v0} {results.vPoint.v1} {results.vPoint.v2}");
        return isIntersecting;
    }

    public void ProcessThatOverlay(Stopwatch stopwatch)
    {
        _overlay.ProcessThatOverlay(stopwatch);
        OpenVR.Overlay.SetOverlayAlpha(_overlay.GetOverlayHandle(), (float)Math.Sqrt(1 - _stopwatch.ElapsedMilliseconds / 1000f));
        if (_stopwatch.ElapsedMilliseconds > 1000)
        {
            _routine.EjectUserFromCostumeMenu();
            
            // We eject and also hide, because we want this to work even when VRC is not running.
            _routine.HideCostumes();
        }
    }

    public void ProvideEyeTracking(Vector3 eyePos, Quaternion eyeGaze)
    {
        _overlay.ProvideEyeTracking(eyePos, eyeGaze);
        OpenVR.Overlay.SetOverlayWidthInMeters(_overlay.GetOverlayHandle(), 0.5f);
    }
}