namespace Hai.HView.Data;

public static class SaveUtil
{
    private const string HViewSaveFolder = "H-View";
    private const string Costumes = "Costumes";

    public static string GetUserDataFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), HViewSaveFolder);
    }
    
    public static string GetCostumesFolder()
    {
        return Path.Combine(GetUserDataFolder(), Costumes);
    }
}