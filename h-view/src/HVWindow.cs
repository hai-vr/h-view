using Hai.HView.Core;

namespace Hai.HView.Gui;

public class HVWindow
{
    private readonly HVRoutine _routine;
    private readonly Action _whenWindowClosed;
    private readonly bool _simulateWindowlessStyle;

    public HVWindow(HVRoutine routine, Action whenWindowClosed, bool simulateWindowlessStyle)
    {
        _routine = routine;
        _whenWindowClosed = whenWindowClosed;
        _simulateWindowlessStyle = simulateWindowlessStyle;
    }

    public void Run()
    {
        new HVInnerWindow(_routine, _simulateWindowlessStyle).UiLoop(); // This call blocks until the user closes the window.
        _whenWindowClosed();
    }
}