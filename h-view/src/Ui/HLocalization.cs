using Newtonsoft.Json.Linq;

namespace Hai.HView.Ui;

public class HLocalization
{
    private const string MetaLanguageNameKey = "_meta_languageName";
    private const string MetaGptKey = "_meta_x_gpt";
        
    private const string Prefix = "hview_localization.";
    private const string Suffix = ".json";
    
    private static List<string> _availableLanguageNames = new List<string> { "English" };
    private static Dictionary<string, Dictionary<string, string>> _languageCodeToLocalization;
    private static List<string> _availableLanguageCodes;
    private static string _selectedLanguageCode = "en";
    private static int _selectedIndex;
    
    private static readonly Dictionary<string, string> DebugKeyDatabase = new Dictionary<string, string>();
        
    static HLocalization()
    {
        DebugKeyDatabase.Add(MetaLanguageNameKey, "English");
        DebugKeyDatabase.Add(MetaGptKey, @"These can be translated with ChatGPT using the prompt: Please translate the values of this JSON file to language written in the _meta_languageName key. Keep the keys intact. The words Float and Bool should not be translated. The value of the first key `_meta_languageName` also needs to be translated to that language (for example, French needs to be Français), and then concatenated with the string ` (ChatGPT)` ");

        INTROSPECT_INVOKE_ALL(typeof(HLocalizationPhrase));
        PrintDatabase();
    }

    private static void PrintDatabase()
    {
#if HV_DEBUG
        var sorted = new SortedDictionary<string, string>(DebugKeyDatabase);
        // var jsonObject = JObject.FromObject(sorted);
        var jsonObject = JObject.FromObject(DebugKeyDatabase);
        Console.WriteLine(jsonObject.ToString());
#endif
    }

    public static void SwitchLanguage(int selectedLanguage)
    {
        var languageCode = _availableLanguageCodes[selectedLanguage];
        _selectedLanguageCode = languageCode;
        _selectedIndex = selectedLanguage;
    }

    public static void InitializeAndProvideFor(string confLocale)
    {
        ReloadLocalizationsInternal();
        
        var languageCode = string.IsNullOrEmpty(confLocale) ? "en" : confLocale;
        if (_languageCodeToLocalization.ContainsKey(languageCode))
        {
            _selectedLanguageCode = languageCode;
        }

        _selectedIndex = _selectedLanguageCode == "en" ? 0 : 1;
    }

    private static void ReloadLocalizationsInternal()
    {
        var allFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory);
        _languageCodeToLocalization = allFiles
            .Where(path =>
            {
                var fileName = Path.GetFileName(path);
                return fileName.StartsWith(Prefix) && fileName.EndsWith(Suffix);
            })
            .Where(path =>
            {
                var fileName = Path.GetFileName(path);
                var languageCode = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - Suffix.Length);
                return languageCode != "en";
            })
            .ToDictionary(path =>
            {
                var fileName = Path.GetFileName(path);
                var languageCode = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - Suffix.Length);
                return languageCode;
            }, ExtractDictionaryFromPath);

        _availableLanguageCodes = new[] { "en" }
            .Concat(_languageCodeToLocalization.Keys)
            .ToList();
        _availableLanguageNames = new[] { "English" }
            .Concat(_languageCodeToLocalization.Values.Select(dictionary => (dictionary.TryGetValue(MetaLanguageNameKey, out var value) ? value : "??")))
            .ToList();
        
        Console.WriteLine($"Found language codes: {string.Join(',', _availableLanguageCodes)}");
    }

    private static Dictionary<string, string> ExtractDictionaryFromPath(string path)
    {
        try
        {
            var contents = File.ReadAllText(path);
            return ExtractDictionaryFromText(contents);
        }
        catch (Exception e)
        {
            // FIXME: Log exception
            return new Dictionary<string, string>();
        }
    }

    private static Dictionary<string, string> ExtractDictionaryFromText(string contents)
    {
        var localizations = new Dictionary<string, string>();
            
        // Assume that NewtonsoftJson is available in the project
        var jsonObject = JObject.Parse(contents);
        foreach (var pair in jsonObject)
        {
            var value = pair.Value.Value<string>();
            localizations.Add(pair.Key, value);
        }

        return localizations;
    }

    internal static string LocalizeOrElse(string labelName, string orDefault)
    {
        var key = $"label_{labelName}";
        return DoLocalize(orDefault, key);
    }

    internal static string LocalizeOrElse__ImGuiTab(string labelName, string orDefault)
    {
        var key = $"label_{labelName}";
        return DoLocalize(orDefault, key) + $"###{labelName}";
    }
    
    private static string DoLocalize(string orDefault, string key)
    {
#if HV_DEBUG
        if (!DebugKeyDatabase.ContainsKey(key))
        {
            DebugKeyDatabase.Add(key, orDefault);
        }
#endif
        if (IsEnglish()) return orDefault;
        if (_languageCodeToLocalization[_selectedLanguageCode].TryGetValue(key, out var value)) return value;
        return orDefault;
    }

    private static bool IsEnglish()
    {
        return _selectedIndex == 0;
    }

    public static List<string> GetLanguages()
    {
        return _availableLanguageNames;
    }

    public static List<string> GetLanguageCodes()
    {
        return _availableLanguageCodes;
    }

    private static void INTROSPECT_INVOKE_ALL(Type type)
    {
        var separatorFound = false;
        foreach (var methodInfo in type.GetMethods()
                     .Where(info => info.ReturnType == typeof(string))
                     .Where(info => info.IsStatic)
                )
        {
            if (separatorFound)
            {
                methodInfo.Invoke(null, Array.Empty<object>());
            }
            else
            {
                if (methodInfo.Name == $"get_{nameof(HLocalizationPhrase.Separator)}") separatorFound = true;
            }
        }
    }
}