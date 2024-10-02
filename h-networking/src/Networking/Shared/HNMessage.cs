namespace Hai.HNetworking.Shared;

public class HNMessage
{
    public long recvTime;
    public long messageNum;
    public ArraySegment<byte> segment;
}