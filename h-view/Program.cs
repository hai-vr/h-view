using System.Globalization;
using Hai.HView.Core;
using Hai.HView.Gui;
using Hai.HView.OSC;
using Hai.HView.OVR;
using Hai.HView.SavedData;

#if INCLUDES_OPENVR
var isOverlay = !args.Contains("--no-overlay");
#else
var isOverlay = false;
#endif

#if REGISTER_MANIFEST
var registerManifest = !args.Contains("--no-register-manifest");
#else
var registerManifest = args.Contains("--register-manifest");
#endif

// Create a desktop window stylized as being the overlay version.
// This does nothing when the --overlay arg is set.
var simulateWindowlessStyle = args.Contains("--simulate-windowless");

// Allow this app to run in both Overlay and Normal mode as separately managed instances.
var serviceName = isOverlay ? $"{HVApp.AppName}-Overlay" : $"{HVApp.AppName}-Windowed";

if (!Directory.Exists(SaveUtil.GetUserDataFolder()))
{
    Console.WriteLine($"Save directory does not exist. Creating...");
    Directory.CreateDirectory(SaveUtil.GetUserDataFolder());
}
var config = SavedData.OpenConfig();

var oscPort = HOsc.RandomOscPort();
var queryPort = HQuery.RandomQueryPort();

var oscClient = new HOsc(oscPort);
var oscQuery = new HQuery(oscPort, queryPort, serviceName);
oscQuery.OnVrcOscPortFound += vrcOscPort => oscClient.SetReceiverOscPort(vrcOscPort);

var messageBox = new HMessageBox();
var externalService = new HVExternalService();
var routine = new HVRoutine(oscClient, oscQuery, messageBox, externalService);

var ovrThread = new HVOpenVRThread(routine, registerManifest);

// Start services
oscClient.Start();
oscQuery.Start();
routine.Start();
externalService.Start();

void WhenWindowClosed()
{
    routine.Finish();
    oscQuery.Finish();
    oscClient.Finish();
    ovrThread.Finish();
}

if (isOverlay)
{
    Console.WriteLine("Starting as a hybrid desktop / VR app.");
    // TODO: Allow the user to completely disable OpenVR integration.
    StartNewThread(() =>
    {
        ovrThread.Run(); // Loops until desktop window is closed.
        WhenWindowClosed();
    }, "VR-Thread");
}
else
{
    Console.WriteLine("Starting as a desktop window.");
    StartNewThread(() =>
    {
        new HVWindow(routine, WhenWindowClosed, simulateWindowlessStyle).Run();
    }, "UI-Thread");
}

void StartNewThread(ThreadStart threadStart1, string threadName)
{
    var thread = new Thread(threadStart1)
    {
        CurrentCulture = CultureInfo.InvariantCulture, // We don't want locale-specific numbers
        CurrentUICulture = CultureInfo.InvariantCulture,
        Name = threadName
    };
    thread.Start();
}

// Main loop
routine.MainLoop(); // This call does not return until routine.Finish() is called.