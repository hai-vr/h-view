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
        MousePosition = relativeCoords * _windowSize;
    }

    public void Deaccumulate()
    {
        WheelDelta = 0f;
    }

    public void Scrolling(float delta)
    {
        Console.WriteLine($"Scrolling event {delta}");
        WheelDelta += delta;
    }

    public void MouseDown(MouseButton veldridButton)
    {
        Console.WriteLine($"MouseDown event {veldridButton}");
        _mouseEvents.Add(new MouseEvent(veldridButton, true));
        _mouseDown[(int)veldridButton] = true;
    }

    public void MouseUp(MouseButton veldridButton)
    {
        Console.WriteLine($"MouseUp event {veldridButton}");
        _mouseEvents.Add(new MouseEvent(veldridButton, false));
        _mouseDown[(int)veldridButton] = false;
    }
}