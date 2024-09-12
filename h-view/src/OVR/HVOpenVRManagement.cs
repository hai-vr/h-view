using System.Diagnostics;
using Valve.VR;

namespace Hai.HView.Overlay;

public class HVOpenVRManagement
{
    public static readonly uint SizeOfVrEvent = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t));
    
    private ulong _handle;
    private Texture_t _vrTexture;
    
    // Overlay management
    private readonly HVPoseData _mgtPoseData = new HVPoseData
    {
        Poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount]
    };
    private bool _exitRequested;

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
        EVRInitError _err = EVRInitError.None;
        OpenVR.Init(ref _err, EVRApplicationType.VRApplication_Background);
        return _err == EVRInitError.None;
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
        Maybe we should decouple the following for future evolutions:
        - In Windowless mode, consider the instantiation of multiple ImGui windows, so that we can have multiple overlays.
          - There may be multiple overlays for the same window (???).
          - A single window may be used to render the UI of multiple overlays, provided they are given a different data model.
          - Overlay mouse events may be on a per-overlay basis, rather than be on a per-window basis.
          - Consider processing mouse events and rendering all visible window UIs at once, and then run the overlay logic.
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
        OverlayManagementFillPose();
    }

    private void OverlayManagementFillPose()
    {
        // Fill the pose data
            
        // TODO: Proper predicted seconds info
        var fPredictedSecondsToPhotonsFromNow = 0f;
        // TODO: Proper tracking universe
        OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, fPredictedSecondsToPhotonsFromNow, _mgtPoseData.Poses);
        _mgtPoseData.LeftHandDeviceIndex = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
        _mgtPoseData.RightHandDeviceIndex = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
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
