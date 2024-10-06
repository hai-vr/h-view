using System.Numerics;
using System.Runtime.InteropServices;
using Valve.VR;

namespace Hai.HView.OVR;

public static class OpenVRUtils
{
    private static readonly uint SizeOfDigitalActionData = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));
    
    public static InputDigitalActionData_t GetDigitalInput(ulong action)
    {
        InputDigitalActionData_t data = default;
        OpenVR.Input.GetDigitalActionData(action, ref data, SizeOfDigitalActionData, 0);
        return data;
    }

    /// Invokes ComputeOverlayIntersection but restricts the accepted U and V components to [0, 1], returning false when not in that range.
    public static bool ComputeOverlayIntersectionStrictUVs(ulong ulOverlayHandle, VROverlayIntersectionParams_t pParams, out Vector2 uv)
    {
        VROverlayIntersectionResults_t results = default;
        var success = OpenVR.Overlay.ComputeOverlayIntersection(ulOverlayHandle, ref pParams, ref results);
        if (success)
        {
            var x01 = results.vUVs.v0;
            var y01 = results.vUVs.v1;
            if (x01 is >= 0f and <= 1f && y01 is >= 0f and <= 1f)
            {
                uv = new Vector2(x01, y01);
                return true;
            }
        }

        uv = Vector2.Zero;
        return false;
    }

    public static bool IsValidDeviceIndex(uint deviceIndex)
    {
        return deviceIndex != OpenVR.k_unTrackedDeviceIndexInvalid;
    }

    public static void TriggerHapticPulse(uint deviceIndex, ushort durationMicroseconds)
    {
        // unAxisId is always zero ( https://steamcommunity.com/app/358720/discussions/0/517141624283630663/ )
        OpenVR.System.TriggerHapticPulse(deviceIndex, 0, durationMicroseconds);
    }
}