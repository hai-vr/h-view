using System.Text;
using Newtonsoft.Json;

namespace Hai.HView.HThirdParty;

public class HThirdPartyRegistry
{
    private readonly Dictionary<string, string> _tagToDescription;
    private readonly HThirdPartyEntry[] _entries;
    private readonly Dictionary<string, string> _fullLicenseText; // May contain missing keys in incorrect builds.

    public HThirdPartyRegistry(string jsonContent)
    {
        var just = JsonConvert.DeserializeObject<HThirdPartyFile>(jsonContent);
        _tagToDescription = just.kinds.ToDictionary(kind => kind.tag, kind => kind.description);
        _entries = just.entries;
        _fullLicenseText = _entries
            .Select(entry => entry.fullLicenseTextFile)
            .Distinct()
            .Where(susStr => !string.IsNullOrWhiteSpace(susStr))
            .Where(susStr => !CouldBePathTraversal(susStr))
            .Where(textFile => File.Exists(HAssets.HThirdPartyLicense(textFile).Absolute()))
            .ToDictionary(textFile => textFile, textFile => File.ReadAllText(HAssets.HThirdPartyLicense(textFile).Absolute(), Encoding.UTF8));
    }

    public bool TryGetTag(string tag, out string description)
    {
        return _tagToDescription.TryGetValue(tag, out description);
    }

    public bool TryGetFullLicenseText(string licenseFile, out string contents)
    {
        return _fullLicenseText.TryGetValue(licenseFile, out contents);
    }

    public HThirdPartyEntry[] GetEntries()
    {
        return _entries;
    }

    private static bool CouldBePathTraversal(string susStr)
    {
        return susStr.Contains("/") || susStr.Contains("\\") || susStr.Contains("..") || susStr.Contains("*");
    }
}

public struct HThirdPartyFile
{
    public HThirdPartyKind[] kinds;
    public HThirdPartyEntry[] entries;
}

public struct HThirdPartyKind
{
    public string tag;
    public string description;
}

public struct HThirdPartyEntry
{
    public string projectName;
    public string attributedTo;
    public string licenseName;
    public string SPDX;
    public string licenseUrl;
    public string projectUrl;
    public string[] kind;
    public string fullLicenseTextFile;
    public bool isRestricted;
    public string[] conditionallyIncludedWhen;
}