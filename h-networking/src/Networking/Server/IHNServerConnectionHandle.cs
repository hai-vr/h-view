using Hai.HNetworking.Shared;

namespace Hai.HNetworking.Server;

public interface IHNServerConnectionHandle : IHNSharedConnectionHandle
{
    public void AcceptConnection();
}