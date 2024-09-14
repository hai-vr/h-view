using System.Diagnostics;

namespace Hai.HView.Overlay;

public interface IOverlayable
{
    void Start();
    void ProvidePoseData(HVPoseData poseData);
    void ProcessThatOverlay(Stopwatch stopwatch);
    void Teardown();
}