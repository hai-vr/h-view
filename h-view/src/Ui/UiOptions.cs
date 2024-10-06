using System.Numerics;
using System.Text;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.Gui;
using Hai.HView.HThirdParty;
using ImGuiNET;

namespace Hai.HView.Ui;

public class UiOptions
{
    private readonly HVInnerWindow _inner;
    private readonly HVRoutine _routine;
    private readonly SavedData _config;
    private readonly bool _isWindowlessStyle;
    private readonly UiScrollManager _scrollManager;
    private string[] _thirdPartyLateInit;
    private HThirdPartyRegistry _thirdPartyRegistry;
    private int _selectedIndex = -1;

    public UiOptions(HVInnerWindow inner, HVRoutine routine, SavedData config, bool isWindowlessStyle, UiScrollManager scrollManager)
    {
        _inner = inner;
        _routine = routine;
        _config = config;
        _isWindowlessStyle = isWindowlessStyle;
        _scrollManager = scrollManager;
    }

    public void OptionsTab()
    {
        ImGui.SeparatorText(HLocalizationPhrase.SteamVRLabel);
        var autoLaunch = _routine.IsAutoLaunch();
        if (ImGui.Checkbox(HLocalizationPhrase.StartWithSteamVRLabel, ref autoLaunch))
        {
            _routine.SetAutoLaunch(autoLaunch);
        }
        
        ImGui.Text("");
        ImGui.SeparatorText(HLocalizationPhrase.OtherLabel);

        var needsSave = false;
        if (!_isWindowlessStyle) needsSave |= ImGui.Checkbox(HLocalizationPhrase.UseSmallFontDesktopLabel, ref _config.useSmallFontDesktop);
        else needsSave |= ImGui.Checkbox(HLocalizationPhrase.UseSmallFontVRLabel, ref _config.useSmallFontVR);
        if (needsSave)
        {
            _config.SaveConfig();
        }
        if (_inner.HapticButton(HLocalizationPhrase.ShowThirdPartyAcknowledgementsLabel))
        {
            _selectedIndex = -1;
            _inner.SwitchPanel(HVInnerWindow.HPanel.Thirdparty);
        }
        if (_inner.HapticButton("Open Developer tools"))
        {
            _inner.SwitchPanel(HVInnerWindow.HPanel.DevTools);
        }
        
        ImGui.Text("");
        ImGui.SeparatorText(HVInnerWindow.LanguagesNonTranslated);
        
        var languages = HLocalization.GetLanguages();
        for (var languageIndex = 0; languageIndex < languages.Count; languageIndex++)
        {
            var language = languages[languageIndex];
            if (_inner.HapticButton(language))
            {
                _routine.SetLocale(HLocalization.GetLanguageCodes()[languageIndex]);
                HLocalization.SwitchLanguage(languageIndex);
            }
        }
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
                if (_inner.HapticButton($"{selectedEntry.projectUrl}###projecturl"))
                {
                    UiUtil.OpenProjectUrl(selectedEntry.projectUrl);
                }
                
                ImGui.TextWrapped($"Attributed to: {selectedEntry.attributedTo}");
                ImGui.TextWrapped($"License: {selectedEntry.licenseName}");
                if (_inner.HapticButton($"{selectedEntry.licenseUrl}###licenseurl"))
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
    }

    private void DisplayEntries(HThirdPartyEntry[] entries, bool sameLine)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (_inner.HapticButton($"{entry.projectName}###project_{index}"))
            {
                _selectedIndex = index;
            }

            if (sameLine) ImGui.SameLine();
            ImGui.TextWrapped($"by {entry.attributedTo} ({DisplaySpdxOrFallback(entry)})");
        }
    }

    private static string DisplaySpdxOrFallback(HThirdPartyEntry entry)
    {
        return entry.SPDX != "" ? entry.SPDX : entry.licenseName;
    }
}