using System.Numerics;
using Veldrid;

namespace Hai.HView.Overlay;

public class HOverlayInputSnapshot : InputSnapshot
{
    public bool IsMouseDown(MouseButton button) => _mouseDown[(int)button];
    public IReadOnlyList<KeyEvent> KeyEvents => _keyEvents;
    public IReadOnlyList<MouseEvent> MouseEvents => _mouseEvents;
    public IReadOnlyList<char> KeyCharPresses => _keyCharPresses;
    public Vector2 MousePosition { get; private set; } = Vector2.Zero;
    public float WheelDelta { get; private set; }
    
    private readonly List<KeyEvent> _keyEvents = new List<KeyEvent>();
    private readonly List<MouseEvent> _mouseEvents = new List<MouseEvent>();
    private readonly List<char> _keyCharPresses = new List<char>();
    private readonly bool[] _mouseDown = new bool[(int)MouseButton.LastButton];
    private Vector2 _windowSize;

    public void SetWindowSize(Vector2 windowSize)
    {
        _windowSize = windowSize;
    }

    public void MouseMove(Vector2 relativeCoords)
    {
        if (_windowSize.X != _windowSize.Y)
        {
            var rescaled = relativeCoords;
            rescaled.Y = Math.Clamp((rescaled.Y - 0.5f) / (_windowSize.Y / _windowSize.X) + 0.5f, 0f, 1f);
            MousePosition = rescaled * _windowSize;
        }
        else
        {
            MousePosition = relativeCoords * _windowSize;
        }
    }

    public void Deaccumulate()
    {
        WheelDelta = 0f;
        _mouseEvents.Clear();
    }

    public void Scrolling(float delta)
    {
        WheelDelta += delta;
    }

    public void MouseDown(MouseButton veldridButton)
    {
        _mouseEvents.Add(new MouseEvent(veldridButton, true));
        _mouseDown[(int)veldridButton] = true;
    }

    public void MouseUp(MouseButton veldridButton)
    {
        _mouseEvents.Add(new MouseEvent(veldridButton, false));
        _mouseDown[(int)veldridButton] = false;
    }
}