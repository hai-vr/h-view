using Hai.HNetworking.Client;
using Hai.HNetworking.Shared;
using Hai.HNetworking.Steamworks.Server;
using Steamworks;
using Steamworks.Data;

namespace Hai.HNetworking.Steamworks.Client;

public class HNClientConnectionManager : IConnectionManager
{
    public event DisconnectedFully OnDisconnectedFully;
    public delegate void DisconnectedFully();
        
    private readonly HNClient _client;
    private ClientConnectionState _state = ClientConnectionState.NotStarted;
        
    [NonSerialized] public IHNClientConnectionHandle Handle;
    
    private bool _disconnected;

    public HNClientConnectionManager(HNClient client)
    {
        _client = client;
    }

    public void OnConnecting(ConnectionInfo info)
    {
        Log("OnConnecting");
        _state = ClientConnectionState.Connecting;
    }

    public void OnConnected(ConnectionInfo info)
    {
        Log("OnConnected");
        _state = ClientConnectionState.Connected;
        _client.OnConnected(Handle);
    }
    
    public void OnDisconnected(ConnectionInfo info)
    {
        if (_disconnected) return; // Prevent double-call
        _disconnected = true;
        
        Log("OnDisconnected");
        _state = ClientConnectionState.Disconnected;
        _client.OnDisconnected(Handle);
        OnDisconnectedFully?.Invoke();
    }

    public void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        if (size > HNSteamNetworkingSocketManager.MaximumMessageLength)
        {
            Log($"Ignored rogue message of size {size} which is larger than the maximum allowed {HNSteamNetworkingSocketManager.MaximumMessageLength}");
            return;
        }
        _client.OnMessage(Handle, new HNMessage
        {
            recvTime = recvTime,
            messageNum = messageNum,
            segment = HNSteamNetworkingSocketManager.ToArraySegment(data, size)
        });
    }

    private void Log(string s)
    {
        Console.WriteLine($"[Client::ConnectionManager] {s}");
    }
}

public enum ClientConnectionState
{
    NotStarted, Connecting, Connected, Disconnected
}