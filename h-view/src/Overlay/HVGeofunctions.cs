using System.Numerics;

namespace Hai.HView.Overlay;

public static class HVGeofunctions
{
    // Ovrnum: System.Numerics matrix functions using OpenVR coordinate system
    // Unity: System.Numerics matrix functions using Unity coordinate system
    
    public static Matrix4x4 TR(Vector3 translation, Quaternion rotation)
    {
        var numRot = Matrix4x4.CreateFromQuaternion(rotation);

        return new Matrix4x4
        (
            numRot.M11, numRot.M12, numRot.M13, translation.X,
            numRot.M21, numRot.M22, numRot.M23, translation.Y,
            numRot.M31, numRot.M32, numRot.M33, translation.Z,
            0, 0, 0, 1
        );
    }

    public static Matrix4x4 TRS(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        var numRot = Matrix4x4.CreateFromQuaternion(rotation);

        return new Matrix4x4
        (
            numRot.M11 * scale.X, numRot.M12 * scale.X, numRot.M13 * scale.X, translation.X,
            numRot.M21 * scale.Y, numRot.M22 * scale.Y, numRot.M23 * scale.Y, translation.Y,
            numRot.M31 * scale.Z, numRot.M32 * scale.Z, numRot.M33 * scale.Z, translation.Z,
            0, 0, 0, 1
        );
    }

    public static Matrix4x4 OvrnumToUnity(Matrix4x4 generic)
    {
        return new Matrix4x4(
            generic.M11, generic.M12, -generic.M13, generic.M14,
            generic.M21, generic.M22, -generic.M23, generic.M24,
            -generic.M31, -generic.M32, generic.M33, -generic.M34,
            generic.M41, generic.M42, generic.M43, generic.M44
        );
    }
    
    public static void ToPosRotV3(Matrix4x4 matrix, out Vector3 pos, out Quaternion rot)
    {
        pos = new Vector3(matrix.M14, matrix.M24, matrix.M34);
        rot = Quaternion.CreateFromRotationMatrix(matrix);
    }
    
    // FIXME: This is a patched function that immediately inverts the quaternion obtained from the rotation matrix.
    // I have no idea why this is needed. This is probably caused by Quaternion.CreateFromRotationMatrix
    // outputting the transpose, because cols and rows are swapped or something like this.
    public static void ToPosRotV3__RectifiedRotation(Matrix4x4 matrix, out Vector3 pos, out Quaternion rot)
    {
        pos = new Vector3(matrix.M14, matrix.M24, matrix.M34);
        rot = Quaternion.Inverse(Quaternion.CreateFromRotationMatrix(matrix));
    }
    
    public static void ToV4(Matrix4x4 matrix, out Vector4 v4)
    {
        v4 = new Vector4(matrix.M14, matrix.M24, matrix.M34, matrix.M44);
    }

    /// TODO: this function is total improv, I don't know if there's a formal system to represent this.
    public static Quaternion QuaternionFromAngles(Vector3 degreesOnEachAxis, HVRotationMulOrder mulOrder)
    {
        var x = Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(degreesOnEachAxis.X * (Math.PI / 180)));
        var y = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(degreesOnEachAxis.Y * (Math.PI / 180)));
        var z = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(degreesOnEachAxis.Z * (Math.PI / 180)));
        
        switch (mulOrder)
        {
            // TODO: I don't know if the order (i.e. XYZ) actually represents the order of the members in the quaternion multiplication, or if it should have been reversed.
            case HVRotationMulOrder.XYZ: return x * y * z;
            case HVRotationMulOrder.XZY: return x * z * y;
            case HVRotationMulOrder.YXZ: return y * x * z;
            case HVRotationMulOrder.YZX: return y * z * x;
            case HVRotationMulOrder.ZXY: return z * x * y;
            case HVRotationMulOrder.ZYX: return z * y * x;
            default:
                throw new ArgumentOutOfRangeException(nameof(mulOrder), mulOrder, null);
        }
    }

    private static Vector3 Intersect(Vector3 linePoint, Vector3 lineNormal, Vector3 planePoint, Vector3 planeNormal)
    {
        return Vector3.Dot(planePoint - linePoint, planeNormal) / Vector3.Dot(lineNormal, planeNormal) * lineNormal + linePoint;
    }

    public static Matrix4x4 QuickInvert(Matrix4x4 matrix)
    {
        if (Matrix4x4.Invert(matrix, out var result)) return result;
        return Matrix4x4.Identity;
    }
}

public enum HVRotationMulOrder
{
    XYZ,
    XZY,
    YXZ,
    YZX,
    ZXY,
    ZYX
}