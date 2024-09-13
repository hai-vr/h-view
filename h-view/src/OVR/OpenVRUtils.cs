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
}