using System.Numerics;
using Hai.HNetworking.Steamworks;
using Hai.HView.Core;
using ImGuiNET;

namespace Hai.HView.Ui;

public class UiNetworking
{
    private readonly HVRoutine _routine;
    private const string StartServerLabel = "Start server";
    private const string StopServerLabel = "Stop server";
    
    private readonly HNSteamworks _steamworks;
    private string _joinCode = "";

    public UiNetworking(HVRoutine routine)
    {
        if (!ConditionalCompilation.IncludesSteamworks) throw new InvalidOperationException("Instances of UiNetworking should not be created when Steamworks is disabled in conditional compilation.");
        
        _routine = routine;
        _steamworks = routine.SteamworksModule();
    }

    public void NetworkingTab()
    {
        if (_steamworks.IsEnabled())
        {
            IfEnabled();
        }
        else
        {
            var appId = HNSteamworks.RVRAppId;
            if (ImGui.Button($"Enable Steamworks"))
            {
                _steamworks.Enable(appId);
            }

            ImGui.Text($"Steamworks will use AppId {appId}.");
            ImGui.TextWrapped("Privacy: By enabling Steamworks, other users may be able to discover your Steam account.");
        }
    }

    private void IfEnabled()
    {
        ImGui.SeparatorText("Create server");
        ImGui.BeginDisabled(_steamworks.LobbyEnabled);
        if (ImGui.Button(StartServerLabel))
        {
            _steamworks.Enqueue(() => _ = _steamworks.CreateLobby());
        }
        ImGui.EndDisabled();
        ImGui.BeginDisabled(!_steamworks.LobbyIsJoinable);
        ImGui.SameLine();
        if (ImGui.Button(StopServerLabel))
        {
            _steamworks.Enqueue(() => _steamworks.TerminateServer());
        }
        ImGui.EndDisabled();
        if (_steamworks.LobbyIsJoinable)
        {
            ImGui.SameLine();
            ImGui.Text($"Ask other users to join: {_steamworks.LobbyShareable()}");
            ImGui.SameLine();
        
            if (ImGui.Button("Send in chatbox"))
            {
                _routine.SendChatMessage($"Join my lobby: {_steamworks.LobbyShareable()}");
            }
        }
        
        ImGui.SeparatorText("Join server");
        if (!_steamworks.LobbyEnabled)
        {
            DisplayCode(_joinCode);
            ImGui.SameLine();
        
            ImGui.BeginDisabled(_joinCode.Length < HNSteamworks.TotalDigitCount || _steamworks.ClientEnabled);
            if (ImGui.Button("Join", new Vector2(64, 32)))
            {
                _steamworks.Enqueue(() => _ = _steamworks.Join(_joinCode));
            }
            ImGui.EndDisabled();
        
            ImGui.Indent();
            JoincodeNumpad();
            ImGui.Unindent();
        }
        else
        {
            ImGui.TextWrapped("Cannot join when hosting a server.");
        }
        
        
        ImGui.SeparatorText("Debug Lobbies");
        ImGui.BeginDisabled(_steamworks.Refreshing);
        if (ImGui.Button("Refresh lobbies"))
        {
            _steamworks.Enqueue(() => _ = _steamworks.RefreshLobbies());
        }
        ImGui.EndDisabled();

        var copy = _steamworks.DebugSearchLobbies.ToArray();
        foreach (var searchLobby in copy)
        {
            ImGui.Text($"({searchLobby.SearchKey}...) {searchLobby.Id} {searchLobby.OwnerName}");
        }
    }

    private void DisplayCode(string code)
    {
        ImGui.BeginDisabled();
        ImGui.Dummy(new Vector2(2, 32));
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        ImGui.Button("HV-", new Vector2(32, 32));
        ImGui.PopStyleColor();
        for (var i = 0; i < HNSteamworks.TotalDigitCount; i++)
        {
            var num = i < code.Length ? $"{code[i]}###n{i}" : "";
            ImGui.SameLine();
            ImGui.Button(num, new Vector2(24, 32));
            if (HNSteamworks.NeedsSeparator && i == HNSteamworks.SearchKeyDigitCount - 1)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                ImGui.Button("-", new Vector2(16, 32));
                ImGui.PopStyleColor();
            }
        }
        ImGui.EndDisabled();
    }

    private void JoincodeNumpad()
    {
        ImGui.BeginDisabled(_joinCode.Length >= HNSteamworks.TotalDigitCount);
        for (var i = 0; i < 10; i++)
        {
            var n = (i + 1) % 10;
            if (i % 3 != 0) ImGui.SameLine();
            if (n == 0)
            {
                ImGui.Dummy(new Vector2(32, 32));
                ImGui.SameLine();
            }
            if (ImGui.Button($"{n}", new Vector2(32, 32)))
            {
                _joinCode += $"{n}";
                
                // Since we're typing a code, we'll probably need SDR. Prepare it
                _steamworks.WillNeedSDR();
            }
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(_joinCode.Length == 0);
        if (ImGui.Button("<", new Vector2(32, 32)))
        {
            _joinCode = _joinCode.Substring(0, _joinCode.Length - 1);
        }
        ImGui.EndDisabled();
    }
}