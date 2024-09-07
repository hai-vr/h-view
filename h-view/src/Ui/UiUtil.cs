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

    // https://stackoverflow.com/a/43232486
    public static void OpenUrl(string url)
    {
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