// ReSharper disable once RedundantUsingDirective
using System.Text; // Used by COOKIES_SUPPORTED
using Hai.HView.SavedData;
using Hai.HView.VRCLogin;

/// DANGER: This is a class that deals with sensitive information.
/// Exercise extreme caution when printing information to the output logs.
public class HVExternalService
{
    private const string LegacyCookieFile = "hview.cookies.txt";
    internal const string NewCookieFile = "hview.vrc.cookies.txt";
    private static string CookieFile => Path.Combine(SaveUtil.GetUserDataFolder(), NewCookieFile);

    private readonly HVVrcSession _vrcSession = new HVVrcSession();

    private Task<HVVrcSession.LoginResponse> _loginTaskNullable;
    private Task<HVVrcSession.LogoutResponseStatus> _logoutTaskNullable;
    private Task<HVVrcSession.SwitchAvatarResponseStatus> _selectingAvatarTaskNullable;

    public HVVrcSession.LoginResponseStatus LoginStatus { get; private set; } = HVVrcSession.LoginResponseStatus.Unresolved;
    public bool NeedsTwofer { get; private set; }
    public HVVrcSession.TwoferMethod TwoferMethod { get; private set; } = HVVrcSession.TwoferMethod.Other;
    public HVVrcSession.LogoutResponseStatus LogoutStatus { get; private set; } = HVVrcSession.LogoutResponseStatus.Unresolved;
    public HVVrcSession.SwitchAvatarResponseStatus SwitchStatus { get; private set; } = HVVrcSession.SwitchAvatarResponseStatus.Unresolved;

    public bool IsLoggedIn => _vrcSession.IsLoggedIn;
    public bool IsProcessingLogin => _loginTaskNullable != null;
    public bool IsProcessingSwitchAvatar => _selectingAvatarTaskNullable != null;

    public void Start()
    {
#if COOKIES_SUPPORTED
        if (File.Exists(CookieFile))
        {
            var userinput_cookies__sensitive = File.ReadAllText(CookieFile, Encoding.UTF8);
            _vrcSession.ProvideCookies(userinput_cookies__sensitive);
        }
#endif
        
        // LEGACY: Delete the cookie file located in the program folder.
        if (File.Exists(LegacyCookieFile))
        {
            File.Delete(LegacyCookieFile);
        }
    }

    private void SaveCookiesIntoFile()
    {
#if COOKIES_SUPPORTED
        File.WriteAllText(CookieFile, _vrcSession.GetAllCookies__Sensitive(), Encoding.UTF8);
#endif
    }

    private void DeleteCookieFile()
    {
#if COOKIES_SUPPORTED
        // TODO: Might have to just delete the auth token, but keep the Twofer cookie.
        if (File.Exists(CookieFile))
        {
            File.Delete(CookieFile);
        }
#endif
    }

    public void Login(string userinput_account__sensitive, string userinput_password__sensitive)
    {
        _loginTaskNullable = _vrcSession.Login(userinput_account__sensitive, userinput_password__sensitive);
    }

    public void Logout()
    {
        DeleteCookieFile(); // Don't wait for a response from the server to delete the cookies locally.
        _logoutTaskNullable = _vrcSession.Logout();
    }

    public void VerifyTwofer(string userinput_twoferCode__sensitive)
    {
        _loginTaskNullable = _vrcSession.VerifyTwofer(userinput_twoferCode__sensitive, TwoferMethod);
    }

    public void SelectAvatar(string userinput_avatarId)
    {
        _selectingAvatarTaskNullable = _vrcSession.SelectAvatar(userinput_avatarId);
    }

    public void ProcessTaskCompletion()
    {
        // TODO: Process async tasks back into the routine thread more sanely.
        
        
        if (_loginTaskNullable != null && _loginTaskNullable.IsCompleted)
        {
            if (_loginTaskNullable.IsCompletedSuccessfully)
            {
                var result = _loginTaskNullable.Result;
                LoginStatus = result.Status;
                if (result.Status == HVVrcSession.LoginResponseStatus.RequiresTwofer)
                {
                    NeedsTwofer = true;
                    TwoferMethod = result.TwoferMethod;
                }
                else if (result.Status == HVVrcSession.LoginResponseStatus.Success)
                {
                    NeedsTwofer = false;
                    SaveCookiesIntoFile();
                    // TODO: When login is successful, forget password
                    // _accountPasswordBuffer__sensitive = "";
                }
            }
            
            _loginTaskNullable = null;
        }

        if (_selectingAvatarTaskNullable != null && _selectingAvatarTaskNullable.IsCompleted)
        {
            if (_selectingAvatarTaskNullable.IsCompletedSuccessfully)
            {
                SwitchStatus = _selectingAvatarTaskNullable.Result;
            }

            _selectingAvatarTaskNullable = null;
        }

        if (_logoutTaskNullable != null && _logoutTaskNullable.IsCompleted)
        {
            if (_logoutTaskNullable.IsCompletedSuccessfully)
            {
                LogoutStatus = _logoutTaskNullable.Result;
                if (LogoutStatus == HVVrcSession.LogoutResponseStatus.Success || LogoutStatus == HVVrcSession.LogoutResponseStatus.Unauthorized)
                {
                    DeleteCookieFile();
                }
            }

            _logoutTaskNullable = null;
        }
    }
}