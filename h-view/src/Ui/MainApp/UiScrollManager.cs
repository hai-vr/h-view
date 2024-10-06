using System.Numerics;
using ImGuiNET;

namespace Hai.HView.Ui.MainApp;

internal class UiScrollManager
{
    private bool _isScrollDragging;
    private bool _anyHighlightLastFrame;

    public void MakeTab(string tabLabel, Action action)
    {
        if (ImGui.BeginTabItem(tabLabel))
        {
            MakeScroll(action);
        }
    }

    public void MakeScroll(Action action)
    {
        ImGui.BeginChild("scroll");
        action.Invoke();
        HandleScrollOnDrag();
        ImGui.EndChild();
        ImGui.EndTabItem();
    }

    public void MakeUnscrollableTab(string tabLabel, Action action)
    {
        if (ImGui.BeginTabItem(tabLabel))
        {
            action.Invoke();
            ImGui.EndTabItem();
        }
    }

    private void HandleScrollOnDrag()
    {
        DoHandleScrollOnDrag(ImGui.GetIO().MouseDelta, ImGuiMouseButton.Left);
    }

    private void DoHandleScrollOnDrag(Vector2 delta, ImGuiMouseButton mouseButton)
    {
        if (!_isScrollDragging && _anyHighlightLastFrame) return;
        
        var held = ImGui.IsMouseDown(mouseButton);
        if (held)
        {
            _isScrollDragging = true;
        }
        else
        {
            _isScrollDragging = false;
        }

        if (held && delta.Y != 0.0f)
        {
            ImGui.SetScrollY(ImGui.GetScrollY() - delta.Y);
        }
    }

    public void StoreIfAnyItemHovered()
    {
        _anyHighlightLastFrame = ImGui.IsAnyItemActive() || ImGui.IsAnyItemHovered();
    }
}