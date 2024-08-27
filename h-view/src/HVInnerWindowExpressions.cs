using System.Numerics;
using Hai.ExternalExpressionsMenu;
using Hai.HView.OSC;
using ImGuiNET;
using Veldrid.ImageSharp;

namespace Hai.HView.Gui;

public partial class HVInnerWindow
{
    private void HandleScrollOnDrag(Vector2 delta, ImGuiMouseButton mouseButton)
    {
        var held = ImGui.IsMouseDown(mouseButton);

        if (held && delta.Y != 0.0f)
        {
            ImGui.SetScrollY(ImGui.GetScrollY() - delta.Y);
        }
    }
    
    // FIXME: We can't store by index, because this cache is not cleared when another avatar manifest gets loaded.
    private readonly Dictionary<string, IntPtr> _indexToPointers = new Dictionary<string, IntPtr>();
    private Vector2 _imageSize = new Vector2(64.02f, 64.02f);
    private readonly Dictionary<int, bool> _clicks = new Dictionary<int, bool>();

    private IntPtr GetOrLoadImage(string[] icons, int index)
    {
        if (index == -1) return 0;
        if (index >= icons.Length) return 0;
        var base64png = icons[index];
        
        // TODO: Should we pre-load all the icons immediately, instead of doing it on request?
        if (_indexToPointers.TryGetValue(base64png, out var found)) return found;
        
        var pngBytes = Convert.FromBase64String(base64png);
        using (var stream = new MemoryStream(pngBytes))
        {
            // https://github.com/ImGuiNET/ImGui.NET/issues/141#issuecomment-905927496
            var img = new ImageSharpTexture(stream, true);
            var deviceTexture = img.CreateDeviceTexture(_gd, _gd.ResourceFactory);
            var pointer = _controller.GetOrCreateImGuiBinding(_gd.ResourceFactory, deviceTexture);
            _indexToPointers.Add(base64png, pointer);
            return pointer;
        }
    }
    
