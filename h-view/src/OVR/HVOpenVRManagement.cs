using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Hai.HView.OVR;
using Valve.VR;

namespace Hai.HView.Overlay;

public class HVOpenVRManagement
{
    public static readonly uint SizeOfVrEvent = (uint)Marshal.SizeOf(typeof(VREvent_t));
    private static readonly uint SizeOfActionSet = (uint)Marshal.SizeOf(typeof(VRActiveActionSet_t));

    // Overlay management
    private readonly HVPoseData _mgtPoseData = new HVPoseData
    {
        Poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount]
    };
    private bool _exitRequested;
    private readonly VRActiveActionSet_t[] _actionsets = new VRActiveActionSet_t[1];
    
    private ulong _actionSetHandle;
    private ulong _actionOpenLeft;
    private ulong _actionOpenRight;
    private ulong _actionInteract;
    public ulong ActionOpenLeft => _actionOpenLeft;
    public ulong ActionOpenRight => _actionOpenRight;
    public ulong ActionInteract => _actionInteract;

    public bool Start()
    {
        EVRInitError _err = EVRInitError.None;
        do
        {
            OpenVR.Init(ref _err, EVRApplicationType.VRApplication_Background);
            if (_err != EVRInitError.None)
            {
                Console.WriteLine($"Application could not start. Is SteamVR running?");
                Console.WriteLine($"        SteamVR error: {_err}");
                if (_exitRequested)
                {
                    Console.WriteLine($"Exiting application per user request");
                    return false;
                }

                Console.WriteLine($"Application will try again in 5 seconds...");
                Thread.Sleep(5000);
            }
        } while (_err != EVRInitError.None);

        return true;
    }

    public bool TryStart()
    {
        // We start as a Background app, so that it doesn't try to start SteamVR if it's not running.
        EVRInitError err = EVRInitError.None;
        OpenVR.Init(ref err, EVRApplicationType.VRApplication_Background);
        var isStarted = err == EVRInitError.None;
        if (isStarted)
        {
            var actionManifestPath = HAssets.ActionManifest.Absolute();
            if (!File.Exists(actionManifestPath))
            {
                Console.WriteLine($"{HAssets.ActionManifest.Relative()} does not exist.");
            }
            OpenVR.Input.SetActionManifestPath(actionManifestPath);
            
            OpenVR.Input.GetActionSetHandle("/actions/h_view", ref _actionSetHandle);
            OpenVR.Input.GetActionHandle("/actions/h_view/in/open_left", ref _actionOpenLeft);
            OpenVR.Input.GetActionHandle("/actions/h_view/in/open_right", ref _actionOpenRight);
            OpenVR.Input.GetActionHandle("/actions/h_view/in/interact", ref _actionInteract);

            _actionsets[0].ulActionSet = _actionSetHandle;
        }
        return isStarted;
    }

    public void Run(Action<Stopwatch> processInstances)
    {
        var stopwatch = new Stopwatch();
        while (!_exitRequested)
        {
            ExecuteLoopIter(processInstances, stopwatch);
        }
    }

    private void ExecuteLoopIter(Action<Stopwatch> processInstances, Stopwatch stopwatch)
    {
/* TODO
- We could separate the window update from the overlay update.
  - Some windows may not need to have their UI contents updated when their corresponding overlay is not visible.
  - Is there a need to decouple the overlay logic update rate from the UI update rate? (overlay position changes faster than the UI renders)
  - Should each window have a different update render rate?
*/
        ProcessOverlayManagement();
            
        processInstances.Invoke(stopwatch);
        
        stopwatch.Restart();
        if (OpenVR.Applications.GetSceneApplicationState() == EVRSceneApplicationState.Running)
        {
            OpenVR.Overlay.WaitFrameSync(1000 / 30);
        }
        else
        {
            Thread.Sleep(1000 / 60);
        }
    }

    public void Teardown()
    {
        _exitRequested = true;
        OpenVR.Shutdown();
    }

    private void ProcessOverlayManagement()
    {
        // TODO: These are related to the management of overlays (plural) in general.
        // Eventually we'll have to separate the concept of managing overlays, and managing that specific overlay bound to that ImGui window.
        OverlayManagementPollVREvents();
        OpenVR.Input.UpdateActionState(_actionsets, SizeOfActionSet);
        OverlayManagementFillPose();
    }

    private void OverlayManagementFillPose()
    {
        // Fill the pose data
            
        // TODO: Proper predicted seconds info
        var fPredictedSecondsToPhotonsFromNow = 0f;
        // TODO: Proper tracking universe
        OpenVR.System.GetDeviceToAbsoluteTrackingPose(OpenVR.Compositor.GetTrackingSpace(), fPredictedSecondsToPhotonsFromNow, _mgtPoseData.Poses);
        _mgtPoseData.LeftHandDeviceIndex = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
        _mgtPoseData.RightHandDeviceIndex = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
        _mgtPoseData.Interact = OpenVRUtils.GetDigitalInput(_actionInteract);
    }

    private void OverlayManagementPollVREvents()
    {
        VREvent_t evt = default;
        while (OpenVR.System.PollNextEvent(ref evt, SizeOfVrEvent))
        {
            var type = (EVREventType)evt.eventType;
            
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (type)
            {
                case EVREventType.VREvent_Quit:
                {
                    _exitRequested = true;
                    return; // Don't bother processing more events.
                }
            }
        }
    }

    public HVPoseData PoseData()
    {
        return _mgtPoseData;
    }

    public void RequestExit()
    {
        _exitRequested = true;
    }
}
