using Hai.HView.Networking.Server;
using Steamworks;

namespace Hai.HView.Networking.Steamworks.Server;

public class HNSteamNetworkingServer
{
    private readonly HNServer _server;
    
    private bool _enabled;
    private SocketManager _socket;

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
        _socket.Interface = new HNSteamNetworkingSocketManager(_server);
    }

    public void Disable()
    {
        if (!_enabled) return;
        _enabled = false;
        
        _socket.Close();
    }
}