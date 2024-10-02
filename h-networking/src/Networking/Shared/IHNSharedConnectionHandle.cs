namespace Hai.HNetworking.Shared;

public interface IHNSharedConnectionHandle
{
    public HNSteamIdentity Identity { get; }

    public void KillConnection();

    void Send(ArraySegment<byte> reliableData, HNSendType sendType);

    /// Send the data, which must be received in order.
    /// If the transport layer allows it, it may bundle several messages together and send them all together.
    void SendReliable(ArraySegment<byte> reliableData);

    /// Send the data, which must be received in order.
    /// If the transport layer allows it, it should send the data as soon as possible, bypassing bundling techniques.
    void SendReliableImmediate(ArraySegment<byte> reliableImmediateData);

    /// Send the data, which may be lost, discarded, or received in a different order.
    /// If the transport layer allows it, it may bundle several messages together and send them all together.
    void SendUnreliable(ArraySegment<byte> unreliableData);

    /// Send the data, which may be lost, discarded, or received in a different order.
    /// If the transport layer allows it, it should send the data as soon as possible, bypassing bundling techniques.
    void SendUnreliableImmediate(ArraySegment<byte> unreliableImmediateData);
}

[Flags]
public enum HNSendType
{
    Unreliable = 0,
    NoNagle = 1,
    NoDelay = 4,
    Reliable = 8,
}