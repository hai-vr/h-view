H-View
===

Hello,

This repository is mostly a personal learning project and has various functions:
- It is my personal OSC Query debugger, and communicates with VRChat by listing all the available addresses.
- It can display the entire Expressions Menu if that menu was exported during the avatar build process using [another Unity Editor tool](https://github.com/hai-vr/external-expressions-menu/)
  I made specifically for this purpose ([learn more](https://docs.hai-vr.dev/docs/products/h-view)).
- It can display the status of all VR hardware.
- In addition to the desktop window, it also has an implementation of the ImGui.NET window:
  - being rendered into a SteamVR overlay ([introduction commit](https://github.com/hai-vr/h-view/commit/cb1b35057a2f3ced0becdf9f013ef11b3de78291))
  using the OpenVR API and Veldrid,
  - with some basic overlay mouse input ([class](https://github.com/hai-vr/h-view/blob/main/h-view/src/Overlay/HVImGuiOverlay.cs), [introduction commit](https://github.com/hai-vr/h-view/commit/697f7e61808f3b857940bcd24be05e67b9d3f774)),
  - and some eye tracking input ([class](https://github.com/hai-vr/h-view/blob/main/h-view/src/Overlay/HVImGuiOverlay.cs#L214), [introduction commit](https://github.com/hai-vr/h-view/commit/969b6c23a260c7888b607acb3b4652735bd99db1)).

For more information, [open the website page](https://docs.hai-vr.dev/docs/products/h-view).

https://github.com/user-attachments/assets/889a2648-7cda-4cba-bb0b-23cf1c96ddaf

https://github.com/user-attachments/assets/253ae182-1db5-4dd5-a260-9eb78ceb48f0

### About VRChat login

This page previously had a section about VRChat login. It can be found [at this link](removed.md).

### Launch options

- *No option specified:* Starts as a desktop window. If SteamVR is running, it also creates an additional dashboard overlay.
- `--no-overlay` Starts as a desktop window.
- `--register-manifest` On `Debug` config only: Register the application path to SteamVR. By default, debug builds do not register themselves.
- `--no-register-manifest` On `Release` config only: Do not register the application path to SteamVR.

### Conditional compilation

This project has various compilation constants available, see h-view/src/ConditionalCompilation.cs

- INCLUDES_OCR (~~Release~~, Debug): Include OCR text recognition and translation from screenshots.
- INCLUDES_OPENVR (**Release**, Debug): Include OpenVR/SteamVR functionality. When not set, only desktop functions will be active.
- INCLUDES_STEAMWORKS (~~Release~~, Debug): Include Steamworks DLL, intended for use with Steam distribution.
- REGISTER_MANIFEST (**Release**, ~~Debug~~): Register the application manifest path, so that SteamVR can provide the option to auto-start it with SteamVR.
- COOKIES_SUPPORTED (~~Release~~, ~~Debug~~): Enable communication with VRChat API, see [VRChat Login](#about-vrchat-login) section above.
- ENABLE_FAKE_VRC_OSC (~~Release~~, ~~Debug~~): Create a fake VRChat OSC server, so that other applications may send data to H-View when VRChat is not running (i.e. face tracking).
- USE_CONSTS_IN_RELEASE (**Release**, ~~Debug~~): Replace static bool properties with const bool fields in ConditionalCompilation.cs.

### Third-party acknowledgements

- Third party acknowledgements can also be found in the HThirdParty/ subfolder:
  - Open HThirdParty/thirdparty-lookup.json
  - For the full license text of the third party dependencies, open HThirdParty/THIRDPARTY-LICENSES/ folder
  - The same information can be found through the user interface within the application, in `Options > Third party acknowledgements`
  - When h-view is executed as a debug build, the file `THIRDPARTY-generated.md` will be generated in the executable folder.


- Included in source code form and DLLs:
  - A3ESimpleOSC for C#, version 0.1 @ https://github.com/lyuma/Av3Emulator/blob/master/Runtime/Scripts/A3ESimpleOSC.cs ([MIT license](https://github.com/lyuma/Av3Emulator/blob/master/Runtime/Scripts/A3ESimpleOSC.cs)) by Lyuma
  - ImGui.NET SampleProgram @ https://github.com/ImGuiNET/ImGui.NET/tree/master/src/ImGui.NET.SampleProgram ([MIT license](https://github.com/ImGuiNET/ImGui.NET/blob/master/LICENSE)) by Eric Mellino and ImGui.NET contributors
  - OpenVR API @ https://github.com/ValveSoftware/openvr ([BSD-3-Clause license](https://github.com/ValveSoftware/openvr/blob/master/LICENSE)) by Valve Corporation
  - openvr-screengrab @ https://github.com/cnlohr/openvr-screengrab ([MIT license](https://github.com/cnlohr/openvr-screengrab/blob/master/LICENSE)) by CNLohr
  - openvr_overlay_model @ https://github.com/cnlohr/openvr_overlay_model ([MIT license](https://github.com/cnlohr/openvr_overlay_model/blob/master/LICENSE)) by CNLohr
- Other dependencies included through NuGet: [h-view/h-view.csproj](h-view/h-view.csproj)
  - Dear ImGui @ https://github.com/ocornut/imgui ([MIT license](https://github.com/ocornut/imgui/blob/master/LICENSE.txt)) by Omar Cornut
  - ImGui.NET @ https://github.com/ImGuiNET/ImGui.NET ([MIT license](https://github.com/ImGuiNET/ImGui.NET/blob/master/LICENSE)) by Eric Mellino and ImGui.NET contributors
  - Veldrid @ https://github.com/veldrid/veldrid ([MIT license](https://github.com/veldrid/veldrid/blob/master/LICENSE)) by Eric Mellino and Veldrid contributors
  - VRChat.OSCQuery @ https://github.com/vrchat-community/vrc-oscquery-lib ([MIT license](https://github.com/Facepunch/Facepunch.Steamworks/blob/master/LICENSE)) by VRChat Inc.
  - Vortice.Windows @ https://github.com/amerkoleci/Vortice.Windows ([MIT license](https://github.com/amerkoleci/Vortice.Windows/blob/main/LICENSE)) by Amer Koleci and Contributors
  - Newtonsoft.Json @ https://github.com/JamesNK/Newtonsoft.Json ([MIT license](https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md)) by James Newton-King
  - *Facepunch.Steamworks* @ https://github.com/Facepunch/Facepunch.Steamworks ([MIT license](https://github.com/Facepunch/Facepunch.Steamworks/blob/master/LICENSE)) by Facepunch Studios LTD (conditionally included when INCLUDES_STEAMWORKS flag is set)
  - *Argos Translate* @ https://github.com/argosopentech/argos-translate ([MIT license](https://github.com/argosopentech/argos-translate/blob/master/LICENSE)) by Argos Open Technologies, LLC (conditionally included when INCLUDES_OCR flag is set)
  - *Python.NET* @ https://github.com/pythonnet/pythonnet ([MIT license](https://github.com/pythonnet/pythonnet/blob/master/LICENSE)) by the contributors of the Python.NET project (conditionally included when INCLUDES_OCR flag is set)
  - (there may be other implicit packages)
- Asset dependencies:
  - Fredoka font @ https://fonts.google.com/specimen/Fredoka/about ([SIL Open Font License](https://fonts.google.com/attribution)) by Milena Brandão, Hafontia
  - Kiwi Maru font @ https://fonts.google.com/specimen/Kiwi+Maru/about ([SIL Open Font License](https://fonts.google.com/attribution)) by Hiroki-Chan
  - K14 font @ https://ja.wikipedia.org/wiki/K14_(フォント) ([Public domain](https://ja.wikipedia.org/wiki/K14_(フォント))) by Hiroshi Tachibana (橘浩志)
  - ProggyClean font @ http://www.proggyfonts.net/ ([MIT License (According to https://github.com/ocornut/imgui/blob/master/docs/FONTS.md#creditslicenses-for-fonts-included-in-repository)](https://github.com/ocornut/imgui/blob/master/docs/FONTS.md#creditslicenses-for-fonts-included-in-repository)) by Tristan Grimmer

### Steamworks

If the project is compiled with `INCLUDE_STEAMWORKS` (only on Debug and ReleaseSteamworks builds), the Steam API will be included.

This is used to build my own application for distribution through Steam. See [Steamworks API](https://partner.steamgames.com/doc/sdk/api).

---
<img src="https://raw.githubusercontent.com/hai-vr/h-view/refs/heads/main/h-view/HAssets/img/DashboardThumb.png" width="128" height="128" alt="Logo" />
