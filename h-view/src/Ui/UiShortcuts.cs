using Hai.ExternalExpressionsMenu;
using Hai.HView.Core;
using static Hai.HView.Gui.UiShortcuts.HVShortcutType;

namespace Hai.HView.Gui;

    /*
TODO:
## Shortcut rework

- Read the current avatar ID through OSC Query for when the application loads.
- When the manifest changes:
    - (DONE) (Make an event listener for the manifest changes) 
    - (DONE) Free the allocated icons and clear the icon cache.
        - (DONE) Switch the icon cache back to indices only.
    - (PARTIAL) Process the manifest to create a new model with the following information:
        - Store whether the parameter associated with a control is local or synced.
        - (DONE) Store the Expression Parameter type of a control, so that we can get and submit the correct value type
          through OSC without having to rely on the Message Box.
        - (DONE) Ignore controls of type Button or Toggle that have both an empty parameter AND a whitespace-only label.
        - (DONE) Partition the controls into 3 groups:
            - Toggles and Buttons.
            - Radials and Axis Puppets.
            - Sub Menus.
- On menu display:
    - (DONE) Show all Toggles and Buttons on the same line, if any exists.
    - (PARTIAL) Show every individual Radials and Axis Puppets on their own lines.
    - (DONE) Show all Sub Menus at the end.
- (DONE) On control display:
    - (DONE) Get the current state of the control through the OSC Message Box.
    - (DONE) Add a way to track press/releasing a non-boolean control (i.e. Toggle sets parameter to value 126).

    */
public class UiShortcuts
{
    private readonly HVInnerWindow _inner;
    private readonly HVRoutine _routine;

    public UiShortcuts(HVInnerWindow inner, HVRoutine routine)
    {
        _inner = inner;
        _routine = routine;
    }

    public HVShortcutHost ShortcutsNullable { get; private set; }

    public void RebuildManifestAsShortcuts(EMManifest manifest)
    {
        ShortcutsNullable = AsHost(manifest.menu, manifest);
    }

    private HVShortcutHost AsHost(EMMenu[] controls, EMManifest manifest)
    {
        var everything = controls
            .Select(menu => AsShortcut(menu, manifest))
            .ToArray();
        var shortcuts = everything
            .Where(shortcut => !IsJustASeparator(shortcut))
            .ToArray();
        return new HVShortcutHost
        {
            pressables = shortcuts.Where(shortcut => shortcut.type is Toggle or Button).ToArray(),
            slidables = shortcuts.Where(shortcut => shortcut.type is not Toggle and not Button and not SubMenu).ToArray(),
            subs = shortcuts.Where(shortcut => shortcut.type is SubMenu).ToArray(),
            shortcuts = shortcuts,
            everything = everything
        };
    }

    private static bool IsJustASeparator(HVShortcut shortcut)
    {
        return shortcut.type is Toggle or Button
               && string.IsNullOrEmpty(shortcut.parameter)
               && string.IsNullOrWhiteSpace(shortcut.label);
    }

    private HVShortcut AsShortcut(EMMenu menu, EMManifest manifest)
    {
        // When the parameter is not an empty string:
        // For non-empty strings, this is not supposed to ever be null on a correctly formed VRCAvatarDescriptor, but we can't trust the client.
        // It could be that this was built in the PreProcess of an avatar that would have been rejected by the VRC build process post checks.
        var expressionParameterNullable = menu.parameter == "" ? null : manifest.expressionParameters.FirstOrDefault(expression => expression.parameter == menu.parameter);
        var referencedParameterType = expressionParameterNullable == null ? HVReferencedParameterType.Unresolved : AsParameterType(expressionParameterNullable.type);
        
        // FIXME: The Expression Parameters of the manifest might contain empty strings, as the default Expression Parameters asset
        // originally used to contain empty fields for the user to fill them in.
        // This may need to be fixed on the External Expressions Menu plugin, while we defensively sanitize the manifest on our side from older versions.
        
        return new HVShortcut
        {
            label = menu.label,
            icon = menu.icon,
            type = AsShortcutType(menu.type),
            parameter = menu.parameter,
            value = menu.value,
            subMenuId = menu.subMenuId,
            isSubMenuRecursive = menu.isSubMenuRecursive,
            axis0 = menu.axis0,
            axis1 = menu.axis1,
            axis2 = menu.axis2,
            axis3 = menu.axis3,
            
            referencedParameterType = referencedParameterType,
            subs = menu.subMenu != null ? AsHost(menu.subMenu, manifest) : null,
        };
    }

    private HVShortcutType AsShortcutType(string menuType)
    {
        // TODO: This can throw an exception.
        return Enum.Parse<HVShortcutType>(menuType);
    }

    private HVReferencedParameterType AsParameterType(string type)
    {
        // TODO: This can throw an exception.
        return Enum.Parse<HVReferencedParameterType>(type);
    }

    public class HVShortcutHost
    {
        public HVShortcut[] pressables;
        public HVShortcut[] slidables;
        public HVShortcut[] subs;
        /// Shortcuts does not include separator items that do nothing.
        public HVShortcut[] shortcuts;
        /// Everything includes separator items that do nothing.
        public HVShortcut[] everything;
    }

    public class HVShortcut
    {
        public string label;
        public int icon;
        public HVShortcutType type;
        public string parameter;
        public float value;
        public int subMenuId;
        public bool isSubMenuRecursive;
        public EMAxis axis0;
        public EMAxis axis1;
        public EMAxis axis2;
        public EMAxis axis3;
        // Derived from Manifest
        public HVReferencedParameterType referencedParameterType;
        public HVShortcutHost subs;
    }

    public enum HVShortcutType
    {
        Toggle,
        Button,
        RadialPuppet,
        TwoAxisPuppet,
        FourAxisPuppet,
        SubMenu
    }

    public enum HVReferencedParameterType
    {
        // Type is unresolved if the parameter name is empty, or somehow doesn't exist in the Expression Parameters.
        // In theory "Somehow doesn't exist" only happens when the avatar being built is invalid, as there are post-process checks
        // to ensure that all Expression Menu parameters are covered by Expression Parameters.
        Unresolved,
        Float,
        Int,
        Bool
    }
}