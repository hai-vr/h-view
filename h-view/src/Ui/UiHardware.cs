using Hai.HView.Hardware;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.Gui;
using ImGuiNET;
using Valve.VR;

namespace Hai.HView.Ui;

public class UiHardware
{
    private readonly HVInnerWindow _inner;
    private readonly HVRoutine _routine;
    private readonly SavedData _config;

    private const uint DarkGray = 0xFFA0A0A0;
    private const uint VeryDarkGray = 0xFF606060;
    private const uint White = 0xFFFFFFFF;
    private const uint Yellow = 0xFF00FFFF;

    private bool _editNames;

    public UiHardware(HVInnerWindow inner, HVRoutine routine, SavedData config)
    {
        _inner = inner;
        _routine = routine;
        _config = config;
    }

    public void HardwareTab()
    {
        // Tell the Hardware module the Hardware UI is being shown, so it may update the data on the OVR thread.
        _routine.RequireHardware();

        OptionsWindow();
        TrackersWindow();
    }
        
    private void OptionsWindow()
    {
        var changed = false;
        changed |= ImGui.Checkbox(HLocalizationPhrase.ShowLighthousesLabel, ref _config.showLighthouses);
        ImGui.SameLine();
        changed |= ImGui.Checkbox(HLocalizationPhrase.ShowSerialLabel, ref _config.showSerial);
        if (changed)
        {
            _config.SaveConfig();
        }
        ImGui.SameLine();
        ImGui.Checkbox(HLocalizationPhrase.EditNamesLabel, ref _editNames);
    }
    
