using System.Collections.Concurrent;
using System.Diagnostics;
using Hai.HView.Audio;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.Overlay;
using Hai.HView.Overlay.Stereocomposer;
using Hai.HView.Rendering;
using Hai.HView.Ui.MainApp;
using Valve.VR;

namespace Hai.HView.OVR;

public class HVOpenVRThread
{
    // Action manifest files cannot have a hyphen in it, it crashes the bindings UI when saving.
    
    public const int TotalWindowWidth = (int)(VRWindowWidth * 0.6f);
    public const int TotalWindowHeight = (int)(VRWindowHeight * 0.6f);
    private const int VRWindowWidth = 1400;
    private const int VRWindowHeight = 800;
    private const ushort HoverHapticPulseDurationMicroseconds = 25_000;
    private const ushort ButtonPressHapticPulseDurationMicroseconds = 50_000;
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
        var themeUpdater = new UiThemeUpdater();
        var desktopImageLoader = new HVImageLoader();
        var desktopMainApp = new UiMainApplication(_routine, false, TotalWindowWidth, TotalWindowHeight, TotalWindowWidth, TotalWindowHeight, _config, desktopImageLoader, themeUpdater);
        var desktopImGuiManagement = new HVRendering(false, TotalWindowWidth, TotalWindowHeight, desktopImageLoader, _config);
        desktopImGuiManagement.OnSubmitUi += desktopMainApp.SubmitUI;
        
        // - We start the desktop app.
        desktopImGuiManagement.SetupUi(false);
        
        // - We try to see if OpenVR is running (if it's not running, we don't want to start OpenVR).
        var ovr = _ovr;
        var ovrStarted = ovr.TryStart();

        var retry = new Stopwatch();
        retry.Start();

        var shouldContinue = true;
        {
            var sw = new Stopwatch();
            sw.Start();
            // - If it is not running, we update the desktop app anyway, and check if OpenVR is running every 5 seconds.
            // - Whenever OpenVR starts running, we exit this loop.
            // - Whenever we detect the application exiting, we exit this loop early.
            while (!ovrStarted)
            {
                var shouldRerender = desktopImGuiManagement.HandleSleep();
                if (shouldRerender)
                {
                    shouldContinue = desktopImGuiManagement.UpdateIteration(sw);
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
            _routine.SetOpenVrAvailable(true);
            
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
            var imageLoader = new HVImageLoader();
            // We pass the width twice (as width and height) because we want OpenVR to deal with a square texture, which will be trimmed by the overlay window ratio.
            var mainApp = new UiMainApplication(_routine, true, VRWindowWidth, VRWindowWidth, VRWindowWidth, VRWindowHeight, _config, imageLoader, themeUpdater);
            var imGuiManagement = new HVRendering(true, VRWindowWidth, VRWindowWidth, imageLoader, _config);
            imGuiManagement.OnSubmitUi += mainApp.SubmitUI;
            imGuiManagement.SetupUi(true);

            var windowRatio = VRWindowWidth / (VRWindowHeight * 1f);
            var dashboard = new HVImGuiOverlay(imGuiManagement, "main", true, windowRatio, mainApp);
            dashboard.Start();
            
            mainApp.RegisterHoverChanged(() =>
            {
                _queuedForOvr.Enqueue(() => OpenVRUtils.TriggerHapticPulse(dashboard.LastMouseMoveDeviceIndex, HoverHapticPulseDurationMicroseconds));
            });
            
            mainApp.RegisterButtonPressed(() =>
            {
                _queuedForOvr.Enqueue(() =>
                {
                    OpenVRUtils.TriggerHapticPulse(dashboard.LastMouseMoveDeviceIndex, ButtonPressHapticPulseDurationMicroseconds);
                    _playSound ??= new PlaySound(HAssets.ClickAudio.Absolute());
                    _playSound.Play();
                });
            });

            var overlayables = new List<IOverlayable>();
            overlayables.Add(dashboard);
            
            HEyeTrackingOverlay eyeTrackingOptional = null;

            HHandOverlay handOverlay = null;
            var onShowCostumes = () => _queuedForOvr.Enqueue(() =>
            {
                handOverlay = new HHandOverlay(imGuiManagement, mainApp, windowRatio, _routine, false);
                handOverlay.Start();
                handOverlay.MoveToInitialPosition(ovr.PoseData());
                eyeTrackingOptional?.SetHandOverlay(handOverlay);
                mainApp.SetIsHandOverlay(true);
                
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
                mainApp.SetIsHandOverlay(false);
                
                handOverlay = null;
            });

            _routine.OnShowCostumes += onShowCostumes;
            _routine.OnHideCostumes += onHideCostumes;

            var togglables = new List<HTogglableOverlay>
            {
                new(overlayables, () =>
                {
                    var result = new HEyeTrackingOverlay(_routine, dashboard);
                    eyeTrackingOptional = result;
                    return result;
                }, () => _config.devTools__EyeTracking),
                new(overlayables, () => new HStereocomposer(_config, _routine, new HGrabMachine(), _ovr), () => _config.devTools__StereoComposer)
            };

            ovr.Run(stopwatch =>
            {
                while (_queuedForOvr.TryDequeue(out var action)) action();
                
                foreach (var togglable in togglables)
                {
                    togglable.Check();
                }

                if (OpenVRUtils.IfDigitalInputChanged(_ovr.ActionInteract, out var interact) && interact)
                {
                    _routine.InteractDown();
                }

                if (OpenVRUtils.IfDigitalInputChanged(_ovr.ActionOpenRight, out var openRight) && openRight)
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
                var shouldContinue = desktopImGuiManagement.UpdateIteration(stopwatch);
                if (!shouldContinue)
                {
                    ovr.RequestExit();
                }
            
            }); // VR loop (blocking call)
            
            _routine.OnShowCostumes -= onShowCostumes;
            _routine.OnHideCostumes -= onHideCostumes;

            foreach (var overlayable in overlayables) overlayable.Teardown();
        
            imGuiManagement.TeardownWindowlessUi(true);
        }

        if (ovrStarted)
        {
            ovr.Teardown();
            _routine.SetOpenVrAvailable(false);
        }
        
        desktopImGuiManagement.TeardownWindowlessUi(false);
    }

    public void Finish()
    {
        _ovr.Teardown();
    }
}