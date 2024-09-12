namespace Hai.HView.Networking.Shared;

public struct HNConnection
{
    public ulong ConnectionId { get; }

    public HNConnection(ulong connectionId)
    {
        ConnectionId = connectionId;
    }
}