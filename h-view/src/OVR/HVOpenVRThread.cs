using System.Collections.Concurrent;
using System.Diagnostics;
using Hai.HView.Audio;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.Gui;
using Hai.HView.Overlay;
using Valve.VR;

namespace Hai.HView.OVR;

public class HVOpenVRThread
{
    // Action manifest files cannot have a hyphen in it, it crashes the bindings UI when saving.
    
    public const int TotalWindowWidth = (int)(VRWindowWidth * 0.6f);
    public const int TotalWindowHeight = (int)(VRWindowHeight * 0.6f);
    private const int VRWindowWidth = 1400;
    private const int VRWindowHeight = 800;
    public const string VrManifestAppKey = "Hai.HView";

    private readonly HVRoutine _routine;
    private readonly HVOpenVRManagement _ovr;
    private readonly bool _registerAppManifest;
    private readonly ConcurrentQueue<Action> _queuedForOvr = new ConcurrentQueue<Action>();
    private readonly SavedData _config;
    private PlaySound _playSound;

    public HVOpenVRThread(HVRoutine routine, bool registerAppManifest, SavedData config)
    {
        _routine = routine;
        _registerAppManifest = registerAppManifest;
        _config = config;
        _ovr = new HVOpenVRManagement();
    }

    public void Run()
    {
        var desktopWindow = new HVInnerWindow(_routine, false, TotalWindowWidth, TotalWindowHeight, TotalWindowWidth, TotalWindowHeight, _config);
        desktopWindow.SetupUi(false);
        
        var ovr = _ovr;
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
            // We don't want the recorded app manifest file to change when trying debug builds.
            if (_registerAppManifest)
            {
                var isApplicationInstalled = OpenVR.Applications.IsApplicationInstalled(VrManifestAppKey);
                if (!isApplicationInstalled)
                {
                    OpenVR.Applications.AddApplicationManifest(HAssets.ApplicationManifest.Absolute(), false);
                }
            }
            _routine.InitializeAutoLaunch(OpenVR.Applications.GetApplicationAutoLaunch(VrManifestAppKey));
        }

        if (shouldContinue)
        {
            var innerWindow = new HVInnerWindow(_routine, true, VRWindowWidth, VRWindowWidth, VRWindowWidth, VRWindowHeight, _config);
            innerWindow.SetupUi(true);

            var windowRatio = VRWindowWidth / (VRWindowHeight * 1f);
            var dashboard = new HVImGuiOverlay(innerWindow, "main", true, windowRatio);
            dashboard.Start();
            
            innerWindow.OnHoverChanged += () =>
            {
                _queuedForOvr.Enqueue(() => OpenVR.System.TriggerHapticPulse(dashboard.LastMouseMoveDeviceIndex, 0, 25_000));
            };
            
            innerWindow.OnButtonPressed += () =>
            {
                _queuedForOvr.Enqueue(() =>
                {
                    OpenVR.System.TriggerHapticPulse(dashboard.LastMouseMoveDeviceIndex, 0, 50_000);
                    _playSound ??= new PlaySound(HAssets.ClickAudio.Absolute());
                    _playSound.Play();
                });
            };

            var overlayables = new List<IOverlayable>();
            overlayables.Add(dashboard);
            
            HEyeTrackingOverlay eyeTrackingOptional = null;
            
            HHandOverlay handOverlay = null;
            var onShowCostumes = () => _queuedForOvr.Enqueue(() =>
            {
                handOverlay = new HHandOverlay(innerWindow, windowRatio, _routine, false);
                handOverlay.Start();
                handOverlay.MoveToInitialPosition(ovr.PoseData());
                eyeTrackingOptional?.SetHandOverlay(handOverlay);
                innerWindow.SetIsHandOverlay(true);
                
                overlayables.Add(handOverlay);
            });
            var onHideCostumes = () => _queuedForOvr.Enqueue(() =>
            {
                if (handOverlay == null)
                {
                    Console.WriteLine("Broken assumption: onHideCostumes was called while handOverlay is null");
                    return;
                }
                handOverlay.Teardown();
                eyeTrackingOptional?.SetHandOverlay(null);
                overlayables.Remove(handOverlay);
                innerWindow.SetIsHandOverlay(false);
                
                handOverlay = null;
            });

            _routine.OnShowCostumes += onShowCostumes;
            _routine.OnHideCostumes += onHideCostumes;
        
            ovr.Run(stopwatch =>
            {
                while (_queuedForOvr.TryDequeue(out var action)) action();

                var useEyeTracking = _config.devTools__EyeTracking;
                if (useEyeTracking && eyeTrackingOptional == null)
                {
                    eyeTrackingOptional = new HEyeTrackingOverlay(_routine, dashboard);
                    eyeTrackingOptional.Start();
                    overlayables.Add(eyeTrackingOptional);
                }
                else if (!useEyeTracking && eyeTrackingOptional != null)
                {
                    dashboard.ForgetEyeTracking();
                    eyeTrackingOptional.Teardown();
                    overlayables.Remove(eyeTrackingOptional);
                    eyeTrackingOptional = null;
                }

                var data = OpenVRUtils.GetDigitalInput(_ovr.ActionOpenRight);
                if (data.bChanged && data.bState)
                {
                    _routine.ToggleCostumes();
                }

                var poseData = ovr.PoseData();
                _routine.HardwareUpdateIfNecessary();
                
                foreach (var overlayable in overlayables) overlayable.ProvidePoseData(poseData);
            
                // TODO: The update rate of the overlay UI event processing UI rendering may need to be independent
                // of the management of the overlay movement and poses.
                foreach (var overlayable in overlayables) overlayable.ProcessThatOverlay(stopwatch);
            
                // TODO: Update the desktop window at a different rate than the HMD
                var shouldContinue = desktopWindow.UpdateIteration(stopwatch);
                if (!shouldContinue)
                {
                    ovr.RequestExit();
                }
            
            }); // VR loop (blocking call)
            
            _routine.OnShowCostumes -= onShowCostumes;
            _routine.OnHideCostumes -= onHideCostumes;

            foreach (var overlayable in overlayables) overlayable.Teardown();
        
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