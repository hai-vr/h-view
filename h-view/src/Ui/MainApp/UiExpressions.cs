using System.Numerics;
using Hai.ExternalExpressionsMenu;
using Hai.HView.Core;
using Hai.HView.OSC;
using Hai.HView.Rendering;
using ImGuiNET;

namespace Hai.HView.Ui.MainApp;

internal class UiExpressions
{
    private readonly ImGuiVRCore ImGuiVR;
    private readonly HVRoutine _routine;
    private readonly HVImageLoader _imageLoader;
    private readonly UiOscQuery _oscQueryTab;
    private readonly UiSharedData _sharedData;
    private readonly Dictionary<int, bool> _buttonPressState = new Dictionary<int, bool>();
    
    private const int NominalImageWidth = 96;
    private const int NominalImageHeight = 96;
    private Vector2 _imageSize;
    private Vector2 _imagelessButtonSize;
    private int _buttonTableWidth;

    public UiExpressions(ImGuiVRCore imGuiVr, HVRoutine routine, HVImageLoader imageLoader, UiOscQuery oscQueryTab, UiSharedData sharedData)
    {
        ImGuiVR = imGuiVr;
        _routine = routine;
        _imageLoader = imageLoader;
        _oscQueryTab = oscQueryTab;
        _sharedData = sharedData;
    }

    private void UpdateIconSize()
    {
        var eyeTrackingSizeMultiplier = 1.5f;
        var width = (int)(NominalImageWidth * (_sharedData.usingEyeTracking ? eyeTrackingSizeMultiplier : 1));
        var height = (int)(NominalImageHeight * (_sharedData.usingEyeTracking ? eyeTrackingSizeMultiplier : 1));
        _imageSize = new Vector2(width, height);
        _imagelessButtonSize = new Vector2(width + 6, height + 6);
        _buttonTableWidth = width + 6;
    }

