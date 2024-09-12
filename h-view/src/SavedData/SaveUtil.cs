namespace Hai.HView.SavedData;

public static class SaveUtil
{
    private const string HViewSaveFolder = "H-View";
    
    public static string GetUserDataFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), HViewSaveFolder);
    }
}