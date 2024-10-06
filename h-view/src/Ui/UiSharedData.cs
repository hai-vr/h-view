using Hai.ExternalExpressionsMenu;

namespace Hai.HView.Gui;

public class UiSharedData
{
    public UiShortcuts.HVShortcutHost ShortcutsNullable { get; set; }
    public EMManifest ManifestNullable { get; set; }
    public Dictionary<string, bool> isLocal = new Dictionary<string, bool>();
}