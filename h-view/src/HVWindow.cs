using Hai.HView.Core;

namespace Hai.HView.Gui;

public class HVWindow
{
    private readonly HVRoutine _routine;
    private readonly Action _whenWindowClosed;

    public HVWindow(HVRoutine routine, Action whenWindowClosed)
    {
        _routine = routine;
        _whenWindowClosed = whenWindowClosed;
    }

    public void Run()
    {
        new HVInnerWindow(_routine).Run();
        _whenWindowClosed();
    }
}