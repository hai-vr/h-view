using System.Numerics;
using Valve.VR;

namespace Hai.HView.Overlay;

public class HVOverlayMovement
{
    public void Evaluate(ulong hOverlay, HVPoseData poseData)
    {
        var controllerIndex = poseData.RightHandDeviceIndex;
        if (IsValidDeviceIndex(controllerIndex))
        {
            // TODO: The following is just test values.
            var quaternion = HVGeofunctions.QuaternionFromAngles(new Vector3(35, -25, -9), HVRotationMulOrder.YZX);
            var pos = HVOvrGeofunctions.OvrTRS(new Vector3(-0.4f, -0.01f, 0.2f), quaternion, Vector3.One);
            OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(hOverlay, controllerIndex, ref pos);
        }
        // OpenVR.Overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref _identity);
        
        // FIXME: This breaks OVR Advanced Settings motion / playspace mover!
        OpenVR.Overlay.SetOverlayFlag(hOverlay, VROverlayFlags.MakeOverlaysInteractiveIfVisible, true);
    }

    private static bool IsValidDeviceIndex(uint deviceIndex)
    {
        return deviceIndex != OpenVR.k_unTrackedDeviceIndexInvalid;
    }
}

public class HVPoseData
{
    public TrackedDevicePose_t[] Poses;
    public uint LeftHandDeviceIndex;
    public uint RightHandDeviceIndex;
}