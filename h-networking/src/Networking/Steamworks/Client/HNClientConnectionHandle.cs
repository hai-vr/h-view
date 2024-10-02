using Hai.HNetworking.Client;
using Hai.HNetworking.Shared;
using Steamworks.Data;

namespace Hai.HNetworking.Steamworks.Client;

public class HNClientConnectionHandle : IHNClientConnectionHandle
{
    private Connection _connection;

    public HNClientConnectionHandle(Connection connection, HNSteamIdentity identity)
    {
        Identity = identity;
        _connection = connection;
    }

    public HNSteamIdentity Identity { get; }

    public void KillConnection()
    {
        _connection.Close();
    }

    public void Send(ArraySegment<byte> reliableData, HNSendType sendType)
    {
        _connection.SendMessage(reliableData.Array, 0, reliableData.Count, (SendType)sendType);
    }

    public void SendReliable(ArraySegment<byte> reliableData)
    {
        // ReSharper disable once RedundantArgumentDefaultValue
        _connection.SendMessage(reliableData.Array, 0, reliableData.Count, SendType.Reliable);
    }

    public void SendReliableImmediate(ArraySegment<byte> reliableImmediateData)
    {
        _connection.SendMessage(reliableImmediateData.Array, 0, reliableImmediateData.Count, SendType.Reliable | SendType.NoNagle);
    }

    public void SendUnreliable(ArraySegment<byte> unreliableData)
    {
        _connection.SendMessage(unreliableData.Array, 0, unreliableData.Count, SendType.Unreliable);
    }

    public void SendUnreliableImmediate(ArraySegment<byte> unreliableImmediateData)
    {
        _connection.SendMessage(unreliableImmediateData.Array, 0, unreliableImmediateData.Count, SendType.Unreliable | SendType.NoNagle);
    }
}