using System.Numerics;
using Hai.HView.Core;
using Hai.HView.OSC;
using Hai.HView.Ui;
using Hai.HView.VRCLogin;
using ImGuiNET;

namespace Hai.HView.Gui.Tab;

public class UiCostumes
{
    private readonly HVInnerWindow _inner;
    private readonly HVRoutine _routine;
    private readonly UiScrollManager _scrollManager;
    private readonly bool _noLogin;
    private readonly string[] _files;
    private readonly string[] _fileNames;
    
    private readonly Vector2 _portraitSize = new Vector2(256, 768);

    private const string LoginLabel = "Login";
    private const string SubmitCodeLabel = "Submit code";
    private const string SwitchAvatarLabel = "Switch avatar";
    private const string LoginToVrchatLabel = "Login to VRChat";
    private const string LoggedInMsg = "You are logged in.";
    private const string CurrentAvatarLabel = "Current avatar";
    private const uint MaxLength = 10_000;
    private const string LogoutLabel = "Logout";

    private string _accountNameBuffer__sensitive = "";
    private string _accountPasswordBuffer__sensitive = "";
    private string _twoferBuffer__sensitive = "";
    private string _avatarIdBuffer = "";

    public UiCostumes(HVInnerWindow inner, HVRoutine routine, UiScrollManager scrollManager, bool noLogin)
    {
        _inner = inner;
        _routine = routine;
        _scrollManager = scrollManager;
        _noLogin = noLogin;
        _files = HVRoutine.GetFilesInLocalLowVRChatDirectories("avtr_*.png");
        _fileNames = _files.Select(Path.GetFileNameWithoutExtension).ToArray();
    }

    internal void CostumesTab(Dictionary<string, HOscItem> oscMessages)
    {
        var uiExternalService = _routine.UiExternalService();
        var currentAvi = oscMessages.TryGetValue("/avatar/change", out var c) ? (string)c.Values[0] : "";

        if (!uiExternalService.IsLoggedIn)
        {
            ImGui.SeparatorText(LoginToVrchatLabel);
            LoginScreen(uiExternalService);
            ImGui.Text("");
        }
        
        ImGui.BeginTabBar("##tabs_costumes");
        _scrollManager.MakeTab("Costumes", () => Costumes(uiExternalService, currentAvi));
        _scrollManager.MakeTab("Switch", () => SwitchAvatar(uiExternalService, currentAvi));
        if (uiExternalService.IsLoggedIn) _scrollManager.MakeTab("Login", () => LoginScreen(uiExternalService));
        ImGui.EndTabBar();
    }

    private void SwitchAvatar(HVExternalService uiExternalService, string currentAvi)
    {
        ImGui.SeparatorText(CurrentAvatarLabel);
        ImGui.Text(currentAvi);
        if (ImGui.Button("Copy"))
        {
            ImGui.SetClipboardText(currentAvi);
        }
        ImGui.SameLine();
        if (ImGui.Button(HVInnerWindow.OpenBrowserLabel))
        {
            UiUtil.OpenAvatarUrl(currentAvi);
        }
        ImGui.Text("");
        
        ImGui.SeparatorText(SwitchAvatarLabel);
        
        ImGui.BeginDisabled(uiExternalService.IsProcessingSwitchAvatar);
        ImGui.InputText("Avatar ID##avatarid.input", ref _avatarIdBuffer, MaxLength);
        if (ImGui.Button(SwitchAvatarLabel))
        {
            var userinput_avatarId = _avatarIdBuffer;
            uiExternalService.SelectAvatar(userinput_avatarId);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.Text("Status: " + Enum.GetName(uiExternalService.SwitchStatus));
        ImGui.Text("");
    }

    private void Costumes(HVExternalService uiExternalService, string currentAvi)
    {
        var acc = 0f;
        for (var i = 0; i < _files.Length; i++)
        {
            DrawAviButton(_fileNames[i], _files[i], uiExternalService, currentAvi);
            acc += _portraitSize.X + 6 * 2 + 12;
            if (i != _files.Length - 1)
            {
                if (acc < ImGui.GetWindowWidth() - _portraitSize.X)
                {
                    ImGui.SameLine();
                }
                else
                {
                    acc = 0;
                }
            }
        }
    }

    private void DrawAviButton(string avatarId, string pngPath, HVExternalService uiExternalService, string currentAvi)
    {
        if (avatarId == currentAvi) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 1, 1, 0.75f));
        if (ImGui.ImageButton($"###switch_{avatarId}", _inner.GetOrLoadImage(pngPath), _portraitSize))
        {
            uiExternalService.SelectAvatar(avatarId);
        }
        if (avatarId == currentAvi) ImGui.PopStyleColor();
    }

    /// I hate this
    /// <param name="uiExternalService"></param>
    private void LoginScreen(HVExternalService uiExternalService)
    {
        if (_noLogin && !uiExternalService.IsLoggedIn)
        {
            ImGui.Text("HaiView is not logged into your VRChat account. To log in, open this tab on the desktop window.");
            return;
        }
        
        if (!uiExternalService.IsLoggedIn)
        {
            if (!uiExternalService.NeedsTwofer)
            {
                ImGui.BeginDisabled(uiExternalService.IsProcessingLogin);
                ImGui.InputText("Username##username.input", ref _accountNameBuffer__sensitive, MaxLength, ImGuiInputTextFlags.Password);
                ImGui.InputText("Password##password.input", ref _accountPasswordBuffer__sensitive, MaxLength, ImGuiInputTextFlags.Password);
                if (ImGui.Button(LoginLabel))
                {
                    var userinput_username__sensitive = _accountNameBuffer__sensitive;
                    var userinput_password__sensitive = _accountPasswordBuffer__sensitive;

                    uiExternalService.Login(userinput_username__sensitive, userinput_password__sensitive);
                }
                ImGui.EndDisabled(); 
            }
            else
            {
                ImGui.BeginDisabled(uiExternalService.IsProcessingLogin);
                ImGui.Text("Check your email for a 2FA code.");
                ImGui.InputText("2FA Code##twofer.input", ref _twoferBuffer__sensitive, MaxLength);
                if (ImGui.Button(SubmitCodeLabel))
                {
                    var userinput_twoferCode__sensitive = _twoferBuffer__sensitive;

                    uiExternalService.VerifyTwofer(userinput_twoferCode__sensitive);
                }
                ImGui.EndDisabled(); 
            }
            ImGui.SameLine();
            ImGui.Text($"Status: {Enum.GetName(uiExternalService.LoginStatus)}");
            if (uiExternalService.NeedsTwofer)
            {
                ImGui.SameLine();
                ImGui.Text($"by {Enum.GetName(uiExternalService.TwoferMethod)}");
            }
        }
        else
        {
            ImGui.Text(LoggedInMsg);
            ImGui.TextWrapped($"Cookies have been saved in {HVExternalService.CookieFile}. Logout to delete these cookies.");
            if (ImGui.Button(LogoutLabel))
            {
                uiExternalService.Logout();
            }
            ImGui.SameLine();
            ImGui.Text($"Status: {Enum.GetName(uiExternalService.LogoutStatus)}");
        }
    }
}