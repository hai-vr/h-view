using Hai.HNetworking.Server;
using Steamworks;

namespace Hai.HNetworking.Steamworks.Server;

public class HNSteamNetworkingServer
{
    private readonly HNServer _server;
    
    private bool _enabled;
    private SocketManager _socket;
    private HNSteamNetworkingSocketManager _intermediaryManager;

    public HNSteamNetworkingServer(HNServer server)
    {
        _server = server;
    }

    public void Update()
    {
        if (_enabled) return;
        
        _socket.Receive();
        _server.PostReceive();
    }

    public void Enable()
    {
        if (_enabled) return;
        _enabled = true;
        
        _socket = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
        _intermediaryManager = new HNSteamNetworkingSocketManager(_server);
        _socket.Interface = _intermediaryManager;
        
        Log("Created server");
    }

    public void Disable()
    {
        if (!_enabled) return;
        _enabled = false;

        _intermediaryManager.CloseAllConnections();
        
        _socket.Close();
        Log("Closed server");
    }

    private void Log(string s)
    {
        Console.WriteLine($"[Server::SteamNetworkingServer] {s}");
    }
}