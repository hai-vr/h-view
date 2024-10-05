using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Hai.HView.Gui;

public static class UiUtil
{
    private const string VRCAvatarUrlFormat = "https://vrchat.com/home/avatar/{0}";

    public static void OpenAvatarUrl(string avatarIdStr)
    {
        OpenUrl(string.Format(VRCAvatarUrlFormat, avatarIdStr));
    }

    public static void OpenProjectUrl(string projectUrl)
    {
        OpenUrl(projectUrl);
    }

    // https://stackoverflow.com/a/43232486
    private static void OpenUrl(string url)
    {
        // SECURITY: We really want to avoid opening any user-provided URL here,
        // as we're starting a process to open this URL.
        if (!(url.StartsWith("https://") || url.StartsWith("http://"))) return;

        DANGER_StartUrl(url);
    }

    private static void DANGER_StartUrl(string url)
    {
        // DANGER: This starts a process. If invoked with untrusted input, this could lead to a RCE.
        try
        {
            Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }
}