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
    private Stopwatch _timer;
    private bool _exitRequested;
    private readonly HQuery _query;
    private readonly HMessageBox _messageBox;
    private readonly HOsc _osc;
    
    private readonly Regex _avoidPathTraversalInAvtrPipelineName = new Regex(@"^avtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

    public event OnManifestChangeEvent OnManifestChanged;
    public delegate void OnManifestChangeEvent(EMManifest newManifest);

    private EMManifest _expressionsManifest;

    public HVRoutine(HOsc osc, HQuery query, HMessageBox messageBox)
    {
        _osc = osc;
        _query = query;
        _messageBox = messageBox;
    }

    public void Start()
    {
        _osc.Start();
        _timer = new Stopwatch();
        _timer.Start();
    }

    public void Execute()
    {
        while (!_exitRequested)
        {
            ExecuteInternal();
        }
    }
    
    public void ExecuteInternal()
    {
        Thread.Sleep(20);

        var messages = _osc.PullMessages();
        ProcessOscEvents(messages);
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

                TryDeserializeExternalExpressionsMenu(result);
            }
        }
    }

    private void TryDeserializeExternalExpressionsMenu(SimpleOSC.OSCMessage result)
    {
        var externalNullable = GetExternalExpressionsMenuFilenameOrNull(result);
        if (externalNullable != null)
        {
            Console.WriteLine("Found external menu");
            _expressionsManifest = JsonConvert.DeserializeObject<EMManifest>(File.ReadAllText(externalNullable, Encoding.UTF8));
            OnManifestChanged?.Invoke(_expressionsManifest);
        }
    }

    private string GetExternalExpressionsMenuFilenameOrNull(SimpleOSC.OSCMessage result)
    {
        var unsafeArgument = result.arguments[0] as string;
        if (unsafeArgument == null) return null;
        if (!_avoidPathTraversalInAvtrPipelineName.IsMatch(unsafeArgument)) return null;
        if (ContainsPathTraversalElements(unsafeArgument)) return null;

        var safeAvatarId = unsafeArgument;
        var usersFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "VRChat", "vrchat");
        var found = Directory.GetFiles(usersFolder, $"ExternalExpressionsMenu_{safeAvatarId}.json", SearchOption.AllDirectories);

        if (found.Length == 0) return null;
        if (found.Length == 1) return found[0];
        
        return found.MaxBy(File.GetLastWriteTime);
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