    public void ExpressionsTab(Dictionary<string, HOscItem> oscMessages)
    {
        UpdateIconSize();
        
        ImGui.BeginTable(HLocalizationPhrase.MenuLabel, 4);
        ImGui.TableSetupColumn(HLocalizationPhrase.MenuLabel, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(HLocalizationPhrase.TypeLabel, ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn(HLocalizationPhrase.ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableHeadersRow();

        var id = 0;
        if (_sharedData.ManifestNullable != null)
        {
            PrintThatMenu(_sharedData.ManifestNullable, _sharedData.ManifestNullable.menu, oscMessages, ref id);
        }
        ImGui.EndTable();
    }

    private void PrintThatMenu(EMManifest manifest, EMMenu[] menu, Dictionary<string, HOscItem> oscMessages, ref int id)
    {
        foreach (var item in menu)
        {
            var interestingParameter = item.type == "RadialPuppet" ? item.axis0.parameter : item.parameter;
            if (interestingParameter == null) interestingParameter = ""; // FIXME: Why does this happen?
            
            var oscParam = OscParameterize(interestingParameter);
            var hasOscItem = oscMessages.TryGetValue(oscParam, out var oscItem);
            var isSubMenu = item.type == "SubMenu";
            var hasParameter = interestingParameter != "";
            
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (item.icon != -1)
            {
                ImGui.Image(_imageLoader.GetOrLoadImage(manifest.icons, item.icon), _imageSize);
                ImGui.SameLine();
            }
            ImGui.Text(item.label);
            
            if (hasParameter && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(interestingParameter);
                ImGui.EndTooltip();
            }

            var key = $"{id}";
            if (hasParameter && ImGui.BeginPopupContextItem($"a popup##{key}"))
            {
                if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{oscParam}\"")) ImGui.SetClipboardText(oscParam);
                if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{interestingParameter}\"")) ImGui.SetClipboardText(interestingParameter);
                if (hasOscItem && oscItem.Values != null)
                {
                    var join = string.Join(",", oscItem.Values.Select(o => o.ToString()));
                    if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{join}\"")) ImGui.SetClipboardText(join);
                }
                ImGui.EndPopup();
            }
            
            ImGui.TableSetColumnIndex(1);
            // FIXME: This should become a dictionary after the menu is read from disk
            if (hasParameter && !isSubMenu)
            {
                var expressionNullable = manifest.expressionParameters.FirstOrDefault(expression => expression.parameter == interestingParameter);
                if (expressionNullable != null)
                {
                    ImGui.Text(expressionNullable.synced ? "" : "local");
                }
                else
                {
                    ImGui.Text("?");
                }
            }
            
            ImGui.TableSetColumnIndex(2);
            ImGui.Text($"{item.type}");
            
            ImGui.TableSetColumnIndex(3);
            if ((item.type == "Button" || item.type == "Toggle") && (!hasOscItem || (hasOscItem && oscItem.OscType == "i")))
            {
                var expected = (int)item.value;
                var b = oscItem.WriteOnlyValueRef is int i && i == expected;
                var doit = b;
                if (doit) ImGui.PushStyleColor(ImGuiCol.Button, UiColors.EnabledButtonTransparentCyan);
                if (ImGuiVR.HapticButton($"= {expected}##{key}.toggle", new Vector2(ImGui.GetContentRegionAvail().X - 50 - 20, 0f)))
                {
                    if (b)
                    {
                        _routine.UpdateMessage(oscItem.Key, 0);
                    }
                    else
                    {
                        _routine.UpdateMessage(oscItem.Key, expected);
                    }
                }
                if (doit) ImGui.PopStyleColor();
                // ImGui.SameLine();
                // if (ImGuiVR.HapticButton($"{HoldLabel}##{key}.hold", new Vector2(50, 0f))) ;
                // _routine.EmitOscFlipEventOnChange(oscItem.Key, ImGui.IsItemActive());

                ImGui.SameLine();
            }
            else if (hasOscItem)
            {
                _oscQueryTab.BuildControls(oscItem, 0, oscItem.Key);
            }

            id++;
            if (item.subMenu != null)
            {
                ImGui.Indent();
                PrintThatMenu(manifest, item.subMenu, oscMessages, ref id);
                ImGui.Unindent();
            }
        }
    }

    private static string OscParameterize(string itemParameter)
    {
        var unsanitizedOscParam = itemParameter;
        var sanitizedOscParam = unsanitizedOscParam.Replace(" ", "_");
        return $"/avatar/parameters/{sanitizedOscParam}";
    }

    public void ContactsTab(Dictionary<string, HOscItem> oscMessages)
    {
        ImGui.BeginTable(HLocalizationPhrase.ContactsLabel, 4);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(HLocalizationPhrase.TypeLabel, ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn(HLocalizationPhrase.ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn(HLocalizationPhrase.ContactsLabel, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();
        
        var id = 0;
        if (_sharedData.ManifestNullable != null)
        {
            PrintContacts(_sharedData.ManifestNullable, oscMessages, ref id);
        }
        ImGui.EndTable();
    }

    private void PrintContacts(EMManifest manifest, Dictionary<string, HOscItem> oscMessages, ref int id)
    {
        foreach (var item in manifest.contactParameters)
        {
            var oscParam = OscParameterize(item.parameter);
            var hasOscItem = oscMessages.TryGetValue(oscParam, out var oscItem);

            ImGui.TableNextRow();
            var i = 0;

            ImGui.TableSetColumnIndex(i++);
            ImGui.Text($"{item.radius}");

            ImGui.TableSetColumnIndex(i++);
            ImGui.Text(item.receiverType);

            ImGui.TableSetColumnIndex(i++);
            if (hasOscItem)
            {
                _oscQueryTab.BuildControls(oscItem, 0, oscItem.Key);
            }
            
            ImGui.TableSetColumnIndex(i++);
            ImGui.Text(item.parameter);

            var key = $"{id}";
            if (ImGui.BeginPopupContextItem($"a popup##{key}"))
            {
                if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{oscParam}\"")) ImGui.SetClipboardText(oscParam);
                if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{item.parameter}\"")) ImGui.SetClipboardText(item.parameter);
                if (hasOscItem && oscItem.Values != null)
                {
                    var join = string.Join(",", oscItem.Values.Select(o => o.ToString()));
                    if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{join}\"")) ImGui.SetClipboardText(join);
                }

                ImGui.EndPopup();
            }

            id++;
        }
    }

    public void PhysBonesTab(Dictionary<string, HOscItem> oscMessages)
    {
        ImGui.BeginTable(HLocalizationPhrase.PhysBonesLabel, 4);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(HLocalizationPhrase.TypeLabel, ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn(HLocalizationPhrase.ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn(HLocalizationPhrase.PhysBonesLabel, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();
        
        var id = 0;
        if (_sharedData.ManifestNullable != null)
        {
            PrintPhysBones(_sharedData.ManifestNullable, oscMessages, ref id);
        }
        ImGui.EndTable();
    }

    private void PrintPhysBones(EMManifest manifest, Dictionary<string, HOscItem> oscMessages, ref int id)
    {
        var options = new[] { "_Stretch", "_Squish", "_Angle", "_IsGrabbed" };
        foreach (var item in manifest.physBoneParameters)
        {
            foreach (var option in options)
            {
                var optionName = item.parameter + option;
                
                var oscParam = OscParameterize(optionName);
                var hasOscItem = oscMessages.TryGetValue(oscParam, out var oscItem);

                ImGui.TableNextRow();
                int i = 0;

                ImGui.TableSetColumnIndex(i++);
                if (option == "_Stretch")
                {
                    ImGui.Text($"{item.maxStretch}");
                }
                else if (option == "_Squish")
                {
                    ImGui.Text($"{item.maxSquish}");
                }
                else if (option == "_Angle")
                {
                    if (item.limitType == "None")
                    {
                        ImGui.Text("");
                    }
                    else if (item.limitType == "Polar")
                    {
                        ImGui.Text($"{item.maxAngleX}deg, {item.maxAngleZ}deg");
                    }
                    else
                    {
                        ImGui.Text($"{item.maxAngleX}deg");
                    }
                }
                else
                {
                    ImGui.Text("");
                }

                ImGui.TableSetColumnIndex(i++);
                if (option == "_Angle")
                {
                    ImGui.Text($"{item.limitType}");
                }
                else
                {
                    ImGui.Text($"{option.Substring(1)}");
                }

                ImGui.TableSetColumnIndex(i++);
                if (hasOscItem)
                {
                    _oscQueryTab.BuildControls(oscItem, 0, oscItem.Key);
                }
                
                ImGui.TableSetColumnIndex(i++);
                ImGui.Text(optionName);

                var key = $"{id}";
                if (ImGui.BeginPopupContextItem($"a popup##{key}"))
                {
                    if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{oscParam}\"")) ImGui.SetClipboardText(oscParam);
                    if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{optionName}\"")) ImGui.SetClipboardText(optionName);
                    if (hasOscItem && oscItem.Values != null)
                    {
                        var join = string.Join(",", oscItem.Values.Select(o => o.ToString()));
                        if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{join}\"")) ImGui.SetClipboardText(join);
                    }

                    ImGui.EndPopup();
                }

                id++;
            }
        }
    }

    public void ShortcutsTab(Dictionary<string, HOscItem> oscMessages)
    {
        UpdateIconSize();
        
        var safeFiles = _routine.UiManifestSafeFiles();
        var names = new[] { " " }.Concat(safeFiles.Select(file => file.ConvenientName)).ToArray();
        var current = 0;
        var changed = ImGui.Combo(HLocalizationPhrase.FileLabel, ref current, names, names.Length);
        if (changed && current != 0)
        {
            var actualIndex = current - 1;
            _routine.ManuallyLoadManifestFromFile(safeFiles[actualIndex].FilePath);
        }
        
        var id = 0;
        if (_sharedData.ShortcutsNullable != null)
        {
            PrintShortcuts(_sharedData.ShortcutsNullable, oscMessages, ref id, _sharedData.ManifestNullable.icons, null);
        }
        ImGui.Text("");
        ImGui.Text("");
    }

    private void PrintShortcuts(HVShortcutHost host, Dictionary<string, HOscItem> oscMessages, ref int id, string[] icons, HVShortcut parentMenuOrNullIfRoot)
    {
        if (parentMenuOrNullIfRoot != null)
        {
            ImGui.SeparatorText(parentMenuOrNullIfRoot.label);
            if (parentMenuOrNullIfRoot.icon != -1)
            {
                ImGui.Image(_imageLoader.GetOrLoadImage(icons, parentMenuOrNullIfRoot.icon), _imageSize);
                ImGui.SameLine();
            }
            else
            {
                // TODO: We might need to pull a default menu icon if none is provided, to hide the weird indent
                ImGui.Dummy(_imageSize);
                ImGui.SameLine();
            }
        }
        else
        {
            ImGui.SeparatorText(HLocalizationPhrase.ExpressionsMenuLabel);
        }

        IterateThrough(oscMessages, ref id, icons, host.pressables, true);
        if (host.pressables.Length > 0 && host.slidables.Length > 0)
        {
            ImGui.Dummy(_imageSize);
            ImGui.SameLine();
        }
        IterateThrough(oscMessages, ref id, icons, host.slidables, false);
        IterateThrough(oscMessages, ref id, icons, host.subs, false);
    }

    private void IterateThrough(Dictionary<string, HOscItem> oscMessages, ref int id, string[] icons, HVShortcut[] orderedMenuItems, bool isPressables)
    {
        if (orderedMenuItems.Length == 0) return;

        if (isPressables)
        {
            ImGui.BeginTable("ignored", orderedMenuItems.Length);
            for (var i = 0; i < orderedMenuItems.Length; i++)
            {
                ImGui.TableSetupColumn($"ignored {i}", ImGuiTableColumnFlags.WidthFixed, _buttonTableWidth);
            }
            ImGui.TableNextRow();
        }
        
        for (var inx = 0; inx < orderedMenuItems.Length; inx++)
        {
            if (isPressables)
            {
                ImGui.TableSetColumnIndex(inx);
            }
            var item = orderedMenuItems[inx];
            var isLastItemOfThatList = inx == orderedMenuItems.Length - 1;
            
            var interestingParameter = item.type == HVShortcutType.RadialPuppet ? item.axis0.parameter : item.parameter;

            var oscParam = OscParameterize(interestingParameter);
            var hasOscItem = oscMessages.TryGetValue(oscParam, out var oscItem);
            var isSubMenu = item.type == HVShortcutType.SubMenu;
            var hasParameter = interestingParameter != "";

            if (!isSubMenu)
            {
                if (item.type is not HVShortcutType.RadialPuppet and not HVShortcutType.TwoAxisPuppet and not HVShortcutType.FourAxisPuppet)
                {
                    ImGui.BeginGroup();

                    var isMatch = hasOscItem && IsControlMatchingOscValue(item, oscItem);
                    if (isMatch) ImGui.PushStyleColor(ImGuiCol.Button, UiColors.EnabledButtonTransparentCyan);

                    var button = DrawButtonFor(id, icons, item);
                    if (hasOscItem && button && item.type == HVShortcutType.Toggle)
                    {
                        _routine.UpdateMessage(oscItem.Key, TransformFloatToType(item.referencedParameterType, !isMatch ? item.value : 0f));
                    }
                    if (item.type == HVShortcutType.Button)
                    {
                        _buttonPressState.TryGetValue(id, out var wasPressed); // The return value does not matter in this scenario
                        var isPressed = ImGui.IsItemActive();
                        if (wasPressed != isPressed)
                        {
                            if (hasOscItem)
                            {
                                _routine.UpdateMessage(oscItem.Key, TransformFloatToType(item.referencedParameterType, isPressed ? item.value : 0f));
                            }
                            _buttonPressState[id] = isPressed;
                        }
                    }

                    if (isMatch) ImGui.PopStyleColor();
                
                    ImGui.TextWrapped($"{item.label}");
                    ImGui.EndGroup();
                }
                else
                {
                    ImGui.BeginTable("ignored", 2);
                    ImGui.TableSetupColumn("ignored", ImGuiTableColumnFlags.WidthFixed, _buttonTableWidth);
                    ImGui.TableSetupColumn("ignored", ImGuiTableColumnFlags.WidthStretch, _buttonTableWidth);
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    
                    ImGui.BeginGroup();
                    var ignored = DrawButtonFor(id, icons, item);
                    ImGui.TextWrapped($"{item.label}");
                    ImGui.EndGroup();

                    ImGui.TableSetColumnIndex(1);
                    if (item.type == HVShortcutType.RadialPuppet)
                    {
                        // FIXME: The control won't show up if the OSC Query module isn't working.
                        // It should always be shown, regardless of the OSC Query availability, because we have all the information needed to display it.
                        _oscQueryTab.BuildControls(oscItem, 0f, $"kk{id}");
                    }
                    else
                    {
                        ImGui.Text("");
                    }
                    
                    ImGui.EndTable();
                }

                if (!isLastItemOfThatList)
                {
                    if (!isPressables)
                    {
                        ImGui.Dummy(_imageSize);
                        ImGui.SameLine();
                    }
                }
            }
            else
            {
                ImGui.Dummy(new Vector2(1, 1));
            }

            var key = $"{id}";
            if (hasParameter && ImGui.BeginPopupContextItem($"a popup##{key}"))
            {
                if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{oscParam}\"")) ImGui.SetClipboardText(oscParam);
                if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{interestingParameter}\"")) ImGui.SetClipboardText(interestingParameter);
                if (hasOscItem && oscItem.Values != null)
                {
                    var join = string.Join(",", oscItem.Values.Select(o => o.ToString()));
                    if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{join}\"")) ImGui.SetClipboardText(join);
                }

                ImGui.EndPopup();
            }

            id++;
            if (item.subs != null)
            {
                ImGui.Indent();
                PrintShortcuts(item.subs, oscMessages, ref id, icons, item);
                ImGui.Unindent();
            }
        }

        if (isPressables)
        {
            ImGui.EndTable();
        }
    }

    private bool DrawButtonFor(int id, string[] icons, HVShortcut item)
    {
        bool button;
        if (item.icon != -1)
        {
            button = ImGuiVR.HapticImageButton($"###{id}", _imageLoader.GetOrLoadImage(icons, item.icon), _imageSize);
        }
        else
        {
            button = ImGuiVR.HapticButton($"?###{id}", _imagelessButtonSize);
        }

        return button;
    }

    private object TransformFloatToType(HVReferencedParameterType referencedType, float itemValue)
    {
        switch (referencedType)
        {
            case HVReferencedParameterType.Unresolved:
                return itemValue;
            case HVReferencedParameterType.Float:
                return itemValue;
            case HVReferencedParameterType.Int:
                return (int)itemValue;
            case HVReferencedParameterType.Bool:
                return itemValue > 0.5f;
            default:
                throw new ArgumentOutOfRangeException(nameof(referencedType), referencedType, null);
        }
    }

    private static bool IsControlMatchingOscValue(HVShortcut item, HOscItem oscItem)
    {
        switch (item.referencedParameterType)
        {
            case HVReferencedParameterType.Unresolved:
                return false;
            case HVReferencedParameterType.Float:
                return oscItem.WriteOnlyValueRef is float f && f == item.value;
            case HVReferencedParameterType.Int:
                return oscItem.WriteOnlyValueRef is int i && i == (int)item.value;
            case HVReferencedParameterType.Bool:
                return oscItem.WriteOnlyValueRef is bool b && b == (item.value > 0.5f);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}