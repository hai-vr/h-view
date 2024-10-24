using System.Numerics;
using Hai.HView.Rendering;
using ImGuiNET;

namespace Hai.HView.Ui.MainApp;

internal class UiEyeTrackingMenu
{
    private const float IconBorder = 3;
    private const float DefaultIconWidth = 128;
    
    private float _iconWidth;
    private float _iconHeight;
    private float _radialWidth;
    private float _radialHeight;
    private float _linearWidth;
    private float _linearHeight;

    private readonly ImGuiVRCore VrGui;
    private readonly bool _isWindowlessStyle;
    private readonly HVImageLoader _imageLoader;
    private readonly UiSharedData _sharedData;
    private readonly Stack<MenuState> _menuState = new Stack<MenuState>();
    
    // TODO: SHARED between windowed and overlay, this needs to go in the config file
    private static bool _mode;
    private static bool _staggered = true;
    private static float _iconSizeMul = 1f;
    private static float _iconSpacingMul = 1.228f;

    public UiEyeTrackingMenu(ImGuiVRCore vrGui, bool isWindowlessStyle, HVImageLoader imageLoader, UiSharedData sharedData)
    {
        VrGui = vrGui;
        _isWindowlessStyle = isWindowlessStyle;
        _imageLoader = imageLoader;
        _sharedData = sharedData;
    }

    public void EyeTrackingMenuTab()
    {
        if (_sharedData.ShortcutsNullable == null) return;
        if (_sharedData.ManifestNullable == null) return;
        
        _iconWidth = NearestMultipleOfTwo(DefaultIconWidth * _iconSizeMul);
        _iconHeight = NearestMultipleOfTwo(_iconWidth);
        _radialWidth = NearestMultipleOfTwo(_iconWidth * 1.65f * _iconSpacingMul);
        _radialHeight = NearestMultipleOfTwo(_iconHeight * 1.65f * _iconSpacingMul);
        _linearWidth = NearestMultipleOfTwo(_iconWidth * _iconSpacingMul);
        _linearHeight = NearestMultipleOfTwo(_iconHeight * _iconSpacingMul);

        if (!_isWindowlessStyle)
        {
            if (VrGui.HapticCheckbox("Radial layout", ref _mode))
            {
                if (_staggered && _mode) _staggered = false;
            }
            ImGui.SameLine();
            if (VrGui.HapticCheckbox("Staggered layout", ref _staggered))
            {
                if (_staggered && _mode) _mode = false;
            }
            ImGui.SliderFloat("Size mul", ref _iconSizeMul, 1f, 2f);
            ImGui.SliderFloat("Spacing mul", ref _iconSpacingMul, 1f, 2f);
        }

        var icons = _sharedData.ManifestNullable.icons;
        
        if (VrGui.HapticButton("Home") && _menuState.Count > 0)
        {
            _menuState.Clear();
        }
        ImGui.SameLine();
        if (VrGui.HapticButton("Back") && _menuState.Count > 0)
        {
            _menuState.Pop();
        }

        if (_menuState.Count == 0)
        {
            ShowMenu(_sharedData.ShortcutsNullable, 0, 0, icons);
        }
        else
        {
            var menuState = _menuState.Peek();
            ShowMenu(menuState.Host, menuState.X, menuState.Y, icons);
        }
    }

    private float NearestMultipleOfTwo(float size)
    {
        var intSize = (int)MathF.Floor(size);
        var resultSize = intSize - intSize % 2;
        return resultSize;
    }

