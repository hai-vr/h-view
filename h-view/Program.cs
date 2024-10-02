using System.Globalization;
using Hai.HNetworking.Steamworks;
using Hai.HView;
using Hai.HView.Core;
using Hai.HView.Gui;
using Hai.HView.OSC;
using Hai.HView.OSC.PretendToBeVRC;
using Hai.HView.OVR;
using Hai.HView.SavedData;

new HViewProgram(args).Run();

internal class HViewProgram
{
    // ReSharper disable InconsistentNaming
    private readonly SavedData config;
    private readonly FakeVRCOSC fakeVrcOptional;
    private readonly HOsc oscClient;
    private readonly HQuery oscQuery;
    private readonly HNSteamworks steamworksOptional;
    private readonly HVExternalService externalService;
    private readonly HVRoutine routine;
    private readonly HVOpenVRThread ovrThreadOptional;
    private readonly Thread uiThread;
    // ReSharper restore InconsistentNaming

    public HViewProgram(string[] arguments)
    {
        var isOverlay = ConditionalCompilation.IncludesOpenVR && !arguments.Contains("--no-overlay");
        
        var registerManifest = ConditionalCompilation.RegisterManifest ? !arguments.Contains("--no-register-manifest") : arguments.Contains("--register-manifest");

        // Create a desktop window stylized as being the overlay version.
        // This does nothing when the --overlay arg is set.
        var simulateWindowlessStyle = arguments.Contains("--simulate-windowless");

        // Allow this app to run in both Overlay and Normal mode as separately managed instances.
        var serviceName = isOverlay ? $"{HVApp.AppName}-Overlay" : $"{HVApp.AppName}-Windowed";

        if (!Directory.Exists(SaveUtil.GetUserDataFolder()))
        {
            Console.WriteLine($"Save directory does not exist. Creating...");
            Directory.CreateDirectory(SaveUtil.GetUserDataFolder());
        }
        config = SavedData.OpenConfig();

        fakeVrcOptional = ConditionalCompilation.EnableFakeVrcOsc ? new FakeVRCOSC() : null;

        var oscPort = HOsc.RandomOscPort();
        var queryPort = HQuery.RandomQueryPort();

        oscClient = new HOsc(oscPort);
        oscQuery = HQuery.ForVrchat(oscPort, queryPort, serviceName);
        oscQuery.OnTargetOscPortFound += vrcOscPort => oscClient.SetReceiverOscPort(vrcOscPort);

        steamworksOptional = ConditionalCompilation.IncludesSteamworks ? new HNSteamworks() : null;

        var messageBox = new HMessageBox();
        externalService = new HVExternalService();
        
        routine = new HVRoutine(oscClient, oscQuery, messageBox, externalService, steamworksOptional, fakeVrcOptional);
        
        ovrThreadOptional = isOverlay ? new HVOpenVRThread(routine, registerManifest) : null;
        
        uiThread = new Thread(() =>
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
        };
    }
        
    private void WhenWindowClosed()
    {
        routine.Finish();
        oscQuery.Finish();
        oscClient.Finish();
        ovrThreadOptional?.Finish();
        fakeVrcOptional?.Finish();
    }

    internal void Run()
    {
        oscClient.Start();
        oscQuery.Start();
        externalService.Start();
        steamworksOptional?.Start();
        fakeVrcOptional?.Start();
        routine.Start();
        
        uiThread.Start();

        if (fakeVrcOptional != null)
        {
            fakeVrcOptional.SendAvatarChange();
        }

        routine.MainLoop(); // This call does not return until routine.Finish() is called.
    }
}