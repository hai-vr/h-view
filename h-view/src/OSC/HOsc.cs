using System.Net;
using System.Numerics;
using hcontroller.Lyuma;
using VRC.OSCQuery;

namespace Hai.HView.OSC;

public class HOsc
{
    private const int DefaultReceiverPort = 9000;
    private int _currentReceiverPort;
    
    private readonly int _oscPort;
    private readonly SimpleOSC _client;
    
    private readonly byte[] _byteBuffer = new byte[65535];
    private readonly List<SimpleOSC.OSCMessage> _queue = new();

    public HOsc(int oscPort)
    {
        _oscPort = oscPort;
        _client = new SimpleOSC();
    }

    public static int RandomOscPort()
    {
        return Extensions.GetAvailableUdpPort();
    }

    public void Start()
    {
        RedefineReceiver(DefaultReceiverPort);
        _client.OpenClient(_oscPort);
    }

    public void SetReceiverOscPort(int oscPort)
    {
        RedefineReceiver(oscPort);
    }

    private void RedefineReceiver(int receiverPort)
    {
        if (receiverPort == _currentReceiverPort) return;

        _currentReceiverPort = receiverPort;

        _client.SetUnconnectedEndpoint(new IPEndPoint(IPAddress.Loopback, receiverPort));
    }
    
    public void SendOsc(string oscItemKey, object x)
    {
        _client.SendOSCPacket(new SimpleOSC.OSCMessage
        {
            path = oscItemKey,
            arguments = new[] { x }
        }, _byteBuffer);
    }

    public void SendOscMultivalue(string oscItemKey, object[] objects)
    {
        _client.SendOSCPacket(new SimpleOSC.OSCMessage
        {
            path = oscItemKey,
            arguments = objects
        }, _byteBuffer);
    }

    public void SendVrcTracker(int oscTrackerNumber, Vector3 unityPosition, Quaternion unityRotation)
    {
        var intermediate = UnityQuaternionToEuler(unityRotation) * (float)(180f / Math.PI);
        var unityEuler = new Vector3(-intermediate.Y, -intermediate.X, -intermediate.Z);
        
        _client.SendOSCPacket(new SimpleOSC.OSCMessage
        {
            path = $"/tracking/trackers/{oscTrackerNumber}/position",
            arguments = new object[] { unityPosition.X, unityPosition.Y, unityPosition.Z },
            typeTag = ",fff",
            time = new SimpleOSC.TimeTag()
        }, _byteBuffer);
        _client.SendOSCPacket(new SimpleOSC.OSCMessage
        {
            path = $"/tracking/trackers/{oscTrackerNumber}/rotation",
            arguments = new object[] { unityEuler.X, unityEuler.Y, unityEuler.Z },
            typeTag = ",fff",
            time = new SimpleOSC.TimeTag()
        }, _byteBuffer);
    }

    private static Vector3 UnityQuaternionToEuler(Quaternion q)
    {
        var xSquared = q.X * q.X;
        var ySquared = q.Y * q.Y;
        var zSquared = q.Z * q.Z;
        var wSquared = q.W * q.W;
        var r11 = -2 * (q.X * q.Y - q.W * q.Z);
        var r12 = wSquared - xSquared + ySquared - zSquared;
        var r21 = 2 * (q.Y * q.Z + q.W * q.X);
        var r31 = -2 * (q.X * q.Z - q.W * q.Y);
        var r32 = wSquared - xSquared - ySquared + zSquared;
        return ThreeAxisRot(r11, r12, r21, r31, r32);
    }

    private static Vector3 ThreeAxisRot(float r11, float r12, float r21, float r31, float r32)
    {
        var x = (float)Math.Atan2(r31, r32);
        var y = (float)Math.Asin(r21);
        var z = (float)Math.Atan2(r11, r12);
        return new Vector3(x, y, z);
    }

    public List<SimpleOSC.OSCMessage> PullMessages()
    {
        _queue.Clear();
        _client.GetIncomingOSC(_queue);
        return _queue;
    }

    public void Finish()
    {
        _client.StopClient();
    }
}