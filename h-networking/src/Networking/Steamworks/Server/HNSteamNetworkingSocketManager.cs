using System.Runtime.InteropServices;
using Hai.HNetworking.Server;
using Steamworks;
using Steamworks.Data;

namespace Hai.HNetworking.Steamworks.Server;

public class HNSteamNetworkingSocketManager : ISocketManager
{
    public const int MaximumMessageLength = 1024 * 1024;
    
    private readonly HNServer _server;
    private static readonly byte[] _maximumMessageBuffer = new byte[MaximumMessageLength];
    
    private readonly List<Connection> _activeConnections = new List<Connection>();

    public HNSteamNetworkingSocketManager(HNServer server)
    {
        _server = server;
    }

    public void OnConnecting(Connection connection, ConnectionInfo info)
    {
        Log($"{info.Identity.SteamId} is connecting");
        connection.Accept();
    }

    public void OnConnected(Connection connection, ConnectionInfo info)
    {
        Log($"{info.Identity.SteamId} is connected");
        
        _activeConnections.Add(connection);
        
        // Log($"NOT IMPLEMENTED. Closing connection");
        // connection.Close();
    }

    public void OnDisconnected(Connection connection, ConnectionInfo info)
    {
        Log($"{info.Identity.SteamId} disconnected");
        
        _activeConnections.Remove(connection);
    }

    public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        Log($"{identity.SteamId} sent message of size {size}, number {messageNum}, channel {channel}");
        
        if (size >= MaximumMessageLength)
        {
            Log($"Ignored rogue message of size {size} which is larger than the maximum allowed {MaximumMessageLength}");
        }
        
        // Log($"NOT IMPLEMENTED. Closing connection");
        // connection.Close();
    }

    private void Log(string s)
    {
        Console.WriteLine($"[Server::SteamNetworkingSocketManager] {s}");
    }
    
    public static ArraySegment<byte> ToArraySegment(IntPtr data, int size)
    {
        var managedArray = _maximumMessageBuffer;
        Marshal.Copy(data, managedArray, 0, size);
        var arraySegment = new ArraySegment<byte>(managedArray, 0, size);
        return arraySegment;
    }

    public void CloseAllConnections()
    {
        var copy = _activeConnections.ToArray();
        foreach (var connection in copy)
        {
            connection.Close();
        }
    }
}