    private void ExpressionsTab(Dictionary<string, HOscItem> oscMessages)
    {
        ImGui.BeginTable("/avatar/descriptor/menu/", 4);
        ImGui.TableSetupColumn("/avatar/descriptor/menu/", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(TypeLabel, ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn(ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableHeadersRow();

        var id = 0;
        var manifest = _routine.ExpressionsManifest;
        if (manifest != null)
        {
            PrintThatMenu(manifest, manifest.menu, oscMessages, ref id);
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
                ImGui.Image(GetOrLoadImage(manifest.icons, item.icon), _imageSize);
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
                if (ImGui.Selectable($"{CopyLabel} \"{oscParam}\"")) ImGui.SetClipboardText(oscParam);
                if (ImGui.Selectable($"{CopyLabel} \"{interestingParameter}\"")) ImGui.SetClipboardText(interestingParameter);
                if (hasOscItem && oscItem.Values != null)
                {
                    var join = string.Join(",", oscItem.Values.Select(o => o.ToString()));
                    if (ImGui.Selectable($"{CopyLabel} \"{join}\"")) ImGui.SetClipboardText(join);
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
                if (doit) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 1, 1, 0.75f));
                if (ImGui.Button($"= {expected}##{key}.toggle", new Vector2(ImGui.GetContentRegionAvail().X - 50 - 20, 0f)))
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
                // if (ImGui.Button($"{HoldLabel}##{key}.hold", new Vector2(50, 0f))) ;
                // _routine.EmitOscFlipEventOnChange(oscItem.Key, ImGui.IsItemActive());

                ImGui.SameLine();
            }
            else if (hasOscItem)
            {
                BuildControls(oscItem, 0, oscItem.Key);
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

    private void ContactsTab(Dictionary<string, HOscItem> oscMessages)
    {
        ImGui.BeginTable("/avatar/descriptor/contacts/", 4);
        ImGui.TableSetupColumn("/avatar/descriptor/contacts/", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(TypeLabel, ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn(ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableHeadersRow();
        
        var id = 0;
        var manifest = _routine.ExpressionsManifest;
        if (manifest != null)
        {
            PrintContacts(manifest, oscMessages, ref id);
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
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(item.parameter);

            var key = $"{id}";
            if (ImGui.BeginPopupContextItem($"a popup##{key}"))
            {
                if (ImGui.Selectable($"{CopyLabel} \"{oscParam}\"")) ImGui.SetClipboardText(oscParam);
                if (ImGui.Selectable($"{CopyLabel} \"{item.parameter}\"")) ImGui.SetClipboardText(item.parameter);
                if (hasOscItem && oscItem.Values != null)
                {
                    var join = string.Join(",", oscItem.Values.Select(o => o.ToString()));
                    if (ImGui.Selectable($"{CopyLabel} \"{join}\"")) ImGui.SetClipboardText(join);
                }

                ImGui.EndPopup();
            }

            ImGui.TableSetColumnIndex(1);
            ImGui.Text($"{item.radius}");

            ImGui.TableSetColumnIndex(2);
            ImGui.Text(item.receiverType);

            ImGui.TableSetColumnIndex(3);
            if (hasOscItem)
            {
                BuildControls(oscItem, 0, oscItem.Key);
            }

            id++;
        }
    }

    private void PhysBonesTab(Dictionary<string, HOscItem> oscMessages)
    {
        ImGui.BeginTable("/avatar/descriptor/physbones/", 4);
        ImGui.TableSetupColumn("/avatar/descriptor/physbones/", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(TypeLabel, ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn(ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableHeadersRow();
        
        var id = 0;
        var manifest = _routine.ExpressionsManifest;
        if (manifest != null)
        {
            PrintPhysBones(manifest, oscMessages, ref id);
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
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(optionName);

                var key = $"{id}";
                if (ImGui.BeginPopupContextItem($"a popup##{key}"))
                {
                    if (ImGui.Selectable($"{CopyLabel} \"{oscParam}\"")) ImGui.SetClipboardText(oscParam);
                    if (ImGui.Selectable($"{CopyLabel} \"{optionName}\"")) ImGui.SetClipboardText(optionName);
                    if (hasOscItem && oscItem.Values != null)
                    {
                        var join = string.Join(",", oscItem.Values.Select(o => o.ToString()));
                        if (ImGui.Selectable($"{CopyLabel} \"{join}\"")) ImGui.SetClipboardText(join);
                    }

                    ImGui.EndPopup();
                }

                ImGui.TableSetColumnIndex(1);
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

                ImGui.TableSetColumnIndex(2);
                if (option == "_Angle")
                {
                    ImGui.Text($"{item.limitType}");
                }
                else
                {
                    ImGui.Text($"{option.Substring(1)}");
                }

                ImGui.TableSetColumnIndex(3);
                if (hasOscItem)
                {
                    BuildControls(oscItem, 0, oscItem.Key);
                }

                id++;
            }
        }
    }

    private void ShortcutsTab(Dictionary<string, HOscItem> oscMessages)
    {
        var id = 0;
        var manifest = _routine.ExpressionsManifest;
        if (manifest != null)
        {
            PrintShortcuts(manifest.menu, oscMessages, ref id, manifest.icons, null);
        }
        ImGui.Text("");
        ImGui.Text("");
    }

    private void PrintShortcuts(EMMenu[] menu, Dictionary<string, HOscItem> oscMessages, ref int id, string[] icons, EMMenu subMenuNullable)
    {
        if (subMenuNullable != null)
        {
            ImGui.SeparatorText(subMenuNullable.label);
            if (subMenuNullable.icon != -1)
            {
                ImGui.Image(GetOrLoadImage(icons, subMenuNullable.icon), _imageSize);
                ImGui.SameLine();
            }
            else
            {
                ImGui.Dummy(new Vector2(1, 1));
            }
        }
        else
        {
            ImGui.SeparatorText("Expressions Menu");
        }

        // TODO: This wastes an array each loop, we really need to pre-process the EMMenu first during acquisition
        // FIXME: This should be partitioning instead of sorting
        var orderedMenuItems = menu
            .Where(item => !(string.IsNullOrWhiteSpace(item.label) && (item.type == "Toggle" || item.type == "Button") && string.IsNullOrEmpty(item.parameter)))
            .OrderByDescending(item => item.type == "Button" || item.type == "Toggle")
            .ThenBy(item => item.type == "SubMenu")
            .ToArray();

        // TODO: We should sort the menu items/do two passes on the menu so that all sub-menus are displayed first
        // TODO: We should show all buttons/toggles before showing sliders (as sliders are on their own line)
        for (var inx = 0; inx < orderedMenuItems.Length; inx++)
        {
            var item = orderedMenuItems[inx];
            
            var interestingParameter = item.type == "RadialPuppet" ? item.axis0.parameter : item.parameter;
            if (interestingParameter == null) interestingParameter = ""; // FIXME: Why does this happen?

            var oscParam = OscParameterize(interestingParameter);
            var hasOscItem = oscMessages.TryGetValue(oscParam, out var oscItem);
            var isSubMenu = item.type == "SubMenu";
            var hasParameter = interestingParameter != "";

            if (item.icon != -1)
            {
                if (!isSubMenu)
                {
                    ImGui.BeginGroup();
                    
                    // FIXME: This works worse than the "Menu" tab.
                    var expected = (int)item.value;
                    var b = oscItem.WriteOnlyValueRef is int i && i == expected;
                    var doit = b;
                    if (doit) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 1, 1, 0.75f));
                    if (ImGui.ImageButton($"###{id}", GetOrLoadImage(icons, item.icon), _imageSize) && item.type == "Toggle")
                    {
                        if (!doit)
                        {
                            _routine.UpdateMessage(oscItem.Key, oscItem.OscType == "i" ? (int)item.value : item.value > 0.5f);
                        }
                        else
                        {
                            _routine.UpdateMessage(oscItem.Key, oscItem.OscType == "i" ? 0 : false);
                        }
                    }
                    if (item.type == "Button")
                    {
                        _clicks.TryGetValue(id, out var isPressed); // The return value does not matter in this scenario
                        var isActive = ImGui.IsItemActive();
                        if (isPressed != isActive)
                        {
                            if (isActive)
                            {
                                _routine.UpdateMessage(oscItem.Key, oscItem.OscType == "i" ? (int)item.value : item.value > 0.5f);
                            }
                            else
                            {
                                _routine.UpdateMessage(oscItem.Key, oscItem.OscType == "i" ? 0 : false);
                            }
                            _clicks[id] = isActive;
                        }
                    }
                    else
                    {
                        
                    }
                    if (doit) ImGui.PopStyleColor();
                    
                    // FIXME: Can't find a way to limit the text width
                    // ImGui.PushItemWidth(64);
                    ImGui.TextWrapped($"{item.label}");
                    // ImGui.PopItemWidth();
                    ImGui.EndGroup();
                    
                    // FIXME: This "SameLine" situation is a disaster.
                    if (item.type == "RadialPuppet")
                    {
                        ImGui.SameLine();
                        // ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 100);
                        BuildControls(oscItem, 0f, $"kk{id}");
                        // ImGui.PopStyleVar();
                        
                        if (inx != orderedMenuItems.Length - 1 && orderedMenuItems[inx + 1].type != "SubMenu")
                        {
                            ImGui.Dummy(_imageSize);
                            ImGui.SameLine();
                        }
                    }
                    else if (inx != orderedMenuItems.Length - 1)
                    {
                        var nextType = orderedMenuItems[inx + 1].type;
                        if (nextType != "RadialPuppet" && nextType != "SubMenu")
                        {
                            ImGui.SameLine();
                        }
                        else
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
            }
            else
            {
                // FIXME: If there's no icon we still need a button
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(item.label);
                ImGui.EndTooltip();
            }

            var key = $"{id}";
            if (hasParameter && ImGui.BeginPopupContextItem($"a popup##{key}"))
            {
                if (ImGui.Selectable($"{CopyLabel} \"{oscParam}\"")) ImGui.SetClipboardText(oscParam);
                if (ImGui.Selectable($"{CopyLabel} \"{interestingParameter}\"")) ImGui.SetClipboardText(interestingParameter);
                if (hasOscItem && oscItem.Values != null)
                {
                    var join = string.Join(",", oscItem.Values.Select(o => o.ToString()));
                    if (ImGui.Selectable($"{CopyLabel} \"{join}\"")) ImGui.SetClipboardText(join);
                }

                ImGui.EndPopup();
            }

            id++;
            if (item.subMenu != null)
            {
                ImGui.Indent();
                PrintShortcuts(item.subMenu, oscMessages, ref id, icons, item);
                ImGui.Unindent();
            }
        }
    }
}