﻿using System.Collections.Concurrent;
using Hai.HNetworking.Client;
using Hai.HNetworking.Server;
using Hai.HNetworking.Shared;
using Hai.HNetworking.Steamworks.Client;
using Hai.HNetworking.Steamworks.Server;
using Steamworks;
using Steamworks.Data;

namespace Hai.HNetworking.Steamworks;

public class HNSteamworks
{
    public const int SearchKeyDigitCount = 3;
    public const int PasscodeDigitCount = 3;
    public const int TotalDigitCount = SearchKeyDigitCount + PasscodeDigitCount;
    public const bool NeedsSeparator = false;
    
    internal const uint ExampleAppId = 480; // https://partner.steamgames.com/doc/sdk/api/example .. https://www.youtube.com/shorts/JceP5iiTh50
    public const uint RVRAppId = 2_212_290;

    private bool _isEnabled;
    private uint _appId;

    private bool _initializedSdr;

    // Server
    private string _searchKey;
    private string _forcedDigits;
    private Lobby? _lobbyNullable;
    private HNServer _serverNullable;
    private HNSteamNetworkingServer _steamNetworkingServerNullable;
    
    // Client
    private HNClient _clientNullable;
    private HNSteamNetworkingClient _steamNetworkingClientNullable;
    
    // Public-Server
    public bool LobbyEnabled { get; private set; }
    public bool LobbyIsJoinable { get; private set; }
    
    // Public-Client
    public bool ClientEnabled { get; private set; }
    public ClientJoinError JoinError { get; set; }
    
    // Debug Lobby
    public List<DebugLobbySearch> DebugSearchLobbies { get; private set; } = new List<DebugLobbySearch>();
    public bool Refreshing { get; private set; }
    
    private readonly ConcurrentQueue<Action> _queued = new ConcurrentQueue<Action>();
    
    public void Start()
    {
    }

    public bool IsEnabled()
    {
        return _isEnabled;
    }

    public void Enable(uint appId)
    {
        if (_isEnabled) return;

        _appId = appId;
        _isEnabled = true;
        
        SteamClient.Init(appId);
    }

    public async Task Join(string joinCode)
    {
        if (ClientEnabled) return;
        if (!IsJoinCodeValid(joinCode)) return;
        
        ClientEnabled = true;
        
        Console.WriteLine($"Trying to join {joinCode}");
        
        _clientNullable = new HNClient();
        _steamNetworkingClientNullable = new HNSteamNetworkingClient(_clientNullable);
        _steamNetworkingClientNullable.OnDisconnected += () =>
        {
            _steamNetworkingClientNullable = null;
            _clientNullable = null;
            ClientEnabled = false;
        };

        var key = joinCode.Substring(0, PasscodeDigitCount);
        Console.WriteLine($"Searching for {key}");
        
        var resultsMatchingKey = await SearchFor(key);
        Console.WriteLine($"Found {resultsMatchingKey.Count} results for {key}");
        
        if (resultsMatchingKey.Count != 1)
        {
            Console.WriteLine($"Incorrect number of results found to join {joinCode} ({resultsMatchingKey.Count})");
            JoinError = resultsMatchingKey.Count == 0 ? ClientJoinError.CannotFindLobby : ClientJoinError.FoundTooManyLobbies;
            ClientEnabled = false;
            return;
        }

        var serverId = resultsMatchingKey[0].Id;
        Console.WriteLine($"Joining {serverId} with {joinCode}");
        _steamNetworkingClientNullable.Join(serverId, joinCode);
    }

    private static bool IsJoinCodeValid(string joinCode)
    {
        if (joinCode.Length != TotalDigitCount) return false;
        
        var isParseable = int.TryParse(joinCode, out var number);
        if (!isParseable) return false;
        
        var minValue = (int)Math.Pow(10, TotalDigitCount - 1);
        var maxValue = (int)Math.Pow(10, TotalDigitCount) - 1;
        return number >= minValue && number <= maxValue;
    }

