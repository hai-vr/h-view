using Hai.HView.Networking.Client;
using Hai.HView.Networking.Shared;
using Hai.HView.Networking.Steamworks.Server;
using Steamworks;
using Steamworks.Data;

namespace Hai.HView.Networking.Steamworks.Client;

public class HNClientConnectionManager : IConnectionManager
{
    public event OnDisconnectedFully onDisconnectedFully;
    public delegate void OnDisconnectedFully();
        
    private readonly HNClient _client;
    private ClientConnectionState _state = ClientConnectionState.NotStarted;
        
    [NonSerialized] public IHNClientConnectionHandle Handle;

    public HNClientConnectionManager(HNClient client)
    {
        _client = client;
    }

    public void OnConnecting(ConnectionInfo info)
    {
        Console.WriteLine($"OnConnecting");
        _state = ClientConnectionState.Connecting;
    }

    public void OnConnected(ConnectionInfo info)
    {
        Console.WriteLine($"OnConnected");
        _state = ClientConnectionState.Connected;
        _client.OnConnected(Handle);
    }

    public void OnDisconnected(ConnectionInfo info)
    {
        Console.WriteLine($"OnDisconnected");
        _state = ClientConnectionState.Disconnected;
        _client.OnDisconnected(Handle);
        onDisconnectedFully?.Invoke();
    }

    public void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        if (size > HNSteamNetworkingSocketManager.MaximumMessageLength)
        {
            Console.WriteLine($"Ignored rogue message of size {size} which is larger than the maximum allowed {HNSteamNetworkingSocketManager.MaximumMessageLength}");
            return;
        }
        _client.OnMessage(Handle, new HNMessage
        {
            recvTime = recvTime,
            messageNum = messageNum,
            segment = HNSteamNetworkingSocketManager.ToArraySegment(data, size)
        });
    }
}

public enum ClientConnectionState
{
    NotStarted, Connecting, Connected, Disconnected
}