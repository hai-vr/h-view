using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Hai.ExternalExpressionsMenu;
using Hai.HView.HaiSteamworks;
using Hai.HView.OSC;
using Hai.HView.OVR;
using Hai.HView.SavedData;
using hcontroller.Lyuma;
using Newtonsoft.Json;
using Valve.VR;

namespace Hai.HView.Core;

public class HVRoutine
{
    private const string AvatarChangeOscAddress = "/avatar/change";
    private const string ChatboxInputOscAddress = "/chatbox/input";
    public const string ShowCostumesOscAddress = "/avatar/parameters/H-View/ShowCostumes";
    
    private const string EEMPrefix = "ExternalExpressionsMenu_";
    private const string JsonSuffix = ".json";
    private Stopwatch _timer;
    private bool _exitRequested;
    private readonly HQuery _query;
    private readonly HMessageBox _messageBox;
    private readonly HVExternalService _externalService;
    private readonly HNSteamworks _steamworks;
    private readonly HOsc _osc;
    
    private readonly Regex _avoidPathTraversalInAvtrPipelineName = new Regex(@"^avtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

    public event OnManifestChangeEvent OnManifestChanged;
    public delegate void OnManifestChangeEvent(EMManifest newManifest);
    public event Action OnShowCostumes;
    public event Action OnHideCostumes;

    private EMManifest _expressionsManifest;
    private string[] _manifestFiles = new string[0];
    private bool _autoLaunch;
    private bool _isAutoLaunchAvailable;
    private Costume[] _costumes;
    private bool _isShowingCostumes;

    public HVRoutine(HOsc osc, HQuery query, HMessageBox messageBox, HVExternalService externalService, HNSteamworks steamworks)
    {
        _osc = osc;
        _query = query;
        _messageBox = messageBox;
        _externalService = externalService;
        _steamworks = steamworks;
    }

    public void Start()
    {
        _osc.Start();
        _timer = new Stopwatch();
        _timer.Start();

        var notARegex = $"{EEMPrefix}*.json";
        _manifestFiles = GetFilesInLocalLowVRChatDirectories(notARegex);
        
        Directory.CreateDirectory(SaveUtil.GetCostumesFolder());
        CollectCostumes();
    }

    private void CollectCostumes()
    {
        _costumes = Directory.GetFiles(SaveUtil.GetCostumesFolder(), "avtr_*.png", SearchOption.AllDirectories)
            .Select(costumeFile => new Costume
            {
                FullPath = costumeFile,
                AvatarId = Path.GetFileNameWithoutExtension(costumeFile)
            })
            .ToArray();
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
        
#if INCLUDES_STEAMWORKS
        _steamworks.Update();
#endif
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
                    if (_messageBox.TryGet(AvatarChangeOscAddress, out var avatar))
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
            
            if (result.path == AvatarChangeOscAddress)
            {
                Console.WriteLine("Detected avatar change");
                _query.Refresh();
                _messageBox.Reset();

                TryDeserializeExternalExpressionsMenu(result.arguments[0] as string);
                HideCostumes();
            }

            if (result.path == ShowCostumesOscAddress)
            {
                if (result.arguments[0] is bool && (bool)result.arguments[0])
                {
                    ShowCostumes();
                }
                else
                {
                    HideCostumes();
                }
            }
        }
    }

    private void ShowCostumes()
    {
        if (!_isShowingCostumes)
        {
            _isShowingCostumes = true;
            OnShowCostumes?.Invoke();
        }
    }

    private void HideCostumes()
    {
        if (_isShowingCostumes)
        {
            _isShowingCostumes = false;
            OnHideCostumes?.Invoke();
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

    public void InitializeAutoLaunch(bool initialValue)
    {
        _autoLaunch = initialValue;
        _isAutoLaunchAvailable = true;
    }

    public bool IsAutoLaunch()
    {
        return _autoLaunch;
    }

    public void SetAutoLaunch(bool autoLaunch)
    {
        if (_isAutoLaunchAvailable)
        {
            OpenVR.Applications.SetApplicationAutoLaunch(HVOpenVRThread.VrManifestAppKey, autoLaunch);
            _autoLaunch = autoLaunch;
        }
    }

    public HNSteamworks SteamworksModule()
    {
        return _steamworks;
    }

    public void SendChatMessage(string lobbyShareable)
    {
        UpdateMessageMultivalue(ChatboxInputOscAddress, new object[] {lobbyShareable, true, false});
    }

    public Costume[] GetCostumes()
    {
        return _costumes;
    }

    /// This forces the user out of the "Show costumes..." expressions menu, a rarely used feature.
    public void EjectUserFromCostumeMenu()
    {
        UpdateMessage(ShowCostumesOscAddress, false);
    }
}

public class Costume
{
    public string FullPath;
    public string AvatarId;
}