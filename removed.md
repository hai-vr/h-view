# Removed functionality

### Removed: VRChat Login

H-View contains code that allows logging into the VRChat account for the purposes of programmatically switching avatars,
but this code is no longer currently used by the default user interface.

Logging into the VRChat account used to be necessary to switch avatars, but OSC replaces this functionality (with the restriction of only
switching between avatars you uploaded, or favorited, or recently used).

The code for that functionality was not removed so that you may choose to re-enable it to serve your own uses.

The code responsible for all VRChat account actions [can be inspected here](https://github.com/hai-vr/h-view/blob/main/h-view/src/VRCLogin/HVVrcSession.cs).
- Logging into the VRChat account `Login(username, password)` ([API docs](https://vrchatapi.github.io/docs/api/#get-/auth/user))
- Sending a 2FA code to VRChat `VerifyTwofer(code, method)` ([API docs](https://vrchatapi.github.io/docs/api/#post-/auth/twofactorauth/emailotp/verify))
- Logging out `Logout()` ([API docs](https://vrchatapi.github.io/docs/api/#put-/logout))
- Switching avatars `SelectAvatar(avatarId)` ([API docs](https://vrchatapi.github.io/docs/api/#put-/avatars/-avatarId-/select))

Logging in will save a cookie file in the `%APPDATA%/H-View/` folder, called `hview.vrc.cookies.txt`
- This file is used to communicate with your VRChat account. Do not share that file.
- This cookie file will be loaded when you start the program.
- To delete this cookie file, go to Costumes > Login > Logout.