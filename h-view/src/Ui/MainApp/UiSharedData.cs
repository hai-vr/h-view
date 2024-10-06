using Hai.ExternalExpressionsMenu;

namespace Hai.HView.Ui.MainApp;

internal class UiSharedData
{
    public HVShortcutHost ShortcutsNullable { get; set; }
    public EMManifest ManifestNullable { get; set; }
    public Dictionary<string, bool> isLocal = new Dictionary<string, bool>();
    public bool usingEyeTracking;
}