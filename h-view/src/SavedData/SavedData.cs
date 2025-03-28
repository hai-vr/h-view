﻿using System.Numerics;
using System.Text;
using Hai.HView.Ui;
using Newtonsoft.Json;

namespace Hai.HView.Data;

public class HOpenVrHardwarePreference
{
    public int positionX;
    public int positionY;
    public string name = "";
    public bool includeInHapticsMeasurements = true;
}

public class SavedData
{
    private const string MainFilename = "user_config.json";
    private const string BackupFilename = "user_config.backup.json";
    private static string Main => Path.Combine(SaveUtil.GetUserDataFolder(), MainFilename);
    private static string Backup => Path.Combine(SaveUtil.GetUserDataFolder(), BackupFilename);
    
    public string locale = "en";
    public bool useSmallFontDesktop = true;
    public bool useSmallFontVR = false;
    public Dictionary<string, HOpenVrHardwarePreference> ovrSerialToPreference = new();

    public ColorReplacement colorTrackingLost = new() { color = UiColors.V3(UiColors.DEFAULT_TrackingLost) };
    public ColorReplacement colorTrackingRecovered = new() { color = UiColors.V3(UiColors.DEFAULT_TrackingRecovered) };
    public ColorReplacement colorStaleParameter = new() { color = UiColors.V3(UiColors.DEFAULT_StaleParameter) };
    public ColorReplacement colorActiveButton = new() { color = UiColors.V3(UiColors.DEFAULT_ActiveButton) };
    public ColorReplacement colorSecondaryTheme = new() { color = UiColors.V3(UiColors.DEFAULT_SecondaryTheme) };

    public bool modeVrc = true;

    [Serializable]
    public struct ColorReplacement
    {
        public bool use;
        public Vector3 color;
    }
    
    public bool showLighthouses;
    public bool showSerial;
    [JsonIgnore] public bool devTools__EyeTracking;
    [JsonIgnore] public bool devTools__TestTransparency;
    public bool devTools__StereoComposer;
    [JsonIgnore] public float devTools__Scale = 1f;
    [JsonIgnore] public float devTools__MoveAmount = 750f;
    [JsonIgnore] public float devTools__FovTest = 1f;

    public static SavedData OpenConfig()
    {
        return OpenConfig(Main, Backup);
    }

    public static SavedData OpenConfig(string main, string backup)
    {
        if (File.Exists(main))
        {
            try
            {
                var serialized = File.ReadAllText(Main, Encoding.UTF8);
                var result = JsonConvert.DeserializeObject<SavedData>(serialized);
                if (result == null) throw new InvalidDataException();
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while reading main config {main}: {e.Message}");
                if (File.Exists(backup))
                {
                    try
                    {
                        Console.WriteLine($"Trying to read backup... {backup}");
                        var serialized = File.ReadAllText(backup, Encoding.UTF8);
                        var result = JsonConvert.DeserializeObject<SavedData>(serialized);
                        if (result == null) throw new InvalidDataException();
                        return result;
                    }
                    catch (Exception e2)
                    {
                        Console.WriteLine(
                            $"Error while reading backup config {backup}: {e2.Message}, will continue with default config");
                    }
                }
                else
                {
                    Console.WriteLine($"No backup config {backup}, will continue with default config");
                }
            }
        }

        return SavedData.DefaultConfig();
    }

    private static SavedData DefaultConfig()
    {
        return new SavedData();
    }

    public void SaveConfig()
    {
        SaveConfig(Main, Backup);
    }

    public void SaveConfig(string main, string backup)
    {
        new FileInfo(main).Directory?.Create();
        if (File.Exists(main))
        {
            File.Copy(main, backup, true);
        }
        File.WriteAllText(main, JsonConvert.SerializeObject(this));
    }
}