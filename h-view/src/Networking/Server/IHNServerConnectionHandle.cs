using Hai.HView.Networking.Shared;

namespace Hai.HView.Networking.Server;

public interface IHNServerConnectionHandle : IHNSharedConnectionHandle
{
    public void AcceptConnection();
}