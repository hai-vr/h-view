using System.Numerics;
using System.Text;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.Gui;
using Hai.HView.HThirdParty;
using ImGuiNET;

namespace Hai.HView.Ui.MainApp;

internal class UiOptions
{
    internal const string LanguagesNonTranslated = "Languages";

    private readonly Action<UiMainApplication.HPanel> _switchPanelCallback;
    private readonly ImGuiVRCore ImGuiVR;
    private readonly HVRoutine _routine;
    private readonly SavedData _config;
    private readonly bool _isWindowlessStyle;
    private readonly UiScrollManager _scrollManager;
    private string[] _thirdPartyLateInit;
    private HThirdPartyRegistry _thirdPartyRegistry;
    private int _selectedIndex = -1;

    public UiOptions(ImGuiVRCore imGuiVr, Action<UiMainApplication.HPanel> switchPanelCallback, HVRoutine routine, SavedData config, bool isWindowlessStyle, UiScrollManager scrollManager)
    {
        _switchPanelCallback = switchPanelCallback;
        ImGuiVR = imGuiVr;
        _routine = routine;
        _config = config;
        _isWindowlessStyle = isWindowlessStyle;
        _scrollManager = scrollManager;
    }

    public void OptionsTab()
    {
        ImGui.SeparatorText(HLocalizationPhrase.SteamVRLabel);
        var autoLaunch = _routine.IsAutoLaunch();
        ImGui.BeginDisabled(!_routine.IsOpenVrAvailable());
        if (ImGui.Checkbox(HLocalizationPhrase.StartWithSteamVRLabel, ref autoLaunch))
        {
            _routine.SetAutoLaunch(autoLaunch);
        }
        ImGui.EndDisabled();

        var needsSave = false;        
        ImGui.SeparatorText(HLocalizationPhrase.ApplicationsLabel);
        needsSave |= ImGui.Checkbox(HLocalizationPhrase.EnableVrcFunctionsLabel, ref _config.modeVrc);
        
        ImGui.SeparatorText(HLocalizationPhrase.OtherLabel);

        if (!_isWindowlessStyle) needsSave |= ImGui.Checkbox(HLocalizationPhrase.UseSmallFontDesktopLabel, ref _config.useSmallFontDesktop);
        else needsSave |= ImGui.Checkbox(HLocalizationPhrase.UseSmallFontVRLabel, ref _config.useSmallFontVR);

        needsSave |= ColorReplacementEdit(HLocalizationPhrase.TrackingLostColorLabel, ref _config.colorTrackingLost, UiColors.DEFAULT_TrackingLost);
        needsSave |= ColorReplacementEdit(HLocalizationPhrase.TrackingRecoveredColorLabel, ref _config.colorTrackingRecovered, UiColors.DEFAULT_TrackingRecovered);
        needsSave |= ColorReplacementEdit(HLocalizationPhrase.StaleParameterColorLabel, ref _config.colorStaleParameter, UiColors.DEFAULT_StaleParameter);
        needsSave |= ColorReplacementEdit(HLocalizationPhrase.ActiveButtonColorLabel, ref _config.colorActiveButton, UiColors.DEFAULT_ActiveButton);
        if (_config.colorActiveButton.use)
        {
            needsSave |= ColorReplacementEdit("", ref _config.colorSecondaryTheme, UiColors.DEFAULT_SecondaryTheme);
        }

        if (needsSave)
        {
            _config.SaveConfig();
        }
        if (ImGuiVR.HapticButton(HLocalizationPhrase.ShowThirdPartyAcknowledgementsLabel))
        {
            _selectedIndex = -1;
            _switchPanelCallback.Invoke(UiMainApplication.HPanel.Thirdparty);
        }
        if (ImGuiVR.HapticButton(HLocalizationPhrase.OpenDeveloperToolsLabel))
        {
            _switchPanelCallback.Invoke(UiMainApplication.HPanel.DevTools);
        }
        
        ImGui.Text("");
        ImGui.SeparatorText(LanguagesNonTranslated);
        
        var languages = HLocalization.GetLanguages();
        for (var languageIndex = 0; languageIndex < languages.Count; languageIndex++)
        {
            var language = languages[languageIndex];
            if (ImGuiVR.HapticButton(language))
            {
                _routine.SetLocale(HLocalization.GetLanguageCodes()[languageIndex]);
                HLocalization.SwitchLanguage(languageIndex);
            }
        }
    }

