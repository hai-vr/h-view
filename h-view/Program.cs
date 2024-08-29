using System.Globalization;
using Hai.HView.Core;
using Hai.HView.Gui;
using Hai.HView.OSC;
using Valve.VR;

var isSteamVROverlay = false;
#if INCLUDES_OVERLAY
isSteamVROverlay = args.Contains("--overlay");
#endif

// Create a desktop window stylized as being the overlay version.
// This does nothing when the --overlay arg is set.
var simulateWindowlessStyle = args.Contains("--simulate-windowless");

// Allow this app to run in both Overlay and Normal mode as separately managed instances.
var serviceName = isSteamVROverlay ? $"{HVApp.AppName}-Overlay" : $"{HVApp.AppName}-Windowed";

var oscPort = HOsc.RandomOscPort();
var queryPort = HQuery.RandomQueryPort();

var oscClient = new HOsc(oscPort);
var oscQuery = new HQuery(oscPort, queryPort, serviceName);
oscQuery.OnVrcOscPortFound += vrcOscPort => oscClient.SetReceiverOscPort(vrcOscPort);

var messageBox = new HMessageBox();
var routine = new HVRoutine(oscClient, oscQuery, messageBox);

oscClient.Start();
oscQuery.Start();
routine.Start();

void WhenWindowClosed()
{
    routine.Finish();
    oscQuery.Finish();
    oscClient.Finish();
}

var uiThread = new Thread(() =>
{
    if (!isSteamVROverlay)
    {
        new HVWindow(routine, WhenWindowClosed, simulateWindowlessStyle).Run();
    }
    else
    {
        Console.WriteLine("Overlay mode is enabled (--overlay)");
        new HVWindowless(routine, WhenWindowClosed).Run();
    }
})
{
    CurrentCulture = CultureInfo.InvariantCulture, // We don't want locale-specific numbers
    CurrentUICulture = CultureInfo.InvariantCulture
};
uiThread.Start();

routine.MainLoop(); // This call does not return until routine.Finish() is called.