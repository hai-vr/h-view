using System.Globalization;
using System.Text;
using Hai.HNetworking.Steamworks;
using Hai.HView;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.Gui;
using Hai.HView.HThirdParty;
using Hai.HView.OSC;
using Hai.HView.OSC.PretendToBeVRC;
using Hai.HView.OVR;
using Hai.HView.Ui;

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
    private readonly HVCaptureModule captureModule;
    
    // ReSharper restore InconsistentNaming

    public HViewProgram(string[] arguments)
    {
#if HV_DEBUG
        WriteThirdPartyRegistrySummaryToFile();
#endif
        
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
        UiColors.ProvideConfig(config);
        
        HLocalization.InitializeAndProvideFor(config.locale);

        fakeVrcOptional = ConditionalCompilation.EnableFakeVrcOsc ? new FakeVRCOSC() : null;

        var oscPort = HOsc.RandomOscPort();
        var queryPort = HQuery.RandomQueryPort();

        oscClient = new HOsc(oscPort);
        oscQuery = HQuery.ForVrchat(oscPort, queryPort, serviceName);
        oscQuery.OnTargetOscPortFound += vrcOscPort => oscClient.SetReceiverOscPort(vrcOscPort);

        steamworksOptional = ConditionalCompilation.IncludesSteamworks ? new HNSteamworks() : null;

        var messageBox = new HMessageBox();
        externalService = new HVExternalService();

        captureModule = new HVCaptureModule();
        routine = new HVRoutine(oscClient, oscQuery, messageBox, externalService, steamworksOptional, fakeVrcOptional, config, captureModule);
        
        ovrThreadOptional = isOverlay ? new HVOpenVRThread(routine, registerManifest, config) : null;
        
        uiThread = new Thread(isOverlay ? () =>
        {
            Console.WriteLine("Starting as a hybrid desktop / VR app.");
            new HVOvrStarter(ovrThreadOptional, WhenWindowClosed).Run();
        } : () =>
        {
            Console.WriteLine("Starting as a desktop window.");
            new HVDesktopStarter(routine, WhenWindowClosed, simulateWindowlessStyle, config).Run();
        })
        {
            CurrentCulture = CultureInfo.InvariantCulture, // We don't want locale-specific numbers
            CurrentUICulture = CultureInfo.InvariantCulture,
            Name = isOverlay ? "VR-Thread" : "UI-Thread"
        };
    }

    private void WriteThirdPartyRegistrySummaryToFile()
    {
        var registry = new HThirdPartyRegistry(File.ReadAllText(HAssets.ThirdPartyLookup.Absolute(), Encoding.UTF8));

        var sb = new StringBuilder();
        var sw = new StringWriter(sb);
        var entries = registry.GetEntries();
        sw.WriteLine("### Third-party acknowledgements");
        sw.WriteLine("");
        sw.WriteLine("- Included in source code form and DLLs:");
        foreach (var entry in entries.Where(IsSourceOrDLL))
        {
            sw.WriteLine(FormatEntry(entry));
        }
        sw.WriteLine("- Other dependencies included through NuGet: [h-view/h-view.csproj](h-view/h-view.csproj)");
        var thirdPartyEntries = entries
            .Where(entry => !IsSourceOrDLL(entry) && !entry.kind.Contains("Asset-Included"))
            .OrderBy(entry => entry.conditionallyIncludedWhen.Count)
            .ToArray();
        foreach (var entry in thirdPartyEntries)
        {
            sw.WriteLine(FormatEntry(entry));
        }
        sw.WriteLine("  - (there may be other implicit packages)");
        sw.WriteLine("- Asset dependencies:");
        foreach (var entry in entries.Where(entry => entry.kind.Contains("Asset-Included")))
        {
            sw.WriteLine(FormatEntry(entry));
        }
        
        File.WriteAllText("THIRDPARTY-generated.md", sb.ToString(), Encoding.UTF8);
    }

    private static bool IsSourceOrDLL(HThirdPartyEntry entry)
    {
        return entry.kind.Contains("Source-Included") || entry.kind.Contains("Binary-DLL-Included");
    }

    private static string FormatEntry(HThirdPartyEntry entry)
    {
        if (entry.conditionallyIncludedWhen.Count > 0)
        {
            return $"  - *{entry.projectName}* @ {entry.projectUrl} ([{entry.licenseName}]({entry.licenseUrl})) by {entry.attributedTo} (conditionally included when {string.Join(", ", entry.conditionallyIncludedWhen)} flag is set)";
        }
        else
        {
            return $"  - {entry.projectName} @ {entry.projectUrl} ([{entry.licenseName}]({entry.licenseUrl})) by {entry.attributedTo}";
        }
    }

    private void WhenWindowClosed()
    {
        routine.Finish();
        oscQuery.Finish();
        oscClient.Finish();
        ovrThreadOptional?.Finish();
        fakeVrcOptional?.Finish();
        captureModule.Teardown();
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