    public async Task CreateLobby()
    {
        if (LobbyEnabled) return;

        if (_serverNullable == null)
        {
            CreateServer();
            _steamNetworkingServerNullable.Enable();
        }
        
        LobbyEnabled = true;

        var lobby = (await SteamMatchmaking.CreateLobbyAsync(128)).Value;
        _lobbyNullable = lobby;
        
        _searchKey = DeriveSearchKey(lobby);
        _forcedDigits = NewPasscode();
        
        lobby.SetData("HV_IsHV", "1");
        lobby.SetData("HV_Protocol", $"{HNProtocol.HandshakeProtocolVersion}");
        lobby.SetData("HV_LobbyId", $"{lobby.Id}");
        lobby.SetData("HV_SearchKey", _searchKey);
        
        // https://partner.steamgames.com/doc/api/ISteamMatchmaking#k_ELobbyTypeInvisible
        // Invisible:
        // Returned by search, but not visible to other friends.
        // This is useful if you want a user in two lobbies, for example matching groups together. A user can be in only one regular lobby, and up to two invisible lobbies.
        lobby.SetInvisible();

        LobbyIsJoinable = true;

        if (true)
        {
            ClientEnabled = true;
            
            _clientNullable = new HNClient();
            _steamNetworkingClientNullable = new HNSteamNetworkingClient(_clientNullable);
            _steamNetworkingClientNullable.OnDisconnected += () =>
            {
                _steamNetworkingClientNullable = null;
                _clientNullable = null;
                ClientEnabled = false;
            };
            try
            {
                _steamNetworkingClientNullable.Join(SteamClient.SteamId, _searchKey + _forcedDigits);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }

    private void CreateServer()
    {
        if (_serverNullable != null) return;

        _serverNullable = new HNServer();
        
        WillNeedSDR();
        _steamNetworkingServerNullable = new HNSteamNetworkingServer(_serverNullable);
    }

    public void WillNeedSDR()
    {
        if (_initializedSdr) return;
            
        SteamNetworkingUtils.InitRelayNetworkAccess();
        _initializedSdr = true;
    }

    public string LobbyShareable()
    {
        return $"HV-{_searchKey}{(NeedsSeparator ? "-" : "")}{_forcedDigits}";
    }

    private static string DeriveSearchKey(Lobby it)
    {
        // Lobbies cannot have passwords, and we can't ask Steam for 6-digit or other unique secret number.
        // Since we cannot do much about this, we're going to derive a random number out of the Lobby ID
        // that Steam gives us (which I call "search key").
        //
        // This random number is public (easily discoverable through the Steamworks Matchmaking API),
        // so it only serves as a way for other users to search for this specific number, but also as a weak way
        // to ensure that other users can't trivially create a lobby with the same search key, since it is derived
        // from a piece of information given to us by Steam.
        //
        // There is indeed a risk of collisions in the generated ID, and possibly a way to force it by generating lots
        // of lobbies, but probably not so trivial to do at the usage scale of this.
        //
        // Simply use a random seed. If we ever need something stronger, we might need some actual uniform
        // hashing functions, and a longer search key, or just host a real server.
        var rand = new Random((int)(it.Id.Value % int.MaxValue));
        var minValue = (int)Math.Pow(10, SearchKeyDigitCount - 1);
        var maxValue = (int)Math.Pow(10, SearchKeyDigitCount) - 1;
        var generated = rand.Next(minValue, maxValue);
        return $"{generated}";
    }

    private static string NewPasscode()
    {
        // The last N=`PasscodeDigitCount` digits that the owner shares with other users is actually the passcode which is transmitted to
        // the room owner after the connection is established.
        //
        // The joining user should still transmit the lobby ID or the search key that lead to opening the connection,
        // in order to know where they're coming from, or support multiple lobbies.
        var maxValue = (int)Math.Pow(10, PasscodeDigitCount) - 1;
        var randomDigits09999 = new Random().Next(0, maxValue);
        
        var joiner = string.Join("", Enumerable.Repeat("0", PasscodeDigitCount));
        var format = "{0:" + joiner + "}";
        var forcedDigits = string.Format(format, randomDigits09999);
        return forcedDigits;
    }

    public void TerminateServer()
    {
        if (LobbyIsJoinable)
        {
            _steamNetworkingServerNullable.Disable();
            
            _lobbyNullable?.Leave();
            _lobbyNullable = null;
            LobbyIsJoinable = false;
            LobbyEnabled = false;

            _serverNullable = null;
            _steamNetworkingServerNullable = null;
        }
    }

    public async Task RefreshLobbies()
    {
        if (Refreshing) return;
        Refreshing = true;
        DebugSearchLobbies = await SearchFor(null);
        
        Refreshing = false;
    }

    private static async Task<List<DebugLobbySearch>> SearchFor(string specificKeyOptional)
    {
        var query = SteamMatchmaking.LobbyList.WithEqual("HV_IsHV", 1);
        if (specificKeyOptional != null)
        {
            query = query.WithKeyValue("HV_SearchKey", specificKeyOptional);
        }
        
        var results = await query.RequestAsync();
        return results != null
            ? results
                .Where(IsLobbySearchKeyValid)
                .Where(lobby =>
                {
                    if (specificKeyOptional == null) return true;
                    
                    // Network-defensive:
                    // As a safety, double-check the results (i.e. if results are somehow inconsistent with the initial search request)
                    return lobby.GetData("HV_SearchKey") == specificKeyOptional;
                })
                .Select(lobby => new DebugLobbySearch
                {
                    Id = lobby.Id,
                    OwnerName = lobby.Owner.Name,
                    SearchKey = lobby.GetData("HV_SearchKey")
                }).ToList()
            : new List<DebugLobbySearch>();
    }

    private static bool IsLobbySearchKeyValid(Lobby lobby)
    {
        return DeriveSearchKey(lobby) == lobby.GetData("HV_SearchKey");
    }

    public void Enqueue(Action action)
    {
        _queued.Enqueue(action);
    }

    public void Update()
    {
        while (_queued.TryDequeue(out var action)) action();
        
        if (_steamNetworkingServerNullable != null) _steamNetworkingServerNullable.Update();
        if (_steamNetworkingClientNullable != null) _steamNetworkingClientNullable.Update();
    }
}

public enum ClientJoinError
{
    CannotFindLobby,
    FoundTooManyLobbies
}

public struct DebugLobbySearch
{
    public SteamId Id;
    public string OwnerName;
    public string SearchKey;
}