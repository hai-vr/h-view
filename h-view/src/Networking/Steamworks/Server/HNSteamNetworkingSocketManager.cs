using System.Runtime.InteropServices;
using Hai.HView.Networking.Server;
using Steamworks;
using Steamworks.Data;

namespace Hai.HView.Networking.Steamworks.Server;

public class HNSteamNetworkingSocketManager : ISocketManager
{
    public const int MaximumMessageLength = 1024 * 1024;
    
    private readonly HNServer _server;
    private static readonly byte[] _maximumMessageBuffer = new byte[MaximumMessageLength];

    public HNSteamNetworkingSocketManager(HNServer server)
    {
        _server = server;
    }

    public void OnConnecting(Connection connection, ConnectionInfo info)
    {
        Console.WriteLine($"{info.Identity.SteamId} is connecting");
        connection.Accept();
    }

    public void OnConnected(Connection connection, ConnectionInfo info)
    {
        Console.WriteLine($"{info.Identity.SteamId} is connected");
        connection.Close();
    }

    public void OnDisconnected(Connection connection, ConnectionInfo info)
    {
        Console.WriteLine($"{info.Identity.SteamId} disconnected");
    }

    public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        Console.WriteLine($"{identity.SteamId} sent message of size {size}, number {messageNum}, channel {channel}");
        
        if (size >= MaximumMessageLength)
        {
            Console.WriteLine($"Ignored rogue message of size {size} which is larger than the maximum allowed {MaximumMessageLength}");
        }
        
        connection.Close();
    }
    
    public static ArraySegment<byte> ToArraySegment(IntPtr data, int size)
    {
        var managedArray = _maximumMessageBuffer;
        Marshal.Copy(data, managedArray, 0, size);
        var arraySegment = new ArraySegment<byte>(managedArray, 0, size);
        return arraySegment;
    }
}