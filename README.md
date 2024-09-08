H-View
===

Hello,

This repository is mostly a personal learning project and has three main functions:
- It is my personal OSC Query debugger, and communicates with VRChat by listing all the available addresses.
- It can display the entire Expressions Menu if that menu was exported during the avatar build process using [another Unity Editor tool](https://github.com/hai-vr/external-expressions-menu/)
  I made specifically for this purpose ([learn more](https://docs.hai-vr.dev/docs/products/h-view)).
- In addition to the desktop window, it also has an implementation of the ImGui.NET window [being rendered into a SteamVR overlay](https://github.com/hai-vr/h-view/commit/cb1b35057a2f3ced0becdf9f013ef11b3de78291)
  using the OpenVR API and Veldrid, and some [basic overlay mouse input](https://github.com/hai-vr/h-view/commit/697f7e61808f3b857940bcd24be05e67b9d3f774).
- If you choose to log-in into the VRChat account, it can switch between avatars.
    - The code responsible for all VRChat account actions [can be inspected here](https://github.com/hai-vr/h-view/blob/main/h-view/src/VRCLogin/HVVrcSession.cs).
      - Logging into the VRChat account `Login(username, password)` ([API docs](https://vrchatapi.github.io/docs/api/#get-/auth/user))
      - Sending a 2FA code to VRChat `VerifyTwofer(code, method)` ([API docs](https://vrchatapi.github.io/docs/api/#post-/auth/twofactorauth/emailotp/verify))
      - Logging out `Logout()` ([API docs](https://vrchatapi.github.io/docs/api/#put-/logout))
      - Switching avatars `SelectAvatar(avatarId)` ([API docs](https://vrchatapi.github.io/docs/api/#put-/avatars/-avatarId-/select))
    - Logging in will save a cookie file in the program folder, called `hview.cookies.txt`
        - This file is used to communicate with your VRChat account. Do not share that file.
        - This cookie file will be loaded when you start the program.
        - To delete this cookie file, go to Costumes > Login > Logout.

For more information, [open the website page](https://docs.hai-vr.dev/docs/products/h-view).

https://github.com/user-attachments/assets/889a2648-7cda-4cba-bb0b-23cf1c96ddaf

### Launch options

- *No option specified:* Starts as a desktop window. If SteamVR is running, it also creates an additional dashboard overlay.
- `--no-overlay` Starts as a desktop window.

### Third-party acknowledgements

- Included in source code form and DLLs: [h-view/THIRDPARTY.md](h-view/THIRDPARTY.md)
  - A3ESimpleOSC @ https://github.com/lyuma/Av3Emulator/blob/master/Runtime/Scripts/A3ESimpleOSC.cs ([MIT License](https://github.com/lyuma/Av3Emulator/blob/master/Runtime/Scripts/A3ESimpleOSC.cs))
  - ImGui.NET @ https://github.com/ImGuiNET/ImGui.NET ([MIT License](https://github.com/ImGuiNET/ImGui.NET/blob/master/LICENSE))
  - OpenVR API @ https://github.com/ValveSoftware/openvr ([BSD-3-Clause license](https://github.com/ValveSoftware/openvr/blob/master/LICENSE))
- Other dependencies included through NuGet: [h-view/h-view.csproj](h-view/h-view.csproj)
  - Dear ImGui @ https://github.com/ocornut/imgui ([MIT License](https://github.com/ocornut/imgui/blob/master/LICENSE.txt))
  - Veldrid @ https://github.com/veldrid/veldrid ([MIT License](https://github.com/veldrid/veldrid/blob/master/LICENSE))
  - VRChat.OSCQuery @ https://github.com/vrchat-community/vrc-oscquery-lib ([MIT License](https://github.com/vrchat-community/vrc-oscquery-lib/blob/main/License.md))
  - Newtonsoft.Json @ https://github.com/JamesNK/Newtonsoft.Json ([MIT License](https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md))
  - (there may be other implicit packages)