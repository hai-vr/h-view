using System.Numerics;
using Hai.ExternalExpressionsMenu;
using Hai.HView.OSC;
using ImGuiNET;
using Veldrid;
using Veldrid.ImageSharp;

namespace Hai.HView.Gui;

public partial class HVInnerWindow
{
    private readonly Dictionary<int, IntPtr> _indexToPointers = new Dictionary<int, IntPtr>();
    private readonly List<Texture> _loadedTextures = new List<Texture>();
    private readonly Dictionary<int, bool> _buttonPressState = new Dictionary<int, bool>();
    
    private readonly Dictionary<string, IntPtr> _pathToPointers = new Dictionary<string, IntPtr>();
    
    private const int ImageWidth = 64;
    private const int ImageHeight = 64;
    private readonly Vector2 _imageSize = new Vector2(ImageWidth, ImageHeight);
    private readonly Vector2 _imagelessButtonSize = new Vector2(ImageWidth + 6, ImageHeight + 6);
    private const int ButtonTableWidth = ImageWidth + 6;

    /// Free allocated images. This needs to be called from the UI thread.
    private void FreeImagesFromMemory()
    {
        // TODO: This may still leak within the custom ImGui controller.
        Console.WriteLine("Freeing images from memory");
        foreach (var loadedTexture in _loadedTextures)
        {
            loadedTexture.Dispose();
        }
        _loadedTextures.Clear();
        _indexToPointers.Clear();
        // TODO: Don't free avatar pictures that were loaded from disk.
        _pathToPointers.Clear();
    }

    private IntPtr GetOrLoadImage(string[] icons, int index)
    {
        // TODO: Should we pre-load all the icons immediately, instead of doing it on request?
        if (_indexToPointers.TryGetValue(index, out var found)) return found;
        
        if (index == -1) return 0;
        if (index >= icons.Length) return 0;
        var base64png = icons[index];
        
        var pngBytes = Convert.FromBase64String(base64png);
        using (var stream = new MemoryStream(pngBytes))
        {
            var pointer = LoadTextureFromStream(stream);
            _indexToPointers.Add(index, pointer);
            return pointer;
        }
    }

    internal IntPtr GetOrLoadImage(string path)
    {
        if (_pathToPointers.TryGetValue(path, out var found)) return found;

        using (var stream = new FileStream(path, FileMode.Open))
        {
            var pointer = LoadTextureFromStream(stream);
            _pathToPointers.Add(path, pointer);
            return pointer;
        }
    }

    private IntPtr LoadTextureFromStream(Stream stream)
    {
        // https://github.com/ImGuiNET/ImGui.NET/issues/141#issuecomment-905927496
        var img = new ImageSharpTexture(stream, true);
        var deviceTexture = img.CreateDeviceTexture(_gd, _gd.ResourceFactory);
        _loadedTextures.Add(deviceTexture);
        var pointer = _controller.GetOrCreateImGuiBinding(_gd.ResourceFactory, deviceTexture);
        return pointer;
    }

    private void ExpressionsTab(Dictionary<string, HOscItem> oscMessages)
    {
        ImGui.BeginTable("Menu", 4);
        ImGui.TableSetupColumn("Menu", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(TypeLabel, ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn(ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableHeadersRow();

        var id = 0;
        if (_manifestNullable != null)
        {
            PrintThatMenu(_manifestNullable, _manifestNullable.menu, oscMessages, ref id);
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
        ImGui.BeginTable("Contacts", 4);
        ImGui.TableSetupColumn("Contacts", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(TypeLabel, ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn(ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableHeadersRow();
        
        var id = 0;
        if (_manifestNullable != null)
        {
            PrintContacts(_manifestNullable, oscMessages, ref id);
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
        ImGui.BeginTable("PhysBones", 4);
        ImGui.TableSetupColumn("PhysBones", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(TypeLabel, ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn(ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableHeadersRow();
        
        var id = 0;
        if (_manifestNullable != null)
        {
            PrintPhysBones(_manifestNullable, oscMessages, ref id);
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
        var safeFilePaths = _routine.UiManifestSafeFilePaths();
        var names = new[] { " " }.Concat(safeFilePaths.Select(Path.GetFileName)).ToArray();
        var current = 0;
        var changed = ImGui.Combo("File", ref current, names, names.Length);
        if (changed && current != 0)
        {
            var actualIndex = current - 1;
            _routine.ManuallyLoadManifestFromFile(safeFilePaths[actualIndex]);
        }
        
        var id = 0;
        if (_shortcutsNullable != null)
        {
            PrintShortcuts(_shortcutsNullable, oscMessages, ref id, _manifestNullable.icons, null);
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
                ImGui.Image(GetOrLoadImage(icons, parentMenuOrNullIfRoot.icon), _imageSize);
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
            ImGui.SeparatorText("Expressions Menu");
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
                ImGui.TableSetupColumn($"ignored {i}", ImGuiTableColumnFlags.WidthFixed, ButtonTableWidth);
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
                    if (isMatch) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 1, 1, 0.75f));

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
                    ImGui.TableSetupColumn("ignored", ImGuiTableColumnFlags.WidthFixed, ButtonTableWidth);
                    ImGui.TableSetupColumn("ignored", ImGuiTableColumnFlags.WidthStretch, ButtonTableWidth);
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
                        BuildControls(oscItem, 0f, $"kk{id}");
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
            button = ImGui.ImageButton($"###{id}", GetOrLoadImage(icons, item.icon), _imageSize);
        }
        else
        {
            button = ImGui.Button($"?###{id}", _imagelessButtonSize);
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