using Hai.HView.Networking.ConnectionResolver;
using Hai.HView.Networking.Shared;

namespace Hai.HView.Networking.Client;

public class HNClient
{
    private readonly HNSharedConnectionResolver _connectionResolver = new HNSharedConnectionResolver();

    // private IEnumerable<HNSharedService> AllServices => services.Concat(sharedServices);

    private HNConnection _connection;
    private bool _connected;
    private string _joinCode = "09999999";

    public void PostReceive()
    {
        if (!_connected) return;
            
        // foreach (var service in AllServices)
        // {
            // Gate(() => service.PostReceive());
        // }
    }

    public void OnMessage(IHNClientConnectionHandle handle, HNMessage message)
    {
    }

    private void DispatchToService(IHNClientConnectionHandle handle, object networkMessage)
    {
        // foreach (var service in AllServices)
        // {
            // if (service.CanHandle(networkMessage.GetType()))
            // {
                // service.DispatchNetworkMessage(handle, networkMessage);
                // return;
            // }
        // }
    }

    public void OnConnected(IHNClientConnectionHandle handle)
    {
        _connected = true;
        _connection = _connectionResolver.Register(handle);
        // foreach (var service in AllServices)
        // {
            // Gate(() => service.OnConnected(handle));
        // }
    }

    public void OnDisconnected(IHNClientConnectionHandle handle)
    {
        if (_connectionResolver.HasConnectionFor(handle))
        {
            // It's possible not to have a connection if the disconnect happens before the connect
            // foreach (var service in AllServices)
            // {
                // Gate(() => service.OnDisconnected(handle));
            // }
            _connectionResolver.Unregister(handle);
        }
        _connected = false;
    }

    private void LogRecord(string msg)
    {
        Console.WriteLine(msg);
    }

    private void Gate(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public void SetJoinCode(string joinCode)
    {
        _joinCode = joinCode;
    }
}