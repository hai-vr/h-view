using System.Globalization;
using Hai.HView;
using Hai.HView.Core;
using Hai.HView.Gui;
using Hai.HView.HaiSteamworks;
using Hai.HView.OSC;
using Hai.HView.OSC.PretendToBeVRC;
using Hai.HView.OVR;
using Hai.HView.SavedData;

var isOverlay = ConditionalCompilation.IncludesOpenVR && !args.Contains("--no-overlay");
var registerManifest = ConditionalCompilation.RegisterManifest ? !args.Contains("--no-register-manifest") : args.Contains("--register-manifest");

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

var fakeVrcOptional = ConditionalCompilation.EnableFakeVrcOsc ? new FakeVRCOSC() : null;

var oscPort = HOsc.RandomOscPort();
var queryPort = HQuery.RandomQueryPort();

var oscClient = new HOsc(oscPort);
var oscQuery = HQuery.ForVrchat(oscPort, queryPort, serviceName);
oscQuery.OnTargetOscPortFound += vrcOscPort => oscClient.SetReceiverOscPort(vrcOscPort);

var steamworksOptional = ConditionalCompilation.IncludesSteamworks ? new HNSteamworks() : null;

var messageBox = new HMessageBox();
var externalService = new HVExternalService();
var routine = new HVRoutine(oscClient, oscQuery, messageBox, externalService, steamworksOptional, fakeVrcOptional);

var ovrThreadOptional = isOverlay ? new HVOpenVRThread(routine, registerManifest) : null;

// Start services
oscClient.Start();
oscQuery.Start();
externalService.Start();
steamworksOptional?.Start();
fakeVrcOptional?.Start();
routine.Start();

void WhenWindowClosed()
{
    routine.Finish();
    oscQuery.Finish();
    oscClient.Finish();
    ovrThreadOptional?.Finish();
    fakeVrcOptional?.Finish();
}

// Start the VR or UI thread.
new Thread(() =>
{
    if (isOverlay)
    {
        Console.WriteLine("Starting as a hybrid desktop / VR app.");
        new HVOvrStarter(ovrThreadOptional, WhenWindowClosed).Run();
    }
    else
    {
        Console.WriteLine("Starting as a desktop window.");
        new HVWindow(routine, WhenWindowClosed, simulateWindowlessStyle).Run();
    }
})
{
    CurrentCulture = CultureInfo.InvariantCulture, // We don't want locale-specific numbers
    CurrentUICulture = CultureInfo.InvariantCulture,
    Name = isOverlay ? "VR-Thread" : "UI-Thread"
}.Start();

if (fakeVrcOptional != null)
{
    fakeVrcOptional.SendAvatarChange();
}

// Main loop
routine.MainLoop(); // This call does not return until routine.Finish() is called.