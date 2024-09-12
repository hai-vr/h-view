namespace Hai.HView.Networking.Shared;

public class HNSteamIdentity
{
    public ulong SteamId { get; }
    public bool IsAuthenticated { get; }
    public string Address { get; }

    public HNSteamIdentity(ulong steamSteamId, string address = "")
    {
        SteamId = steamSteamId;
        IsAuthenticated = steamSteamId != 0;
        Address = address;
    }

    public static HNSteamIdentity NotSteam(string address)
    {
        return new HNSteamIdentity(0, address);
    }

    public static HNSteamIdentity Irrelevant()
    {
        return new HNSteamIdentity(0, "Placeholder");
    }

    public string DebugDisplay()
    {
        return IsAuthenticated ? $"SteamID::[{SteamId}]" : $"Address::[{Address}]";
    }
}