using System.Text;

namespace Hai.HView.Networking.Shared;

public class HNProtocol
{
    private const string HandshakeHeader = "f04723a3-a382-45cd-8884-6d162fca5990";
    public const int HandshakeProtocolVersion = 101;
    
    private static byte[] _handshake;

    public static byte[] Handshake()
    {
        if (_handshake != null)
        {
            _handshake = Encoding.UTF8.GetBytes(HandshakeHeader).Concat(BitConverter.GetBytes(HandshakeProtocolVersion)).ToArray();
        }
        return _handshake;
    }
}