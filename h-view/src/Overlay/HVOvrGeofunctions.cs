using System.Numerics;
using Valve.VR;

namespace Hai.HView.Overlay;

/// Anything that takes or returns a Valve.VR.HmdMatrix34_t goes here.
public static class HVOvrGeofunctions
{
    // Ovr: OpenVR matrix functions
    
    public static HmdMatrix34_t OvrIdentity()
    {
        return new HmdMatrix34_t {
            m0 = 1f, m1 = 0f, m2 = 0f, m3 = 0f,
            m4 = 0f, m5 = 1f, m6 = 0f, m7 = 0f,
            m8 = 0f, m9 = 0f, m10 = 1f, m11 = 0f
        };
    }

    public static HmdMatrix34_t OvrTranslate(Vector3 translation)
    {
        return new HmdMatrix34_t {
            m0 = 1f, m1 = 0f, m2 = 0f, m3 = translation.X,
            m4 = 0f, m5 = 1f, m6 = 0f, m7 = translation.Y,
            m8 = 0f, m9 = 0f, m10 = 1f, m11 = translation.Z
        };
    }

    public static HmdMatrix34_t OvrTR(Vector3 translation, Quaternion rotation)
    {
        var numRot = Matrix4x4.CreateFromQuaternion(rotation);

        return new HmdMatrix34_t
        {
            m0 = numRot.M11, m1 = numRot.M12, m2 = numRot.M13, m3 = translation.X,
            m4 = numRot.M21, m5 = numRot.M22, m6 = numRot.M23, m7 = translation.Y,
            m8 = numRot.M31, m9 = numRot.M32, m10 = numRot.M33, m11 = translation.Z,
        };
    }

    public static HmdMatrix34_t OvrTRS(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        var numRot = Matrix4x4.CreateFromQuaternion(rotation);

        return new HmdMatrix34_t
        {
            m0 = numRot.M11 * scale.X, m1 = numRot.M12 * scale.X, m2 = numRot.M13 * scale.X, m3 = translation.X,
            m4 = numRot.M21 * scale.Y, m5 = numRot.M22 * scale.Y, m6 = numRot.M23 * scale.Y, m7 = translation.Y,
            m8 = numRot.M31 * scale.Z, m9 = numRot.M32 * scale.Z, m10 = numRot.M33 * scale.Z, m11 = translation.Z,
        };
    }

    public static Matrix4x4 OvrToUnity(HmdMatrix34_t ovr)
    {
        return HVGeofunctions.OvrnumToUnity(OvrToOvrnum(ovr));
    }

    public static Matrix4x4 OvrToOvrnum(HmdMatrix34_t ovr)
    {
        return new Matrix4x4(
            ovr.m0, ovr.m1, ovr.m2, ovr.m3,
            ovr.m4, ovr.m5, ovr.m6, ovr.m7,
            ovr.m8, ovr.m9, ovr.m10, ovr.m11,
            0, 0, 0, 1
        );
    }
    
    public static HmdMatrix34_t OvrnumToOvr(Matrix4x4 overnum)
    {
        return new HmdMatrix34_t {
            m0 = overnum.M11, m1 = overnum.M12, m2 = overnum.M13, m3 = overnum.M14,
            m4 = overnum.M21, m5 = overnum.M22, m6 = overnum.M23, m7 = overnum.M24,
            m8 = overnum.M31, m9 = overnum.M32, m10 = overnum.M33, m11 = overnum.M34,
        };
    }
    
    public static HmdVector3_t Vec(Vector3 num)
    {
        return new HmdVector3_t
        {
            v0 = num.X, v1 = num.Y, v2 = num.Z
        };
    }
}