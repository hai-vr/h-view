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