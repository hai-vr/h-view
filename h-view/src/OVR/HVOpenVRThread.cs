using System.Diagnostics;
using System.Numerics;
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

            var width = 1400;
            var height = 800;
            var innerWindow = new HVInnerWindow(_routine, true, width, width, width, height);
            innerWindow.SetupUi(true);

            var windowRatio = width / (height * 1f);
            var dashboard = new HVOverlayInstance(innerWindow, "main", true, windowRatio);
            dashboard.Start();

            HVOverlayInstance costumesNullable = null;
            var onShowCostumes = () =>
            {
                costumesNullable = new HVOverlayInstance(innerWindow, "costumes", false, windowRatio);
                costumesNullable.Start();
                // TODO: Show should be moved into the overlay iteself.
                var poseData = ovr.PoseData();
                if (HVOverlayMovement.IsValidDeviceIndex(poseData.RightHandDeviceIndex))
                {
                    // FIXME: Executing things on the correct thread is really messy at the moment.
                    var pos = new Vector3(0f, -0.01f, -0.2f);
                    var angles = new Vector3(65, 0, 0);
                    var rot = HVGeofunctions.QuaternionFromAngles(angles, HVRotationMulOrder.YZX);
                    var handToOverlayPlace = HVOvrGeofunctions.OvrToOvrnum(HVOvrGeofunctions.OvrTRS(pos, rot, Vector3.One));
                    
                    var absToHandPlace = HVOvrGeofunctions.OvrToOvrnum(ovr.PoseData().Poses[poseData.RightHandDeviceIndex].mDeviceToAbsoluteTracking);
                    var absToOverlayPlace = HVOvrGeofunctions.OvrnumToOvr(absToHandPlace * handToOverlayPlace);

                    OpenVR.Overlay.SetOverlayTransformAbsolute(costumesNullable.GetOverlayHandle(), ETrackingUniverseOrigin.TrackingUniverseStanding, ref absToOverlayPlace);
                    OpenVR.Overlay.SetOverlayFlag(costumesNullable.GetOverlayHandle(), VROverlayFlags.MakeOverlaysInteractiveIfVisible, true);
                }
            };
            var onHideCostumes = () =>
            {
                // FIXME: Executing things on the correct thread is really messy at the moment.
                var overlay = costumesNullable;
                costumesNullable = null;
                OpenVR.Overlay.SetOverlayFlag(overlay.GetOverlayHandle(), VROverlayFlags.MakeOverlaysInteractiveIfVisible, false);
                overlay.Teardown();
            };
            
            _routine.OnShowCostumes += onShowCostumes;
            _routine.OnHideCostumes += onHideCostumes;
        
            ovr.Run(stopwatch =>
            {
                dashboard.ProvidePoseData(ovr.PoseData());
                costumesNullable?.ProvidePoseData(ovr.PoseData());
            
                // TODO: The update rate of the overlay UI event processing UI rendering may need to be independent
                // of the management of the overlay movement and poses.
                dashboard.ProcessThatOverlay(stopwatch);
                costumesNullable?.ProcessThatOverlay(stopwatch);
            
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
            costumesNullable?.Teardown();
        
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