using System.Numerics;

namespace Hai.HView.Overlay.Stereocomposer;

public class HGrabMachine
{
    private readonly Dictionary<string, int> _keyToHandle = new();
    private readonly Dictionary<int, HGrabbable> _handleToGrabbable = new();
    private int i = 0;

    public int HandleFor(string obj)
    {
        if (_keyToHandle.TryGetValue(obj, out var result))
        {
            return result;
        }

        var handle = ++i;
        _keyToHandle[obj] = handle;
        return handle;
    }

    public void InitiateGrab(int grabHandle, Matrix4x4 rightHandMatrix, Vector3 pos, Quaternion rot, Action<Vector3, Quaternion> updateFn)
    {
        _handleToGrabbable[grabHandle] = new HGrabbable
        {
            HandToObj = HVGeofunctions.QuickInvert(rightHandMatrix) * HVGeofunctions.TR(pos, rot),
            Pos = pos,
            Rot = rot,
            DestPos = pos,
            DestRot = rot,
            UpdateFn = updateFn,
            IsGrabbed = true
        };
    }

    public void ReleaseGrab(int grabHandle)
    {
        if (_handleToGrabbable.TryGetValue(grabHandle, out var value))
        {
            value.IsGrabbed = false;
        }
    }

    public void UpdateGrabbables(Matrix4x4 rightHandMatrix)
    {
        foreach (var grabbable in _handleToGrabbable.Values)
        {
            if (grabbable.IsGrabbed)
            {
                var destinationMatrix = rightHandMatrix * grabbable.HandToObj;
                HVGeofunctions.ToPosRotV3(destinationMatrix, out var pos, out var rot);
                grabbable.DestPos = pos;
                grabbable.DestRot = rot;
            }

            grabbable.Pos = Vector3.Lerp(grabbable.Pos, grabbable.DestPos, 0.1f);
            grabbable.Rot = Quaternion.Slerp(grabbable.Rot, grabbable.DestRot, 0.1f);
            
            grabbable.UpdateFn.Invoke(grabbable.Pos, grabbable.Rot);
        }
    }
}

internal class HGrabbable
{
    public Matrix4x4 HandToObj;
    public Action<Vector3, Quaternion> UpdateFn;
    public bool IsGrabbed;
    public Vector3 Pos;
    public Quaternion Rot;
    public Vector3 DestPos;
    public Quaternion DestRot;
}