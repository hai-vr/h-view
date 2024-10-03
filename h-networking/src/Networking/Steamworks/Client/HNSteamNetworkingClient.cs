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
        
        Log("Creating connections");
        _connectionManager = new HNClientConnectionManager(_client);
        _connectionManager.OnDisconnectedFully += WhenDisconnectedFully;
        _intermediaryManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(steamId);
        Log("Connected");
        _intermediaryManager.Interface = _connectionManager;
        _handle = new HNClientConnectionHandle(_intermediaryManager.Connection, new HNSteamIdentity(steamId));
        _connectionManager.Handle = _handle;
    }

    private void WhenDisconnectedFully()
    {
        Log("WhenDisconnectedFully");
        _intermediaryManager.Close();
        _enabled = false;
        OnDisconnected?.Invoke();
    }

    private void Log(string s)
    {
        Console.WriteLine($"[Client::SteamNetworkingClient] {s}");
    }

    public void Update()
    {
        if (!_enabled) return;
        if (_intermediaryManager == null) return; // Can be null when connection is being established
        
        _intermediaryManager.Receive();
        _client.PostReceive();
    }
}