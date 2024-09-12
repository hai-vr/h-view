using System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using Hai.HView.Gui;
using Valve.VR;

namespace Hai.HView.Overlay;

public class HHandOverlay
{
    private readonly HVRoutine _routine;
    private readonly HVOverlayInstance _overlay;
    private readonly Stopwatch _stopwatch;

    public HHandOverlay(HVInnerWindow innerWindow, float windowRatio, HVRoutine routine)
    {
        _routine = routine;
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
        if (!HVOverlayMovement.IsValidDeviceIndex(poseData.RightHandDeviceIndex)) return;
        
        var pos = new Vector3(0f, -0.01f, -0.2f);
        var angles = new Vector3(65, 0, 0);
        var rot = HVGeofunctions.QuaternionFromAngles(angles, HVRotationMulOrder.YZX);
        var handToOverlayPlace = HVOvrGeofunctions.OvrToOvrnum(HVOvrGeofunctions.OvrTRS(pos, rot, Vector3.One));
                    
        var absToHandPlace = HVOvrGeofunctions.OvrToOvrnum(poseData.Poses[poseData.RightHandDeviceIndex].mDeviceToAbsoluteTracking);
        var absToOverlayPlace = HVOvrGeofunctions.OvrnumToOvr(absToHandPlace * handToOverlayPlace);

        OpenVR.Overlay.SetOverlayTransformAbsolute(_overlay.GetOverlayHandle(), ETrackingUniverseOrigin.TrackingUniverseStanding, ref absToOverlayPlace);
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
        var rightHand = poseData.RightHandDeviceIndex;
        if (HVOverlayMovement.IsValidDeviceIndex(rightHand))
        {
            // var isIntersecting = IsIntersecting(poseData, rightHand);
            var isIntersecting = OpenVR.Overlay.IsHoverTargetOverlay(_overlay.GetOverlayHandle());
            if (isIntersecting)
            {
                _stopwatch.Restart();
            }
        }
    }

    private bool IsIntersecting(HVPoseData poseData, uint rightHand)
    {
        // FIXME: None of this works, unsure why. Intersection is offset, or has wrong angle or something. It feels squished vertically.
        
        var rightHandPose = poseData.Poses[rightHand].mDeviceToAbsoluteTracking;
        var move = new Vector3(0, -0.2f, 0);
        var absToOverHand = HVOvrGeofunctions.OvrToOvrnum(HVOvrGeofunctions.OvrTranslate(move)) * HVOvrGeofunctions.OvrToOvrnum(rightHandPose);
        var pos = HVOvrGeofunctions.PosOvr(HVOvrGeofunctions.OvrnumToOvr(absToOverHand));
        var intersection = new VROverlayIntersectionParams_t
        {
            eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding,
            vSource = pos,
            vDirection = HVOvrGeofunctions.ForwardOvr(rightHandPose)
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
        }
    }
}