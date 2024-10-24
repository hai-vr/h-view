using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
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
    
    public static bool IfDigitalInputChanged(ulong action, out bool newValue)
    {
        var data = GetDigitalInput(action);
        newValue = data.bState;
        return data.bChanged;
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

    public static Dictionary<ulong, string> FindAllOverlayHandlesBrute()
    {
        var results = new Dictionary<ulong, string>();
        
        // There seems to be a pattern where j == i - 8, but there are some exceptions.
        ulong lowerBound = 0;
        ulong searchUpperBound = 256;
        for (ulong i = lowerBound; i < searchUpperBound; i++)
        {
            for (ulong j = 0; j < searchUpperBound; j++)
            {
                ulong possibleHandle = (i << 32) + j;
                var err = EVROverlayError.None;
                var sb = new StringBuilder(256);
                _ = OpenVR.Overlay.GetOverlayKey(possibleHandle, sb, 256, ref err);
                if (err == EVROverlayError.None)
                {
                    results.Add(possibleHandle, sb.ToString());
                }
            }
        }

        return results;
    }

    public static string GetOverlayNameOrNull(ulong handle)
    {
        var err = EVROverlayError.None;
        var nameBuilder = new StringBuilder(1024);
        var klen2 = OpenVR.Overlay.GetOverlayName(handle, nameBuilder, 1024, ref err);
        if (err == EVROverlayError.None)
        {
            return nameBuilder.ToString();
        }

        return null;
    }
}