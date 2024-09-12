using Hai.HView.Networking.Shared;

namespace Hai.HView.Networking.ConnectionResolver;

public class HNSharedConnectionResolver
{
    private readonly Dictionary<HNConnection, IHNSharedConnectionHandle> _connectionToHandle = new();
    private readonly Dictionary<IHNSharedConnectionHandle, HNConnection> _handleToConnection = new();
    private ulong nextId = 1;

    public HNConnection Register(IHNSharedConnectionHandle handle)
    {
        var connection = new HNConnection(nextId++);
        _connectionToHandle.Add(connection, handle);
        _handleToConnection.Add(handle, connection);
        return connection;
    }

    public void Unregister(IHNSharedConnectionHandle handle)
    {
        var connection = _handleToConnection[handle];
        _connectionToHandle.Remove(connection);
        _handleToConnection.Remove(handle);
    }
        
    public IHNSharedConnectionHandle HandleFor(HNConnection connection)
    {
        return _connectionToHandle[connection];
    }
        
    public HNConnection ConnectionFor(IHNSharedConnectionHandle handle)
    {
        return _handleToConnection[handle];
    }
        
    public bool HasConnectionFor(IHNSharedConnectionHandle handle)
    {
        return _handleToConnection.ContainsKey(handle);
    }
}