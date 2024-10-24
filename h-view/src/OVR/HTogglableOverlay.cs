using Hai.HView.Overlay;

namespace Hai.HView.OVR;

public class HTogglableOverlay
{
    private readonly Func<IOverlayable> _overlayFactoryFn;
    private readonly List<IOverlayable> _overlayables;
    private readonly Func<bool> _checkerFn;
    private bool _previous;
    private IOverlayable _overlayLateInit;

    public HTogglableOverlay(List<IOverlayable> overlayables, Func<IOverlayable> overlayFnFactoryFn, Func<bool> checkerFn)
    {
        _overlayFactoryFn = overlayFnFactoryFn;
        _overlayables = overlayables;
        _checkerFn = checkerFn;
        _previous = false;
    }
    
    public void Check()
    {
        var current = _checkerFn.Invoke();
        if (current != _previous)
        {
            _previous = current;
            if (current)
            {
                _overlayLateInit = _overlayFactoryFn.Invoke();
                _overlayLateInit.Start();
                _overlayables.Add(_overlayLateInit);
            }
            else
            {
                _overlayLateInit.Teardown();
                _overlayables.Remove(_overlayLateInit);
                _overlayLateInit = null;
            }
        }
    }
}