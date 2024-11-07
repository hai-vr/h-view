using System.Numerics;
using System.Text;
using Hai.HView.Data;
using Hai.HView.Overlay;
using Valve.VR;

namespace Hai.HView.Hardware
{
    public class HHardwareRoutine
    {
        private readonly SavedData _config;
        private const string UnknownSerialNumber = "DH-UNKNOWN-SN";

        private readonly TrackedDevicePose_t[] _holder = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private readonly HardwareTracker[] _hardwares = new HardwareTracker[OpenVR.k_unMaxTrackedDeviceCount];
        private uint _maxValidIndex;

        public HHardwareRoutine(SavedData config)
        {
            _config = config;
            for (var i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                _hardwares[i].DeviceIndex = (uint)i;
            }
        }

        public void UpdateHardwareTrackers()
        {
            OpenVR.System.GetDeviceToAbsoluteTrackingPose(
                ETrackingUniverseOrigin.TrackingUniverseStanding,
                0,
                _holder
            );
            var now = DateTime.Now;
            for (uint deviceIndex = 0; deviceIndex < _holder.Length; deviceIndex++)
            {
                var device = _holder[deviceIndex];
                var deviceClass = OpenVR.System.GetTrackedDeviceClass(deviceIndex);
                var role = OpenVR.System.GetControllerRoleForTrackedDeviceIndex(deviceIndex);
                if (device.bDeviceIsConnected && deviceIndex > _maxValidIndex)
                {
                    _maxValidIndex = deviceIndex;
                }
                if (device.bDeviceIsConnected)
                {
                    var matrix = HVOvrGeofunctions.OvrToOvrnum(device.mDeviceToAbsoluteTracking);

                    // Since VRChat OSC Trackers expects positions and euler angles using Unity's coordinate system,
                    // we might as well immediately convert the SteamVR matrices to that coordinate system.
                    // https://docs.vrchat.com/docs/osc-trackers
                    matrix = HVGeofunctions.OvrnumToUnity(matrix);

                    var pos = new Vector3(matrix.M14, matrix.M24, matrix.M34);
                    // var quat = Quaternion.CreateFromRotationMatrix(matrix).Normalize();

                    var angVelRaw = device.vAngularVelocity;
                    var angVel = new Vector3(angVelRaw.v0, angVelRaw.v1, angVelRaw.v2);
                    var velRaw = device.vVelocity;
                    var vel = new Vector3(velRaw.v0, velRaw.v1, velRaw.v2);
                    var serial = GetDevicePropertyStringOrNull(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String);
                    
                    var eTrackedDeviceClass = deviceClass;
                    if (serial != null && !_config.ovrSerialToPreference.ContainsKey(serial))
                    {
                        var x = -1;
                        var y = -1;
                        var name = serial;
                        if (deviceClass == ETrackedDeviceClass.HMD)
                        {
                            x = 2;
                            y = 0;
                            name = "HMD";
                        }
                        else if (deviceClass == ETrackedDeviceClass.Controller && (role == ETrackedControllerRole.LeftHand || role == ETrackedControllerRole.RightHand))
                        {
                            x = role == ETrackedControllerRole.LeftHand ? 1 : 3;
                            y = 1;
                            name = $"{role}";
                        }
                        _config.ovrSerialToPreference[serial] = new HOpenVrHardwarePreference
                        {
                            name = name,
                            positionX = x,
                            positionY = y,
                            includeInHapticsMeasurements = deviceClass == ETrackedDeviceClass.GenericTracker
                        };
                    }

                    var isHealthy = device.eTrackingResult == ETrackingResult.Running_OK;
                    _hardwares[deviceIndex] = new HardwareTracker
                    {
                        DeviceIndex = deviceIndex,
                        DeviceClass = eTrackedDeviceClass,
                        ControllerRole = role,
                        SerialNumber = serial ?? UnknownSerialNumber,
                        Manufacturer = GetDevicePropertyStringOrNull(deviceIndex, ETrackedDeviceProperty.Prop_ManufacturerName_String) ?? "",
                        ModelNumber = GetDevicePropertyStringOrNull(deviceIndex, ETrackedDeviceProperty.Prop_ModelNumber_String) ?? "",
                        Exists = true,
                        IsHealthy = isHealthy,
                        Pos = pos,
                        // Rot = quat,
                        // Matrix = matrix,
                        AngVel = angVel,
                        Vel = vel,
                        DebugTrackingResult = device.eTrackingResult,
                        BatteryLevel = GetBatteryLevel(deviceIndex),
                        IsBatteryCharging = IsBatteryCharging(deviceIndex),
                        // Euler = Geofunctions.ToUnityForumsZXYEulerDegrees(quat),
                        // SimpleAngle = Geofunctions.QuaternionAngleDeg_IgnoreNaN(quat)
                        ClosestTrackerDistance = 0,
                        LastIssueTime = isHealthy ? _hardwares[deviceIndex].LastIssueTime : now
                    };
                }
                else
                {
                    _hardwares[deviceIndex].DeviceIndex = deviceIndex;
                    _hardwares[deviceIndex].Exists = false;
                    _hardwares[deviceIndex].DebugTrackingResult = 0;
                    _hardwares[deviceIndex].LastIssueTime = now;
                }
            }
            
            var validTrackers = _hardwares
                .Where(other => other.Exists && other.DeviceClass != ETrackedDeviceClass.TrackingReference)
                .ToArray();
            if (validTrackers.Length > 0)
            {
                for (var index = 0; index < _hardwares.Length; index++)
                {
                    var thisDevice = _hardwares[index];
                    if (thisDevice.Exists)
                    {
                        var otherTracker = validTrackers
                            .Select(other => (other.Pos - thisDevice.Pos).Length())
                            .DefaultIfEmpty(0)
                            .Min();
                        _hardwares[index].ClosestTrackerDistance = otherTracker;
                    }
                }
            }
        }

        private float GetBatteryLevel(uint deviceIndex)
        {
            var propError = ETrackedPropertyError.TrackedProp_Success;
            return OpenVR.System.GetFloatTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref propError);
        }

        private bool IsBatteryCharging(uint deviceIndex)
        {
            var propError = ETrackedPropertyError.TrackedProp_Success;
            return OpenVR.System.GetBoolTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_DeviceIsCharging_Bool, ref propError);
        }

        private static string GetDevicePropertyStringOrNull(uint deviceIndex, ETrackedDeviceProperty which)
        {
            var error = ETrackedPropertyError.TrackedProp_Success;

            var capacity = OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex,
                which, null, 0, ref error);
            if (capacity > 1)
            {
                var buffer = new StringBuilder((int)capacity);
                OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex,
                    which, buffer, capacity, ref error);
                return buffer.ToString();
            }

            return null;
        }

        public UiHardwareResponse UiHardware()
        {
            return new UiHardwareResponse
            {
                Trackers = _hardwares,
                MaxIndex = _maxValidIndex
            };
        }
    }

    public class UiHardwareResponse
    {
        public HardwareTracker[] Trackers;
        public uint MaxIndex;
    }
}