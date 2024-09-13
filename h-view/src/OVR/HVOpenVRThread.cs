using System.Diagnostics;
using System.Reflection;
using Hai.HView.Core;
using Hai.HView.Gui;
using Hai.HView.Overlay;
using Valve.VR;

namespace Hai.HView.OVR;

public class HVOpenVRThread
{
    // Action manifest files cannot have a hyphen in it, it crashes the bindings UI when saving.
    private const string ActionManifestFileName = "h_view_actions.json";
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

        if (ovrStarted)
        {
            var actionManifestPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ActionManifestFileName);
            if (!File.Exists(actionManifestPath))
            {
                Console.WriteLine($"{ActionManifestFileName} does not exist.");
            }
            OpenVR.Input.SetActionManifestPath(actionManifestPath);
            
            // We don't want the recorded app manifest file to change when trying debug builds.
            if (_registerAppManifest)
            {
                var isApplicationInstalled = OpenVR.Applications.IsApplicationInstalled(VrManifestAppKey);
                if (!isApplicationInstalled)
                {
                    OpenVR.Applications.AddApplicationManifest(Path.GetFullPath("manifest.vrmanifest"), false);
                }
            }
            _routine.InitializeAutoLaunch(OpenVR.Applications.GetApplicationAutoLaunch(VrManifestAppKey));
        }

        if (shouldContinue)
        {
            var width = 1400;
            var height = 800;
            var innerWindow = new HVInnerWindow(_routine, true, width, width, width, height);
            innerWindow.SetupUi(true);

            var windowRatio = width / (height * 1f);
            var dashboard = new HVOverlayInstance(innerWindow, "main", true, windowRatio);
            dashboard.Start();

            HHandOverlay handOverlay = null;
            var onShowCostumes = () =>
            {
                handOverlay = new HHandOverlay(innerWindow, windowRatio, _routine, false);
                handOverlay.Start();
                handOverlay.MoveToInitialPosition(ovr.PoseData());
            };
            var onHideCostumes = () =>
            {
                // FIXME: Executing things on the correct thread is really messy at the moment.
                var overlay = handOverlay;
                handOverlay = null;
                overlay.Teardown();
            };
            
            _routine.OnShowCostumes += onShowCostumes;
            _routine.OnHideCostumes += onHideCostumes;
        
            ovr.Run(stopwatch =>
            {
                var poseData = ovr.PoseData();
                
                dashboard.ProvidePoseData(poseData);
                handOverlay?.ProvidePoseData(poseData);
            
                // TODO: The update rate of the overlay UI event processing UI rendering may need to be independent
                // of the management of the overlay movement and poses.
                dashboard.ProcessThatOverlay(stopwatch);
                handOverlay?.ProcessThatOverlay(stopwatch);
            
                // TODO: Update the desktop window at a different rate than the HMD
                var shouldContinue = desktopWindow.UpdateIteration(stopwatch);
                if (!shouldContinue)
                {
                    ovr.RequestExit();
                }
            
            }); // VR loop (blocking call)
            _routine.OnShowCostumes -= onShowCostumes;
            _routine.OnHideCostumes -= onHideCostumes;

            dashboard.Teardown();
            handOverlay?.Teardown();
        
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