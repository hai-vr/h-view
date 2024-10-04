using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.OVR;

namespace Hai.HView.Gui;

public class HVWindow
{
    private const int TotalWindowWidth = HVOpenVRThread.TotalWindowWidth;
    private const int TotalWindowHeight = HVOpenVRThread.TotalWindowHeight;
    
    private readonly HVRoutine _routine;
    private readonly Action _whenWindowClosed;
    private readonly bool _simulateWindowlessStyle;
    private SavedData _config;

    public HVWindow(HVRoutine routine, Action whenWindowClosed, bool simulateWindowlessStyle, SavedData config)
    {
        _routine = routine;
        _whenWindowClosed = whenWindowClosed;
        _simulateWindowlessStyle = simulateWindowlessStyle;
        _config = config;
    }

    public void Run()
    {
        new HVInnerWindow(_routine, _simulateWindowlessStyle, TotalWindowWidth, TotalWindowHeight, TotalWindowWidth, TotalWindowHeight, _config).UiLoop(); // This call blocks until the user closes the window.
        _whenWindowClosed();
    }
}

public class HVOvrStarter
{
    private readonly HVOpenVRThread _ovrThread;
    private readonly Action _whenWindowClosed;

    public HVOvrStarter(HVOpenVRThread ovrThread, Action whenWindowClosed)
    {
        _ovrThread = ovrThread;
        _whenWindowClosed = whenWindowClosed;
    }

    public void Run()
    {
        _ovrThread.Run(); // Loops until desktop window is closed.
        _whenWindowClosed();
    }
}