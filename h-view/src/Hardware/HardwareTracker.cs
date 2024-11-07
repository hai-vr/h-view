using System.Numerics;
using Valve.VR;

namespace Hai.HView.Hardware
{
    public struct HardwareTracker
    {
        public uint DeviceIndex;
        public string SerialNumber;
        public bool Exists;
        public bool IsHealthy;
        public Vector3 Pos;
        // public Quaternion Rot;
        // public Matrix4x4 Matrix;
        public ETrackingResult DebugTrackingResult;
        public Vector3 AngVel;
        public float BatteryLevel;
        // public Vector3 Euler;
        // public double SimpleAngle;
        public ETrackedDeviceClass DeviceClass;
        public Vector3 Vel;
        public ETrackedControllerRole ControllerRole;
        public string Manufacturer;
        public string ModelNumber;
        public float ClosestTrackerDistance;
        public DateTime LastIssueTime;
        public bool IsBatteryCharging;
    }
}