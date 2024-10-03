using System.Numerics;
using Hai.HView.Core;
using Hai.HView.OSC;
using Hai.HView.Ui;
using ImGuiNET;

namespace Hai.HView.Gui.Tab;

public class UiCostumes
{
    private readonly HVInnerWindow _inner;
    private readonly HVRoutine _routine;
    private readonly UiScrollManager _scrollManager;
    private readonly bool _noLogin;
    
    private readonly Vector2 _portraitSize = new Vector2(256, 768);

    private const uint MaxLength = 10_000;

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
    }

    internal void CostumesTab(Dictionary<string, HOscItem> oscMessages)
    {
        var uiExternalService = _routine.UiExternalService();
        var currentAvi = oscMessages.TryGetValue(CommonOSCAddresses.AvatarChangeOscAddress, out var c) ? (string)c.Values[0] : "";

        if (!uiExternalService.IsLoggedIn)
        {
            ImGui.SeparatorText(HLocalizationPhrase.LoginToVrchatLabel);
            LoginScreen(uiExternalService);
            ImGui.Text("");
        }
        
        ImGui.BeginTabBar("##tabs_costumes");
        _scrollManager.MakeTab(HLocalizationPhrase.SelectTabLabel, () => Costumes(uiExternalService, currentAvi));
        _scrollManager.MakeTab(HLocalizationPhrase.SwitchTabLabel, () => SwitchAvatar(uiExternalService, currentAvi));
        if (uiExternalService.IsLoggedIn) _scrollManager.MakeTab(HLocalizationPhrase.SignInTabLabel, () => LoginScreen(uiExternalService));
        ImGui.EndTabBar();
    }

    private void SwitchAvatar(HVExternalService uiExternalService, string currentAvi)
    {
        ImGui.SeparatorText(HLocalizationPhrase.CurrentAvatarLabel);
        ImGui.Text(currentAvi);
        if (ImGui.Button(HLocalizationPhrase.CopyLabel))
        {
            ImGui.SetClipboardText(currentAvi);
        }
        ImGui.SameLine();
        if (ImGui.Button(HLocalizationPhrase.OpenBrowserLabel))
        {
            UiUtil.OpenAvatarUrl(currentAvi);
        }
        ImGui.Text("");
        
        ImGui.SeparatorText(HLocalizationPhrase.SwitchAvatarLabel);
        
        ImGui.BeginDisabled(uiExternalService.IsProcessingSwitchAvatar);
        ImGui.InputText($"{HLocalizationPhrase.AvatarIdLabel}##avatarid.input", ref _avatarIdBuffer, MaxLength);
        if (ImGui.Button(HLocalizationPhrase.SwitchAvatarLabel))
        {
            _routine.EjectUserFromCostumeMenu();
            var userinput_avatarId = _avatarIdBuffer;
            uiExternalService.SelectAvatar(userinput_avatarId);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.Text($"{HLocalizationPhrase.StatusLabel}: " + Enum.GetName(uiExternalService.SwitchStatus));
        ImGui.Text("");
    }

    private void Costumes(HVExternalService uiExternalService, string currentAvi)
    {
        var costumes = _routine.GetCostumes();

        var acc = 0f;
        for (var index = 0; index < costumes.Length; index++)
        {
            var costume = costumes[index];
            DrawAviButton(costume.AvatarId, costume.FullPath, uiExternalService, currentAvi);
            acc += _portraitSize.X + 6 * 2 + 12;
            if (index != costumes.Length - 1)
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
        if (_inner.HapticImageButton($"###switch_{avatarId}", _inner.GetOrLoadImage(pngPath), _portraitSize))
        {
            _routine.EjectUserFromCostumeMenu();
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
            ImGui.Text(HLocalizationPhrase.MsgNoVrNotLoggedIn);
            return;
        }
        
        if (!uiExternalService.IsLoggedIn)
        {
            if (!uiExternalService.NeedsTwofer)
            {
                ImGui.BeginDisabled(uiExternalService.IsProcessingLogin);
                ImGui.InputText($"{HLocalizationPhrase.UsernameLabel}##username.input", ref _accountNameBuffer__sensitive, MaxLength, ImGuiInputTextFlags.Password);
                ImGui.InputText($"{HLocalizationPhrase.PasswordLabel}##password.input", ref _accountPasswordBuffer__sensitive, MaxLength, ImGuiInputTextFlags.Password);
                if (ImGui.Button(HLocalizationPhrase.LoginLabel))
                {
                    var userinput_username__sensitive = _accountNameBuffer__sensitive;
                    var userinput_password__sensitive = _accountPasswordBuffer__sensitive;

                    uiExternalService.Login(userinput_username__sensitive, userinput_password__sensitive);
                }
                ImGui.EndDisabled(); 
            }
            else
            {
                MakePasswordFieldEmpty();
                
                ImGui.BeginDisabled(uiExternalService.IsProcessingLogin);
                ImGui.Text(HLocalizationPhrase.MsgMultifactorCheckEmails);
                ImGui.InputText($"{HLocalizationPhrase.MultifactorCodeLabel}##twofer.input", ref _twoferBuffer__sensitive, MaxLength);
                if (ImGui.Button(HLocalizationPhrase.SubmitCodeLabel))
                {
                    var userinput_twoferCode__sensitive = _twoferBuffer__sensitive;

                    uiExternalService.VerifyTwofer(userinput_twoferCode__sensitive);
                }
                ImGui.EndDisabled(); 
            }
            ImGui.SameLine();
            ImGui.Text($"{HLocalizationPhrase.StatusLabel}: {Enum.GetName(uiExternalService.LoginStatus)}");
            if (uiExternalService.NeedsTwofer)
            {
                ImGui.SameLine();
                ImGui.Text($"by {Enum.GetName(uiExternalService.TwoferMethod)}");
            }
        }
        else
        {
            MakePasswordFieldEmpty();

            ImGui.Text(HLocalizationPhrase.MsgLoggedIn);
            ImGui.TextWrapped(string.Format(HLocalizationPhrase.MsgCookieSaveLocation, HVExternalService.NewCookieFile));
            ImGui.TextWrapped(HLocalizationPhrase.MsgLogoutToDeleteTheseCookies);
            if (ImGui.Button(HLocalizationPhrase.LogoutLabel))
            {
                uiExternalService.Logout();
            }
            ImGui.SameLine();
            ImGui.Text($"{HLocalizationPhrase.StatusLabel}: {Enum.GetName(uiExternalService.LogoutStatus)}");
        }
    }

    private void MakePasswordFieldEmpty()
    {
        _accountPasswordBuffer__sensitive = "";
    }
}