    private bool ColorReplacementEdit(string label, ref SavedData.ColorReplacement replacement, Vector4 defaultValue)
    {
        var anyChanged = false;
        anyChanged |= ImGui.Checkbox($"{HLocalizationPhrase.ReplaceColorLabel}###replace_checkbox_{label}", ref replacement.use);
        ImGui.SameLine();
        if (replacement.use)
        {
            anyChanged |= ImGui.ColorEdit3($"###color_edit_{label}", ref replacement.color, ImGuiColorEditFlags.NoInputs);
        }
        else
        {
            ImGui.ColorButton($"###color_button{label}", defaultValue);
        }
        ImGui.SameLine();
        UiColors.Colored(!replacement.use, ImGuiCol.Text, UiColors.IsDefaultGray, () => ImGui.Text(label));

        return anyChanged;
    }

    public void ThirdPartyTab()
    {
        _thirdPartyLateInit ??= File.ReadAllText(HAssets.ThirdParty.Absolute(), Encoding.UTF8).Split("- ");
        _thirdPartyRegistry ??= new HThirdPartyRegistry(File.ReadAllText(HAssets.ThirdPartyLookup.Absolute(), Encoding.UTF8));

        ImGui.TextWrapped(HLocalizationPhrase.MsgCreditsHViewInfo);
        ImGui.TextWrapped(HLocalizationPhrase.MsgCreditsHViewMore);
        
        ImGui.Text("");
        ImGui.SeparatorText(HLocalizationPhrase.CreditsThirdPartyAcknowledgementsLabel);
        _scrollManager.MakeScroll(() =>
        {
            ImGui.TextWrapped(HLocalizationPhrase.MsgCreditsFindNearExecutableFile);

            var entries = _thirdPartyRegistry.GetEntries();

            if (_selectedIndex != -1)
            {
                var availY = ImGui.GetContentRegionAvail().Y;
                ImGui.BeginChild("thirdpartychildwindows", new Vector2(ImGui.GetContentRegionAvail().X / 2 - 20, availY));
                DisplayEntries(entries, false);
                ImGui.EndChild();
                
                ImGui.SameLine();
                
                ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 5.0f);
                ImGui.BeginChild("licensewindow", new Vector2(ImGui.GetContentRegionAvail().X, availY), ImGuiChildFlags.Border);
                
                var selectedEntry = entries[_selectedIndex];
                ImGui.SeparatorText(selectedEntry.projectName);
                if (ImGuiVR.HapticButton($"{selectedEntry.projectUrl}###projecturl"))
                {
                    UiUtil.OpenProjectUrl(selectedEntry.projectUrl);
                }
                
                ImGui.TextWrapped($"Attributed to: {selectedEntry.attributedTo}");
                ImGui.TextWrapped($"License: {selectedEntry.licenseName}");
                if (ImGuiVR.HapticButton($"{selectedEntry.licenseUrl}###licenseurl"))
                {
                    UiUtil.OpenProjectUrl(selectedEntry.licenseUrl);
                }
                ImGui.TextWrapped($"Usage: {string.Join(", ", selectedEntry.kind)}");
                
                ImGui.Text("");
                if (_thirdPartyRegistry.TryGetFullLicenseText(selectedEntry.fullLicenseTextFile, out var contents))
                {
                    ImGui.SeparatorText("License text");
                    ImGui.TextWrapped(contents);
                }
                ImGui.EndChild();
                ImGui.PopStyleVar();
            }
            else
            {
                DisplayEntries(entries, true);
            }
        });
    }

    public void DevToolsTab()
    {
        ImGui.SeparatorText("DevTools");
        ImGui.Checkbox("[DEV] Use Eye Tracking instead of controllers as input", ref _config.devTools__EyeTracking);
        ImGui.Checkbox("[DEV] Test transparency", ref _config.devTools__TestTransparency);
        if (ImGuiVR.HapticButton("[DEV] Open Processing tab"))
        {
            _switchPanelCallback.Invoke(UiMainApplication.HPanel.Processing);
        }
    }

    private void DisplayEntries(HThirdPartyEntry[] entries, bool aerated)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (ImGuiVR.HapticButton($"{entry.projectName}###project_{index}"))
            {
                _selectedIndex = index;
            }

            if (aerated) ImGui.SameLine();
            ImGui.TextWrapped($"by {entry.attributedTo} ({DisplaySpdxOrFallback(entry)})");
            if (!aerated) ImGui.Separator();
        }
    }

    private static string DisplaySpdxOrFallback(HThirdPartyEntry entry)
    {
        return entry.SPDX != "" ? entry.SPDX : entry.licenseName;
    }
}