    private void TrackersWindow()
    {
        var data = _routine.UiHardware();
        var hardwareTrackers = data.Trackers;
        var options = _config;
        var maxValidDeviceIndex = data.MaxIndex;
        
        // var preferredO = PreferredOrder();
        // var preferredOrder = preferredO
        //     .Where(wanted => hardwareTrackers.Any(tracker => tracker.serialNumber == wanted))
        //     .Select(wanted => hardwareTrackers.FirstOrDefault(tracker => tracker.serialNumber == wanted))
        //     .Concat(
        //         hardwareTrackers.Where(tracker => !preferredO.Any(unwanted => tracker.serialNumber == unwanted)))
        //     .ToArray();

        var showSerial = _config.showSerial;
        ImGui.BeginTable("###OpenVR Devices", 4 + (showSerial ? 1 : 0));
        if (showSerial) ImGui.TableSetupColumn(HLocalizationPhrase.SerialLabel, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("LHW-AAAAAAAA").X + 10);
        ImGui.TableSetupColumn(HLocalizationPhrase.NameLabel, ImGuiTableColumnFlags.WidthStretch);
        var batteryWidth = ImGui.CalcTextSize("- 999 % -").X + 50;
        ImGui.TableSetupColumn(HLocalizationPhrase.BatteryLabel, ImGuiTableColumnFlags.WidthFixed, batteryWidth);
        ImGui.TableSetupColumn(HLocalizationPhrase.SensorLabel, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("<<< ---").X + 10);
        ImGui.TableSetupColumn(HLocalizationPhrase.StatusLabel, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Calibrating_OutOfRange").X + 10);
        ImGui.TableHeadersRow();
        var preferredOrder = hardwareTrackers;
        var anyChanged = false;
        var now = DateTime.Now;
        foreach (var hardware in preferredOrder)
        {
            if (hardware.SerialNumber == null) continue;
            if (hardware.DeviceIndex > maxValidDeviceIndex) break;
            if (options.showLighthouses || hardware.DeviceClass != ETrackedDeviceClass.TrackingReference)
            {
                var battery = hardware.BatteryLevel * 100;
                    
                var angle = hardware.AngVel.Length();
                var angleIndicator = angle > 10f ? "<<<" : (angle > 2f ? "<<" : angle > 0.2f ? "<" : "");
                var angForm = string.Format("{0,-3}", angleIndicator);
                    
                var vel = hardware.Vel.Length();
                var velIndicator = vel > 2f ? "---" : (vel > 1f ? "--" : vel > 0.05f ? "-" : "");
                var velForm = string.Format("{0,-3}", velIndicator);
                    
                var batteryForm = string.Format("{0,9}", battery > 0 && hardware.DeviceClass != ETrackedDeviceClass.TrackingReference ? $"{battery:0} %%" : "%%");
                    
                var debugTrackingResult = hardware.DebugTrackingResult;

                var color = hardware.Exists ? hardware.DeviceClass == ETrackedDeviceClass.TrackingReference ? DarkGray : hardware.IsHealthy ? (
                    ComputeHealthColor(now, hardware)
                ) : Yellow : VeryDarkGray;
                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.TableNextRow();
                var tableIndex = 0;
                if (showSerial)
                {
                    ImGui.TableSetColumnIndex(tableIndex++);
                    ImGui.Text(hardware.SerialNumber);
                    if (ImGui.BeginPopupContextItem($"serial_popup##{hardware.DeviceIndex}"))
                    {
                        ImGui.PopStyleColor();
                        if (ImGui.Selectable($"{HLocalizationPhrase.CopySerialNumberLabel} \"{hardware.SerialNumber}\"")) ImGui.SetClipboardText(hardware.SerialNumber);
                        ImGui.PushStyleColor(ImGuiCol.Text, color);
                        ImGui.EndPopup();
                    }
                }
                ImGui.TableSetColumnIndex(tableIndex++);
                var found = options.ovrSerialToPreference.TryGetValue(hardware.SerialNumber, out var preference);
                var name = found ? (preference.name == hardware.SerialNumber && hardware.DeviceClass == ETrackedDeviceClass.Controller ? $"{hardware.ControllerRole}" : preference.name) : "";
                if (_editNames && found)
                {
                    anyChanged = ImGui.InputText($"##edit{hardware.SerialNumber}", ref options.ovrSerialToPreference[hardware.SerialNumber].name, 10_000);
                    ItemHovered(hardware, color);
                    ImGui.SameLine();
                    if (_inner.HapticButton($"{HLocalizationPhrase.OkLabel}##ok{hardware.SerialNumber}"))
                    {
                        _editNames = false;
                    }
                }
                else
                {
                    ImGui.Text(name);
                    ItemHovered(hardware, color);
                }
                if (ImGui.BeginPopupContextItem($"popup##{hardware.DeviceIndex}"))
                {
                    ImGui.PopStyleColor();
                    if (ImGui.Selectable($"{HLocalizationPhrase.CopySerialNumberLabel} \"{hardware.SerialNumber}\"")) ImGui.SetClipboardText(hardware.SerialNumber);
                    if (ImGui.Selectable($"{HLocalizationPhrase.RenameLabel} \"{name}\" ...")) _editNames = true;
                    ImGui.PushStyleColor(ImGuiCol.Text, color);
                    ImGui.EndPopup();
                }
                ImGui.TableSetColumnIndex(tableIndex++);
                ImGui.SetNextItemWidth(batteryWidth);
                if (battery == 0f)
                {
                    ImGui.Text("");
                }
                else
                {
                    ImGui.SliderFloat($"###battery_slider{hardware.DeviceIndex}", ref battery, 0f, 100f, $"{(int)battery} %%", ImGuiSliderFlags.NoInput);
                }
                ImGui.TableSetColumnIndex(tableIndex++);
                ImGui.Text(hardware.DeviceClass == ETrackedDeviceClass.TrackingReference
                    ? $"{hardware.ClosestTrackerDistance:0.0}m"
                    : $"{angForm}{velForm}");
                if (hardware.DeviceClass == ETrackedDeviceClass.TrackingReference && ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted($"{HLocalizationPhrase.DistanceToClosestDeviceLabel}: {hardware.ClosestTrackerDistance:0.0}m");
                    ImGui.EndTooltip();
                }
                ImGui.TableSetColumnIndex(tableIndex++);
                ImGui.Text($"{(hardware.Exists ? debugTrackingResult == 0 ? "" : debugTrackingResult : $"<{HLocalizationPhrase.NotConnectedLabel}>")}");
                ImGui.PopStyleColor();
            }
        }
        if (anyChanged)
        {
            options.SaveConfig();
        }
        ImGui.EndTable();
    }

    private uint ComputeHealthColor(DateTime now, HardwareTracker hardware)
    {
        float mercyMs = 1000f;
        
        float healthiness01 = (float)((now - hardware.LastIssueTime).TotalMilliseconds / mercyMs);
        if (healthiness01 > 1f) return White;
        if (healthiness01 < 0f) return Yellow; // Defensive

        // FIXME: Really need a color lerping solution here
        return (uint)(0xFF00FF00 | (int)(healthiness01 * 0xFF) | (int)(healthiness01 * 0xFF) << 16);
    }

    private static void ItemHovered(HardwareTracker hardware, uint color)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.PopStyleColor();
            ImGui.BeginTooltip();
            ImGui.TextUnformatted($"{HLocalizationPhrase.DeviceClassLabel}: {hardware.DeviceClass}");
            if (hardware.DeviceClass == ETrackedDeviceClass.Controller)
            {
                ImGui.TextUnformatted($"{HLocalizationPhrase.RoleLabel}: {hardware.ControllerRole}");
            }

            ImGui.TextUnformatted($"{HLocalizationPhrase.SerialLabel}: {hardware.SerialNumber}");
            ImGui.TextUnformatted($"{HLocalizationPhrase.ManufacturerLabel}: {hardware.Manufacturer}");
            ImGui.TextUnformatted($"{HLocalizationPhrase.ModelNumberLabel}: {hardware.ModelNumber}");
            ImGui.TextUnformatted($"{HLocalizationPhrase.BatteryLabel}: {hardware.BatteryLevel * 100:0} %");
            ImGui.TextUnformatted($"ID: {hardware.DeviceIndex}");
            ImGui.EndTooltip();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
        }
    }
}