using Hai.HNetworking.Client;
using Hai.HNetworking.Shared;
using Steamworks;

namespace Hai.HNetworking.Steamworks.Client;

public class HNSteamNetworkingClient
{
    public event Action OnDisconnected;
    
    private readonly HNClient _client;
    private HNClientConnectionManager _connectionManager;
    private ConnectionManager _intermediaryManager;
    private HNClientConnectionHandle _handle;
    private bool _enabled;

    public HNSteamNetworkingClient(HNClient client)
    {
        _client = client;
    }

    public void Join(SteamId steamId, string joinCode)
    {
        if (_enabled) return;
        _enabled = true;
        
        _client.SetJoinCode(joinCode);
        
        Console.WriteLine($"Creating connections");
        _connectionManager = new HNClientConnectionManager(_client);
        _connectionManager.onDisconnectedFully += WhenDisconnectedFully;
        _intermediaryManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(steamId);
        _intermediaryManager.Interface = _connectionManager;
        _handle = new HNClientConnectionHandle(_intermediaryManager.Connection, new HNSteamIdentity(steamId));
        _connectionManager.Handle = _handle;
    }

    private void WhenDisconnectedFully()
    {
        _intermediaryManager.Close();
        _enabled = false;
        OnDisconnected?.Invoke();
    }
}