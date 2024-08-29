using Hai.HView.Core;
using Hai.HView.Overlay;

namespace Hai.HView.Gui;

public class HVWindowless
{
    private readonly HVRoutine _routine;
    private readonly Action _whenWindowClosed;

    public HVWindowless(HVRoutine routine, Action whenWindowClosed)
    {
        _routine = routine;
        _whenWindowClosed = whenWindowClosed;
    }

    public void Run()
    {
        var innerWindow = new HVInnerWindow(_routine, true);
        innerWindow.SetupWindowlessUi();
        
        var overlay = new HVOverlay(innerWindow);
        var success = overlay.Start();
        if (success)
        {
            overlay.Run(); // VR loop (blocking call)
        }
        innerWindow.TeardownWindowlessUi();
        _whenWindowClosed.Invoke();
    }
}