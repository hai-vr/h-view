using System.Globalization;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Hai.HView.Core;

public class VERSION
{
    // ReSharper disable once InconsistentNaming
    public static string version { get; private set; }
    public static string miniVersion { get; private set; }

    static VERSION()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        if (v.Major == 1 && v.Minor == 0 && v.Build == 0)
        {
            var packageVer = "0.0.0";
            var packageJson = "../../../../Packages/dev.hai-vr.app.h-view/package.json";
            if (File.Exists(packageJson))
            {
                if (JObject.Parse(File.ReadAllText(packageJson)).TryGetValue("version", out var ver))
                {
                    packageVer = (string)ver;
                }
            }
            version = $"v{packageVer}-ExecutingFromSource";
            miniVersion = $"v{packageVer}-EFS";
        }
        else
        {
            version = string.Format(CultureInfo.InvariantCulture, "v{0}.{1}.{2}", v.Major, v.Minor, v.Build);
            miniVersion = version;
        }
    }
}