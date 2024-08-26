using System.Numerics;
using Hai.ExternalExpressionsMenu;
using Hai.HView.OSC;
using ImGuiNET;
using Veldrid.ImageSharp;

namespace Hai.HView.Gui;

public partial class HVInnerWindow
{
    private readonly Dictionary<string, IntPtr> _imageToPointers = new Dictionary<string, IntPtr>();
    private Vector2 _imageSize = new Vector2(64, 64);

    private IntPtr GetOrLoadImage(string base64png)
    {
        if (string.IsNullOrEmpty(base64png)) return 0;
        if (_imageToPointers.TryGetValue(base64png, out var found)) return found;
        
        var pngBytes = Convert.FromBase64String(base64png);
        using (var stream = new MemoryStream(pngBytes))
        {
            // https://github.com/ImGuiNET/ImGui.NET/issues/141#issuecomment-905927496
            var img = new ImageSharpTexture(stream);
            var deviceTexture = img.CreateDeviceTexture(_gd, _gd.ResourceFactory);
            var pointer = _controller.GetOrCreateImGuiBinding(_gd.ResourceFactory, deviceTexture);
            _imageToPointers.Add(base64png, pointer);
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
            if (item.icon != "")
            {
                ImGui.Image(GetOrLoadImage(item.icon), _imageSize);
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
            if (hasOscItem)
            {
                BuildControls(oscItem, 0);
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
                BuildControls(oscItem, 0);
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
                    BuildControls(oscItem, 0);
                }

                id++;
            }
        }
    }
}