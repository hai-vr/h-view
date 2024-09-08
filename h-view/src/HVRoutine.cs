using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Hai.ExternalExpressionsMenu;
using Hai.HView.OSC;
using hcontroller.Lyuma;
using Newtonsoft.Json;

namespace Hai.HView.Core;

public class HVRoutine
{
    private const string EEMPrefix = "ExternalExpressionsMenu_";
    private const string JsonSuffix = ".json";
    private Stopwatch _timer;
    private bool _exitRequested;
    private readonly HQuery _query;
    private readonly HMessageBox _messageBox;
    private readonly HVExternalService _externalService;
    private readonly HOsc _osc;
    
    private readonly Regex _avoidPathTraversalInAvtrPipelineName = new Regex(@"^avtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

    public event OnManifestChangeEvent OnManifestChanged;
    public delegate void OnManifestChangeEvent(EMManifest newManifest);

    private EMManifest _expressionsManifest;
    private string[] _manifestFiles = new string[0];

    public HVRoutine(HOsc osc, HQuery query, HMessageBox messageBox, HVExternalService externalService)
    {
        _osc = osc;
        _query = query;
        _messageBox = messageBox;
        _externalService = externalService;
    }

    public void Start()
    {
        _osc.Start();
        _timer = new Stopwatch();
        _timer.Start();

        var notARegex = $"{EEMPrefix}*.json";
        _manifestFiles = GetFilesInLocalLowVRChatDirectories(notARegex);
    }

    public void MainLoop()
    {
        while (!_exitRequested)
        {
            Loop();
        }
    }

    private void Loop()
    {
        Thread.Sleep(20);

        var queryMessages = _query.PullMessages();
        ProcessQueryEvents(queryMessages);

        var messages = _osc.PullMessages();
        ProcessOscEvents(messages);

        _externalService.ProcessTaskCompletion();
    }

    private void ProcessQueryEvents(List<object> queryMessages)
    {
        foreach (var msg in queryMessages)
        {
            if (msg is HQuery.HQueryMessageEvent messageEvent)
            {
                _messageBox.ReceivedQuery(messageEvent.Node);
            }
            else if (msg is HQuery.HQueryCompleteEvent)
            {
                if (_expressionsManifest == null)
                {
                    if (_messageBox.TryGet("/avatar/change", out var avatar))
                    {
                        Console.WriteLine("Trying to load avatar from OSC Query response");
                        TryDeserializeExternalExpressionsMenu(avatar.Values[0] as string);
                    }
                }
            }
        }
    }

    private void ProcessOscEvents(List<SimpleOSC.OSCMessage> oscMessages)
    {
        foreach (var result in oscMessages)
        {
            _messageBox.ReceivedOsc(result.path, result.arguments);
            
            if (result.path == "/avatar/change")
            {
                Console.WriteLine("Detected avatar change");
                _query.Refresh();
                _messageBox.Reset();

                TryDeserializeExternalExpressionsMenu(result.arguments[0] as string);
            }
        }
    }

    public void ManuallyLoadManifestFromFile(string safeFileName)
    {
        LoadManifestFromFile(safeFileName);
    }

    private void TryDeserializeExternalExpressionsMenu(string unsafePipelineId_Nullable)
    {
        var file = GetExternalExpressionsMenuFilenameOrNull(unsafePipelineId_Nullable);
        if (file != null)
        {
            Console.WriteLine("Found external menu");
            LoadManifestFromFile(file);
        }
    }

    private void LoadManifestFromFile(string safeFileName)
    {
        _expressionsManifest = JsonConvert.DeserializeObject<EMManifest>(File.ReadAllText(safeFileName, Encoding.UTF8));
            
        // Fix quirk in JSON files generated in 1.0.0-beta.4 where it could contain empty parameters.
        // This can be a problem when we're trying to get information about a parameter,
        // but that parameter name happens to be the empty string.
        _expressionsManifest.expressionParameters = _expressionsManifest.expressionParameters
            .Where(expression => expression.parameter != "")
            .ToArray();
            
        OnManifestChanged?.Invoke(_expressionsManifest);
    }

    private string GetExternalExpressionsMenuFilenameOrNull(string unsafeArgumentNullable)
    {
        if (unsafeArgumentNullable == null) return null;
        if (!_avoidPathTraversalInAvtrPipelineName.IsMatch(unsafeArgumentNullable)) return null;
        if (ContainsPathTraversalElements(unsafeArgumentNullable)) return null;

        var safeAvatarId = unsafeArgumentNullable;
        var searchPattern = $"{EEMPrefix}{safeAvatarId}{JsonSuffix}";
        var found = GetFilesInLocalLowVRChatDirectories(searchPattern);

        if (found.Length == 0) return null;
        if (found.Length == 1) return found[0];
        
        return found.MaxBy(File.GetLastWriteTime);
    }

    public static string[] GetFilesInLocalLowVRChatDirectories(string searchPattern)
    {
        var usersFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "VRChat", "vrchat");
        var found = Directory.GetFiles(usersFolder, searchPattern, SearchOption.AllDirectories);
        return found;
    }

    private static bool ContainsPathTraversalElements(string susStr)
    {
        return susStr.Contains("/") || susStr.Contains("\\") || susStr.Contains(".") || susStr.Contains("*");
    }

    public void Finish()
    {
        _exitRequested = true;
    }

    public Dictionary<string, HOscItem> UiOscMessages()
    {
        return _messageBox.CopyForUi();
    }

    public string[] UiManifestSafeFilePaths()
    {
        return _manifestFiles.ToArray();
    }

    public HVExternalService UiExternalService()
    {
        // TODO: Review how the service calls are threaded.
        return _externalService;
    }

    // Given a key and the state of a hold-to-toggle button,
    // emit an OSC event when the value changes:
    // - When pressing, the emitted value is inverted from what it was.
    // - When released, the emitted value goes back to what it was.
    public void EmitOscFlipEventOnChange(string key, bool isHeldDown)
    {
        var pressState = _messageBox.SubmitFlipState(key, isHeldDown, out var initialValue);
        switch (pressState)
        {
            case HMessageBox.FlipState.None:
                break;
            case HMessageBox.FlipState.OnPress:
                _osc.SendOsc(key, !initialValue);
                break;
            case HMessageBox.FlipState.OnRelease:
                _osc.SendOsc(key, initialValue);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void UpdateMessage(string key, object singleValue)
    {
        _osc.SendOsc(key, singleValue);
        _messageBox.RewriteIfNecessary(key, singleValue);
    }

    public void UpdateMessageMultivalue(string key, object[] multipleValues)
    {
        _osc.SendOscMultivalue(key, multipleValues);
    }
}