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
    private readonly UiScrollManager _scrollManager;
    private readonly string[] _files;
    private readonly string[] _fileNames;

    private readonly HVVrcSession _vrcSession = new HVVrcSession(); // FIXME: This should be in Routine, or a separate class. Not in UI
    
    private Task<HVVrcSession.LoginResponse> _loginTaskNullable;
    private HVVrcSession.LoginResponseStatus _loginStatus = HVVrcSession.LoginResponseStatus.Unresolved;
    private bool _needsTwofer;
    private HVVrcSession.TwoferMethod _twoferMethod = HVVrcSession.TwoferMethod.Other;
    
    private Task<HVVrcSession.SwitchAvatarResponseStatus> _selectingAvatarTaskNullable;
    private HVVrcSession.SwitchAvatarResponseStatus _switchResult;
    private readonly Vector2 _portraitSize = new Vector2(256, 768);

    private const string LoginLabel = "Login";
    private const string SubmitCodeLabel = "Submit code";
    private const string SwitchAvatarLabel = "Switch avatar";
    private const string LoginToVrchatLabel = "Login to VRChat";
    private const string LoggedInMsg = "You are logged in.";
    private const string CostumesLabel = "Costumes";
    private const string CurrentAvatarLabel = "Current avatar";
    private const uint MaxLength = 10_000;
    
    private string _accountNameBuffer__sensitive = "";
    private string _accountPasswordBuffer__sensitive = "";
    private string _twoferBuffer__sensitive = "";
    private string _avatarIdBuffer = "";

    public UiCostumes(HVInnerWindow inner, UiScrollManager scrollManager)
    {
        _inner = inner;
        _scrollManager = scrollManager;
        _files = HVRoutine.GetFilesInLocalLowVRChatDirectories("avtr_*.png");
        _fileNames = _files.Select(Path.GetFileNameWithoutExtension).ToArray();
    }

    internal void CostumesTab(Dictionary<string, HOscItem> oscMessages)
    {
        var currentAvi = oscMessages.TryGetValue("/avatar/change", out var c) ? (string)c.Values[0] : "";

        if (_loginStatus != HVVrcSession.LoginResponseStatus.Success)
        {
            ImGui.SeparatorText(LoginToVrchatLabel);
            LoginScreen();
            ImGui.Text("");
        }
        
        ImGui.BeginTabBar("##tabs_costumes");
        _scrollManager.MakeTab("Costumes", () => Costumes(currentAvi));
        _scrollManager.MakeTab("Switch", () => SwitchAvatar(currentAvi));
        if (_loginStatus == HVVrcSession.LoginResponseStatus.Success) _scrollManager.MakeTab("Login", LoginScreen);
        ImGui.EndTabBar();

        // TODO: This should be on the Routine side.
        EvalTasks();
    }

    private void SwitchAvatar(string currentAvi)
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
        
        ImGui.BeginDisabled(_selectingAvatarTaskNullable != null);
        ImGui.InputText("Avatar ID##avatarid.input", ref _avatarIdBuffer, MaxLength);
        if (ImGui.Button(SwitchAvatarLabel))
        {
            var userinput_avatarId = _avatarIdBuffer;
            _selectingAvatarTaskNullable = _vrcSession.SelectAvatar(userinput_avatarId);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.Text("Status: " + Enum.GetName(_switchResult));
        ImGui.Text("");
    }

    private void Costumes(string currentAvi)
    {
        ImGui.SeparatorText(CostumesLabel);
        var acc = 0f;
        for (var i = 0; i < _files.Length; i++)
        {
            DrawAviButton(_fileNames[i], _files[i], currentAvi);
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

    private void DrawAviButton(string avatarId, string pngPath, string currentAvi)
    {
        if (avatarId == currentAvi) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 1, 1, 0.75f));
        if (ImGui.ImageButton($"###switch_{avatarId}", _inner.GetOrLoadImage(pngPath), _portraitSize))
        {
            _selectingAvatarTaskNullable = _vrcSession.SelectAvatar(avatarId);
        }
        if (avatarId == currentAvi) ImGui.PopStyleColor();
    }

    private void EvalTasks()
    {
        if (_loginTaskNullable != null && _loginTaskNullable.IsCompleted)
        {
            if (_loginTaskNullable.IsCompletedSuccessfully)
            {
                var result = _loginTaskNullable.Result;
                _loginStatus = result.Status;
                if (result.Status == HVVrcSession.LoginResponseStatus.RequiresTwofer)
                {
                    _needsTwofer = true;
                    _twoferMethod = result.TwoferMethod;
                }
                else if (result.Status == HVVrcSession.LoginResponseStatus.Success)
                {
                    _needsTwofer = false;
                    _accountPasswordBuffer__sensitive = "";
                }
            }
            _loginTaskNullable = null;
        }

        if (_selectingAvatarTaskNullable != null && _selectingAvatarTaskNullable.IsCompleted)
        {
            if (_selectingAvatarTaskNullable.IsCompletedSuccessfully)
            {
                _switchResult = _selectingAvatarTaskNullable.Result;
            }

            _selectingAvatarTaskNullable = null;
        }
    }

    /// I hate this
    private void LoginScreen()
    {
        if (_loginStatus != HVVrcSession.LoginResponseStatus.Success)
        {
            if (!_needsTwofer)
            {
                ImGui.BeginDisabled(_loginTaskNullable != null);
                ImGui.InputText("Username##username.input", ref _accountNameBuffer__sensitive, MaxLength, ImGuiInputTextFlags.Password);
                ImGui.InputText("Password##password.input", ref _accountPasswordBuffer__sensitive, MaxLength, ImGuiInputTextFlags.Password);
                if (ImGui.Button(LoginLabel))
                {
                    var userinput_username__sensitive = _accountNameBuffer__sensitive;
                    var userinput_password__sensitive = _accountPasswordBuffer__sensitive;

                    _loginTaskNullable = _vrcSession.Login(userinput_username__sensitive, userinput_password__sensitive);
                }
                ImGui.EndDisabled(); 
            }
            else
            {
                ImGui.BeginDisabled(_loginTaskNullable != null);
                ImGui.Text("Check your email for a 2FA code.");
                ImGui.InputText("2FA Code##twofer.input", ref _twoferBuffer__sensitive, MaxLength);
                if (ImGui.Button(SubmitCodeLabel))
                {
                    var userinput_twoferCode__sensitive = _twoferBuffer__sensitive;

                    _loginTaskNullable = _vrcSession.VerifyTwofer(userinput_twoferCode__sensitive, _twoferMethod);
                }
                ImGui.EndDisabled(); 
            }
            ImGui.SameLine();
            ImGui.Text($"Status: {Enum.GetName(_loginStatus)}");
            if (_needsTwofer)
            {
                ImGui.SameLine();
                ImGui.Text($"by {Enum.GetName(_twoferMethod)}");
            }
        }
        else
        {
            ImGui.Text(LoggedInMsg);
        }
    }
}