    private void ShowMenu(HVShortcutHost host, float x, float y, string[] icons)
    {
        var xCenter = ImGui.GetWindowWidth() / 2;
        var yCenter = ImGui.GetWindowHeight() / 2;

        var isBackable = _menuState.Count > 0;
        if (isBackable && false)
        {
            if (_mode)
            {
                ImGui.SetCursorPosX(xCenter - (_iconWidth + IconBorder) / 2);
                ImGui.SetCursorPosY(yCenter - (_iconHeight + IconBorder) / 2);
            }
            else
            {
                
            }

            if (DrawEyeTrackingButtonFor(-1, icons, new HVShortcut
                {
                    label = "Back",
                    type = HVShortcutType.Button,
                    icon = -1
                }))
            {
                _menuState.Pop();
            }
        }
        
        var itemCount = host.everything.Length + (isBackable ? 1 : 0);
        for (var clocked = 0; clocked < itemCount; clocked++)
        {
            var shortcutNullWhenUnclocked = isBackable && clocked == 0 ? null : host.everything[isBackable ? clocked - 1 : clocked];
            if (_mode) // Using radial layout
            {
                var angle01 = clocked / (1f * itemCount);
                var angleRad = (angle01 - 0.25f) * MathF.PI * 2;
                var xJoy = MathF.Cos(angleRad);
                var yJoy = MathF.Sin(angleRad);

                var xShift = (x + xJoy) * _radialWidth;
                var yShift = (y + yJoy) * _radialHeight;
                ImGui.SetCursorPosX(xCenter + xShift - (_iconWidth + IconBorder) / 2);
                ImGui.SetCursorPosY(yCenter + yShift - (_iconHeight + IconBorder) / 2);
            }
            else if (_staggered)
            {
                var (xx, yy) = StaggerItems(clocked, itemCount);
                ImGui.SetCursorPosX(xCenter + (_linearWidth + IconBorder * 2 + 10) * xx - (_iconWidth + IconBorder) / 2);
                ImGui.SetCursorPosY(yCenter + (_linearHeight + IconBorder * 2 + 10) * yy - (_iconHeight + IconBorder) / 2);
            }
            else
            {
                var xx = clocked - itemCount / 2 - 0.5f;
                ImGui.SetCursorPosX(xCenter + (_linearWidth + IconBorder * 2 + 10) * xx );
                ImGui.SetCursorPosY(yCenter - (_iconHeight + IconBorder) / 2);
            }

            var pos = ImGui.GetCursorPos();
            if (shortcutNullWhenUnclocked != null && DrawEyeTrackingButtonFor(clocked, icons, shortcutNullWhenUnclocked))
            {
                if (shortcutNullWhenUnclocked.type == HVShortcutType.SubMenu)
                {
                    _menuState.Push(new MenuState
                    {
                        Host = shortcutNullWhenUnclocked.subs,
                        Name = shortcutNullWhenUnclocked.label,
                        Id = shortcutNullWhenUnclocked.subMenuId
                    });
                }
            }
            else if (shortcutNullWhenUnclocked == null)
            {
                if (DrawEyeTrackingButtonFor(-2, icons, new HVShortcut
                    {
                        label = "Back",
                        type = HVShortcutType.Button,
                        icon = -1
                    }))
                {
                    _menuState.Pop();
                }
            }

            var label = shortcutNullWhenUnclocked != null ? shortcutNullWhenUnclocked.label : "Back";
            var potentialWidth = ImGui.CalcTextSize(label);
            
            ImGui.SetCursorPosY(pos.Y + (_iconHeight + IconBorder * 2) + 5);
            ImGui.SetCursorPosX(pos.X + (float)(_iconWidth / 2 + IconBorder) - potentialWidth.X / 2f);
            ImGui.Text(label);
        }
    }

    private (float, float) StaggerItems(int index, int itemCount)
    {
        switch (itemCount)
        {
            case <= 1:
                return (0f, 0f);
            case <= 3:
                return (StaggerLine(index, itemCount), 0f);
            case 4:
                switch (index)
                {
                    case 0: return (-0.5f, -0.5f);
                    case 1: return (0.5f, -0.5f);
                    case 2: return (0, 0.5f);
                    case 3: return (1f, 0.5f);
                    default: throw new IndexOutOfRangeException();
                }
            case 5:
            {
                var itemsInFirstRow = 2;
                if (index < itemsInFirstRow) return (StaggerLine(index, itemsInFirstRow), -0.5f);
                return (StaggerLine(index - itemsInFirstRow, 3), 0.5f);
            }
            case 6:
            {
                var firstRow = 3;
                if (index < firstRow) return (StaggerLine(index, firstRow) - 0.25f, -0.5f);
                return (StaggerLine(index - firstRow, 3) + 0.25f, 0.5f);
            }
            case 7:
            {
                var firstRow = 2;
                var secondRow = 3;
                if (index < firstRow) return (StaggerLine(index, firstRow), -1);
                if (index < firstRow + secondRow) return (StaggerLine(index - firstRow, secondRow), 0);
                return (StaggerLine(index - firstRow - secondRow, 2), 1f);
            }
            default:
            {
                var firstRow = 3;
                var secondRow = 4;
                if (index < firstRow) return (StaggerLine(index, firstRow), -1);
                if (index < firstRow + secondRow) return (StaggerLine(index - firstRow, secondRow), 0);
                return (StaggerLine(index - firstRow - secondRow, 3), 1f);
            }
        }
    }

    private static float StaggerLine(int index, int itemsInRow)
    {
        return index - itemsInRow / 2f + 0.5f;
    }

    private bool DrawEyeTrackingButtonFor(int id, string[] icons, HVShortcut item)
    {
        // No haptics here, the eyes are the controllers.
        
        bool button;
        if (item.icon != -1)
        {
            button = VrGui.HapticImageButton($"###{id}", _imageLoader.GetOrLoadImage(icons, item.icon), new Vector2(_iconWidth, _iconHeight));
        }
        else
        {
            button = VrGui.HapticButton($"?###{id}", new Vector2(_iconWidth + IconBorder * 2, _iconHeight + IconBorder * 2));
        }

        return button;
    }

    private struct MenuState
    {
        public int Id;
        public string Name;
        public HVShortcutHost Host;
        public float X;
        public float Y;
    }
}