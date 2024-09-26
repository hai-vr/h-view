using hcontroller.Lyuma;

namespace Hai.HView.OSC.PretendToBeVRC;

/// When VRChat is not running, pretend that VRChat is still running in order to let OSC programs
/// send information to this application (i.e. face tracking).
public class FakeVRCOSC
{
    private const string FakeHViewAvatarId = "avtr_00000000-bc83-4caa-b77f-000000000000";
    private const int VrcOscPort = 9000;
    private const int VrcFtPort = 9001;

    private readonly HOsc _client;

    public FakeVRCOSC()
    {
        _client = new HOsc(VrcOscPort);
    }

    public void Start()
    {
        _client.Start();
        _client.SetReceiverOscPort(VrcFtPort);
    }

    public void SendAvatarChange()
    {
        _client.SendOsc(CommonOSCAddresses.AvatarChangeOscAddress, FakeHViewAvatarId);
    }

    public void Finish()
    {
        _client.Finish();
    }

    public List<SimpleOSC.OSCMessage> PullMessages()
    {
        return _client.PullMessages();
    }
}