using System.Diagnostics;
using Hai.HView.Core;
using Hai.HView.Gui;
using Hai.HView.Overlay;
using Valve.VR;

namespace Hai.HView.OVR;

public class HVOpenVRThread
{
    private const int TotalWindowWidth = 600;
    private const int TotalWindowHeight = 510;
    public const string VrManifestAppKey = "Hai.HView";

    private readonly HVRoutine _routine;
    private readonly HVOpenVRManagement _ovr;
    private readonly bool _registerAppManifest;

    public HVOpenVRThread(HVRoutine routine, bool registerAppManifest)
    {
        _routine = routine;
        _registerAppManifest = registerAppManifest;
        _ovr = new HVOpenVRManagement();
    }

    public void Run()
    {
        var desktopWindow = new HVInnerWindow(_routine, false, TotalWindowWidth, TotalWindowHeight, TotalWindowWidth, TotalWindowHeight);
        desktopWindow.SetupUi(false);
        
        var ovr = new HVOpenVRManagement();
        var ovrStarted = ovr.TryStart();

        var retry = new Stopwatch();
        retry.Start();

        var shouldContinue = true;
        {
            var sw = new Stopwatch();
            sw.Start();
            while (!ovrStarted)
            {
                var shouldRerender = desktopWindow.HandleSleep();
                if (shouldRerender)
                {
                    shouldContinue = desktopWindow.UpdateIteration(sw);
                }
                if (!shouldContinue) break;
            
                sw.Restart();
            
                if (retry.Elapsed.TotalSeconds > 5)
                {
                    ovrStarted = ovr.TryStart();
                    retry.Restart();
                }
            }
        }

        if (shouldContinue)
        {
            if (_registerAppManifest)
            {
                var isApplicationInstalled = OpenVR.Applications.IsApplicationInstalled(VrManifestAppKey);
                if (!isApplicationInstalled)
                {
                    OpenVR.Applications.AddApplicationManifest(Path.GetFullPath("manifest.vrmanifest"), false);
                }
            }
            _routine.InitializeAutoLaunch(OpenVR.Applications.GetApplicationAutoLaunch(VrManifestAppKey));

            var innerWindow = new HVInnerWindow(_routine, true, 1400, 1400, 1400, 800);
            innerWindow.SetupUi(true);
            
            var overlay = new HVOverlayInstance(innerWindow, "main", true, 1400 / 800f);
            overlay.Start();
        
            ovr.Run(stopwatch =>
            {
                overlay.ProvidePoseData(ovr.PoseData());
            
                // TODO: The update rate of the overlay UI event processing UI rendering may need to be independent
                // of the management of the overlay movement and poses.
                overlay.ProcessThatOverlay(stopwatch);
            
                // TODO: Update the desktop window at a different rate than the HMD
                var shouldContinue = desktopWindow.UpdateIteration(stopwatch);
                if (!shouldContinue)
                {
                    ovr.RequestExit();
                }
            
            }); // VR loop (blocking call)

            overlay.Teardown();
        
            innerWindow.TeardownWindowlessUi(true);
        }

        if (ovrStarted)
        {
            ovr.Teardown();
        }
        
        desktopWindow.TeardownWindowlessUi(false);
    }

    public void Finish()
    {
        _ovr.Teardown();
    }
}