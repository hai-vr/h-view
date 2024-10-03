namespace Hai.HView.Ui;

public class HLocalizationPhrase
{
    // Labels
    public static string AddressLabel => HLocalization.LocalizeOrElse(nameof(AddressLabel), "Address");
    public static string AvatarIdLabel => HLocalization.LocalizeOrElse(nameof(AvatarIdLabel), "Avatar ID");
    public static string CopyLabel => HLocalization.LocalizeOrElse(nameof(CopyLabel), "Copy");
    public static string CurrentAvatarLabel => HLocalization.LocalizeOrElse(nameof(CurrentAvatarLabel), "Current avatar");
    public static string HoldLabel => HLocalization.LocalizeOrElse(nameof(HoldLabel), "Hold");
    public static string KeyboardLabel => HLocalization.LocalizeOrElse(nameof(KeyboardLabel), "Keyboard");
    public static string LoginLabel => HLocalization.LocalizeOrElse(nameof(LoginLabel), "Login");
    public static string LoginToVrchatLabel => HLocalization.LocalizeOrElse(nameof(LoginToVrchatLabel), "Login to VRChat");
    public static string LogoutLabel => HLocalization.LocalizeOrElse(nameof(LogoutLabel), "Logout");
    public static string MultifactorCodeLabel => HLocalization.LocalizeOrElse(nameof(MultifactorCodeLabel), "2FA Code");
    public static string OpenBrowserLabel => HLocalization.LocalizeOrElse(nameof(OpenBrowserLabel), "Open browser");
    public static string PasswordLabel => HLocalization.LocalizeOrElse(nameof(PasswordLabel), "Password");
    public static string PhysBonesLabel => HLocalization.LocalizeOrElse(nameof(PhysBonesLabel), "PhysBones");
    public static string QuickMenuLeftLabel => HLocalization.LocalizeOrElse(nameof(QuickMenuLeftLabel), "Quick Menu Left");
    public static string QuickMenuRightLabel => HLocalization.LocalizeOrElse(nameof(QuickMenuRightLabel), "Quick Menu Right");
    public static string SendLabel => HLocalization.LocalizeOrElse(nameof(SendLabel), "Send");
    public static string StartServerLabel => HLocalization.LocalizeOrElse(nameof(StartServerLabel), "Start server");
    public static string StartWithSteamVRLabel => HLocalization.LocalizeOrElse(nameof(StartWithSteamVRLabel), "Start with SteamVR");
    public static string SteamVRLabel => HLocalization.LocalizeOrElse(nameof(SteamVRLabel), "SteamVR");
    public static string StopServerLabel => HLocalization.LocalizeOrElse(nameof(StopServerLabel), "Stop server");
    public static string SubmitCodeLabel => HLocalization.LocalizeOrElse(nameof(SubmitCodeLabel), "Submit code");
    public static string SwitchAvatarLabel => HLocalization.LocalizeOrElse(nameof(SwitchAvatarLabel), "Switch avatar");
    public static string TypeLabel => HLocalization.LocalizeOrElse(nameof(TypeLabel), "Type");
    public static string UsernameLabel => HLocalization.LocalizeOrElse(nameof(UsernameLabel), "Username");
    public static string ValueLabel => HLocalization.LocalizeOrElse(nameof(ValueLabel), "Value");
    
    // Tabs
    public static string AvatarTabLabel => HLocalization.LocalizeOrElse(nameof(AvatarTabLabel), "Avatar");
    public static string ContactsTabLabel => HLocalization.LocalizeOrElse(nameof(ContactsTabLabel), "Contacts");
    public static string CostumesTabLabel => HLocalization.LocalizeOrElse(nameof(CostumesTabLabel), "Costumes");
    public static string DefaultTabLabel => HLocalization.LocalizeOrElse(nameof(DefaultTabLabel), "Default");
    public static string FaceTrackingTabLabel => HLocalization.LocalizeOrElse(nameof(FaceTrackingTabLabel), "Face Tracking");
    public static string InputTabLabel => HLocalization.LocalizeOrElse(nameof(InputTabLabel), "Input");
    public static string SignInTabLabel => HLocalization.LocalizeOrElse(nameof(SignInTabLabel), "Sign in");
    public static string MenuTabLabel => HLocalization.LocalizeOrElse(nameof(MenuTabLabel), "Menu");
    public static string NetworkingTabLabel => HLocalization.LocalizeOrElse(nameof(NetworkingTabLabel), "Networking");
    public static string OptionsTabLabel => HLocalization.LocalizeOrElse(nameof(OptionsTabLabel), "Options");
    public static string ParametersTabLabel => HLocalization.LocalizeOrElse(nameof(ParametersTabLabel), "Parameters");
    public static string SelectTabLabel => HLocalization.LocalizeOrElse(nameof(SelectTabLabel), "Select");
    public static string ShortcutsTabLabel => HLocalization.LocalizeOrElse(nameof(ShortcutsTabLabel), "Shortcuts");
    public static string SwitchTabLabel => HLocalization.LocalizeOrElse(nameof(SwitchTabLabel), "Switch");
    public static string TabsTabLabel => HLocalization.LocalizeOrElse(nameof(TabsTabLabel), "Tabs");
    public static string TrackingTabLabel => HLocalization.LocalizeOrElse(nameof(TrackingTabLabel), "Tracking");
    public static string UtilityTabLabel => HLocalization.LocalizeOrElse(nameof(UtilityTabLabel), "Utility");
    
    // Messages
    public static string MsgCookieSaveLocation => HLocalization.LocalizeOrElse(nameof(MsgCookieSaveLocation), "Cookies have been saved in %%APPDATA%%/H-View/{0}.");
    public static string MsgLoggedIn => HLocalization.LocalizeOrElse(nameof(MsgLoggedIn), "You are logged in.");
    public static string MsgLogoutToDeleteTheseCookies => HLocalization.LocalizeOrElse(nameof(MsgLogoutToDeleteTheseCookies), "Logout to delete these cookies.");
    public static string MsgMultifactorCheckEmails => HLocalization.LocalizeOrElse(nameof(MsgMultifactorCheckEmails), "Check your email for a 2FA code.");
    public static string MsgNoVrNotLoggedIn => HLocalization.LocalizeOrElse(nameof(MsgNoVrNotLoggedIn), "HaiView is not logged into your VRChat account. To log in, open this tab on the desktop window.");
    public static string RandomizeParametersLabel => HLocalization.LocalizeOrElse(nameof(RandomizeParametersLabel), "Randomize parameters");
}