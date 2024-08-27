namespace Hai.HView.Gui;

public partial class HVInnerWindow
{
    /*
TODO:
## Shortcut rework

- Read the current avatar ID through OSC Query for when the application loads.
- When the manifest changes (make an event listener).
  - Free the allocated icons and clear the icon cache.
    - Switch the icon cache back to indices only.
  - Process the manifest to create a new model with the following information:
    - Store whether the parameter associated with a control is local or synced.
    - Store the Expression Parameter type of a control, so that we can get and submit the correct value type
      through OSC without having to rely on the Message Box.
    - Ignore controls of type Button or Toggle that have both an empty parameter AND a whitespace-only label.
    - Partition the controls into 3 groups:
      - Toggles and Buttons.
      - Radials and Axis Puppets.
      - Sub Menus.
- On menu display:
  - Show all Toggles and Buttons on the same line, if any exists.
  - Show every individual Radials and Axis Puppets on their own lines.
  - Show all Sub Menus at the end.
- On control display:
  - Get the current state of the control through the OSC Message Box.
  - Add a way to track press/releasing a non-boolean control (i.e. Toggle sets parameter to value 126).

    */
}