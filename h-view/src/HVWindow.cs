using Hai.HView.Core;

namespace Hai.HView.Gui;

public class HVWindow
{
    private const int TotalWindowWidth = 600;
    private const int TotalWindowHeight = 510;
    
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
        new HVInnerWindow(_routine, _simulateWindowlessStyle, TotalWindowWidth, TotalWindowHeight, TotalWindowWidth, TotalWindowHeight).UiLoop(); // This call blocks until the user closes the window.
        _whenWindowClosed();
    }
}