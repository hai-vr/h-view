using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.OVR;
using Hai.HView.Rendering;
using Hai.HView.Ui.MainApp;

namespace Hai.HView.Gui;

public class HVDesktopStarter
{
    private const int TotalWindowWidth = HVOpenVRThread.TotalWindowWidth;
    private const int TotalWindowHeight = HVOpenVRThread.TotalWindowHeight;
    
    private readonly HVRoutine _routine;
    private readonly Action _whenWindowClosed;
    private readonly bool _simulateWindowlessStyle;
    private SavedData _config;

    public HVDesktopStarter(HVRoutine routine, Action whenWindowClosed, bool simulateWindowlessStyle, SavedData config)
    {
        _routine = routine;
        _whenWindowClosed = whenWindowClosed;
        _simulateWindowlessStyle = simulateWindowlessStyle;
        _config = config;
    }

    public void Run()
    {
        var themeUpdater = new UiThemeUpdater();
        var imageLoader = new HVImageLoader();
        var mainApp = new UiMainApplication(_routine, _simulateWindowlessStyle, TotalWindowWidth, TotalWindowHeight, TotalWindowWidth, TotalWindowHeight, _config, imageLoader, themeUpdater);
        var imGuiManagement = new HVRendering(_simulateWindowlessStyle, TotalWindowWidth, TotalWindowHeight, imageLoader, _config);
        imGuiManagement.OnSubmitUi += mainApp.SubmitUI;
        
        imGuiManagement.UiLoop(); // This call blocks until the user closes the window.
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