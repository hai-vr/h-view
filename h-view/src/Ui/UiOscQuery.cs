using System.Numerics;
using Hai.HView.Core;
using Hai.HView.OSC;
using Hai.HView.Ui;
using ImGuiNET;

namespace Hai.HView.Gui;

public class UiOscQuery
{
    private readonly ImGuiVR ImGuiVR;
    private readonly HVRoutine _routine;
    private readonly UiSharedData _sharedData;
    private const string AvatarParametersPath = "/avatar/parameters/";
    private const string InputPathRoot = "/input/";
    private const string ChatboxPathRoot = "/chatbox/";
    private const string TrackingPathRoot = "/tracking/";
    private static readonly string[] PathRoots = { AvatarParametersPath, InputPathRoot, ChatboxPathRoot, TrackingPathRoot };
    private const string RootPath = "/";
    private const string GestureLeftPath = "/avatar/parameters/GestureLeft";
    private const string GestureRightPath = "/avatar/parameters/GestureRight";
    private const string VisemePath = "/avatar/parameters/Viseme";
    private const string TrackingTypePath = "/avatar/parameters/TrackingType";
    private const string AvatarChangePath = "/avatar/change";
    private const string FloatOscTypeF = "f";
    private const string BoolOscTypeT = "T";
    private const string IntOscTypeI = "i";
    private const string ChatboxOscSpecialtyTypeSTT = "sTT";
    private readonly Vector4 _redColor = new Vector4(1, 0, 0, 0.75f);
    
    private string _chatboxBuffer = "";
    private bool _chatboxB;
    private bool _chatboxN;

    public UiOscQuery(ImGuiVR imGuiVr, HVRoutine routine, UiSharedData sharedData)
    {
        ImGuiVR = imGuiVr;
        _routine = routine;
        _sharedData = sharedData;
    }

    private const string ThirdParty_FaceTrackingPath = "/avatar/parameters/FT/v2/";

    public void AvatarTab(Dictionary<string, HOscItem> messages)
    {
        var filtered = messages.Values.Where(item => !item.IsDisabled).ToArray();
        if (false && ImGuiVR.HapticButton(HLocalizationPhrase.RandomizeParametersLabel))
        {
            SendRandomAvatarParameters(filtered);
        }
        MakeOscTable(AvatarParametersPath, filtered.Where(item => item.Key.StartsWith(AvatarParametersPath)
                                                                  && !item.Key.StartsWith(ThirdParty_FaceTrackingPath)), _sharedData.ManifestNullable != null);
        MakeOscTable(RootPath, filtered.Where(item => !PathRoots.Any(path => item.Key.StartsWith(path))));
    }

    public void FaceTrackingTab(Dictionary<string, HOscItem> messages)
    {
        var filtered = messages.Values.Where(item => !item.IsDisabled).ToArray();
        MakeOscTable(ThirdParty_FaceTrackingPath, filtered.Where(item => item.Key.StartsWith(ThirdParty_FaceTrackingPath)), _sharedData.ManifestNullable != null, AvatarParametersPath);
        
        var randFiltered = filtered.Where(item => item.Key.StartsWith(ThirdParty_FaceTrackingPath)).ToArray();
        if (ImGuiVR.HapticButton(HLocalizationPhrase.RandomizeParametersLabel))
        {
            SendRandomAvatarParameters(randFiltered);
        }
        RandomPercent(filtered, 0.75f);
        RandomPercent(filtered, 0.50f);
        RandomPercent(filtered, 0.33f);
        RandomPercent(filtered, 0.20f);
        RandomPercent(filtered, 0.10f);
        ImGui.SameLine();
        if (ImGuiVR.HapticButton("Reset"))
        {
            ResetFaceTrackingParameters(randFiltered);
        }
    }

    private void RandomPercent(HOscItem[] filtered, float normalized)
    {
        ImGui.SameLine();
        if (ImGuiVR.HapticButton($"{normalized * 100}%"))
        {
            var rand = new Random();
            var randFiltered2 = filtered.Where(item => item.Key.StartsWith(ThirdParty_FaceTrackingPath))
                .OrderBy(item => rand.Next())
                .Take((int)(filtered.Length * normalized))
                .ToArray();
            SendRandomAvatarParameters(randFiltered2);
        }
    }

    public void InputTab(Dictionary<string, HOscItem> messages)
    {
        var filtered = messages.Values.Where(item => !item.IsDisabled).ToArray();
        MakeOscTable(InputPathRoot, filtered.Where(item => item.Key.StartsWith(InputPathRoot)));
        MakeOscTable(ChatboxPathRoot, filtered.Where(item => item.Key.StartsWith(ChatboxPathRoot)));
    }

    public void TrackingTab(Dictionary<string, HOscItem> messages)
    {
        var filtered = messages.Values.Where(item => !item.IsDisabled).ToArray();
        MakeOscTable(TrackingPathRoot, filtered.Where(item => item.Key.StartsWith(TrackingPathRoot)));
    }

    private void SendRandomAvatarParameters(HOscItem[] messages)
    {
        var r = new Random();
        foreach (var item in messages
                     .Where(item => item.Key.StartsWith(AvatarParametersPath)))
        {
            switch (item.OscType)
            {
                case FloatOscTypeF:
                {
                    var range = r.NextSingle() * (item.Key.EndsWith("X") || item.Key.EndsWith("Y") ? 2 - 1f : 1f);
                    _routine.UpdateMessage(item.Key, range);
                    break;
                }
                case BoolOscTypeT:
                    _routine.UpdateMessage(item.Key, r.NextSingle() > 0.5f);
                    break;
                case IntOscTypeI:
                    _routine.UpdateMessage(item.Key, (int)Math.Floor(r.NextSingle() * 256));
                    break;
            }
        }
    }

    private void ResetFaceTrackingParameters(HOscItem[] messages)
    {
        foreach (var item in messages
                     .Where(item => item.Key.StartsWith(AvatarParametersPath)))
        {
            switch (item.OscType)
            {
                case FloatOscTypeF:
                    _routine.UpdateMessage(item.Key, item.Key.Contains("EyeLid") ? 0.8f : 0f);
                    break;
                case BoolOscTypeT:
                    _routine.UpdateMessage(item.Key, item.Key.Contains("PupilDilation1") || item.Key.Contains("PupilDilation4"));
                    break;
                case IntOscTypeI:
                    _routine.UpdateMessage(item.Key, 0);
                    break;
            }
        }
    }

    private void MakeOscTable(string title, IEnumerable<HOscItem> enumerable, bool showIsLocal = false, string copyPrefixNullable = null)
    {
        ImGui.BeginTable(title, showIsLocal ? 5 : 4);
        if (showIsLocal)
        {
            ImGui.TableSetupColumn("=", ImGuiTableColumnFlags.WidthFixed, 40);
        }
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 20);
        ImGui.TableSetupColumn(HLocalizationPhrase.TypeLabel, ImGuiTableColumnFlags.WidthFixed, 45);
        ImGui.TableSetupColumn(HLocalizationPhrase.ValueLabel, ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn(title, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();
        var items = enumerable
            .OrderBy(item => !item.IsWritable)
            .ThenBy(item => item.Key)
            .ToArray();
        foreach (HOscItem oscItem in items)
        {
            var id = 0;
            ImGui.TableNextRow();

            if (showIsLocal)
            {
                _sharedData.isLocal.TryGetValue(oscItem.Key.Substring(AvatarParametersPath.Length), out var isLocal);
                ImGui.TableSetColumnIndex(id++);
                ImGui.Text(isLocal ? "local" : "");
            }
            
            ImGui.TableSetColumnIndex(id++);
            
            // We don't know whether parameters not received through Query are writable.
            var r = oscItem.IsReadable ? "r" : " ";
            var w = oscItem.IsWritable ? "w" : oscItem.IsAbsentFromQuery ? "?" : " ";
            
            
            ImGui.Text($"{r}{w}");
            ImGui.TableSetColumnIndex(id++);
            if (oscItem.OscType.Length == 1)
            {
                ImGui.Text($"{MakeOscTypeReadable(oscItem)}");
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.75f, 0.5f, 0.5f, 1));
                ImGui.Text($"{oscItem.OscType}");
                ImGui.PopStyleColor();
            }
            ImGui.TableSetColumnIndex(id++);
            BuildControls(oscItem, -1, oscItem.Key);
            
            ImGui.TableSetColumnIndex(id++);
            ComputeNameColumn(title, copyPrefixNullable, oscItem);
        }

        ImGui.EndTable();
    }

    private void ComputeNameColumn(string title, string copyPrefixNullable, HOscItem oscItem)
    {
        var key = oscItem.Key;
        var shortstring = key.Substring(title.Length);
        var parameterCopyStringNullable = copyPrefixNullable != null ? key.Substring(copyPrefixNullable.Length) : null;
        var onlyChangedOnce = oscItem.IsReadable && oscItem.DifferentValueCount <= 1;
        if (onlyChangedOnce) // Color near-unchanged values to help finding out "unused" or "frozen value (updated once)" parameters.
        {
            ImGui.PushStyleColor(ImGuiCol.Text, _redColor);
        }
        ImGui.Text($"{shortstring}");
        if (onlyChangedOnce)
        {
            ImGui.PopStyleColor();
        }
        if (oscItem.Description != "" && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(oscItem.Description);
            ImGui.EndTooltip();
        }
        if (ImGui.BeginPopupContextItem($"a popup##{key}"))
        {
            if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{key}\" ({HLocalizationPhrase.AddressLabel})")) ImGui.SetClipboardText(key);
            if (parameterCopyStringNullable != null)
            {
                // For the face tracking address, we want to copy the parameter name, including the FT/ prefix.
                if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{parameterCopyStringNullable}\"")) ImGui.SetClipboardText(parameterCopyStringNullable);
            }
            else
            {
                if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{shortstring}\"")) ImGui.SetClipboardText(shortstring);
            }
            if (oscItem.Values != null)
            {
                var join = string.Join(",", oscItem.Values.Select(o => o.ToString()));
                if (ImGui.Selectable($"{HLocalizationPhrase.CopyLabel} \"{join}\" ({HLocalizationPhrase.ValueLabel})")) ImGui.SetClipboardText(join);
            }
            ImGui.EndPopup();
        }
    }

    internal void BuildControls(HOscItem oscItem, float sliderMin, string key)
    {
        if (oscItem.IsWritable)
        {
            // Sometimes the OscType received through Query, and the received type do not agree.
            // That's why we double-check here.
            // This often happens when the Animator type is not the same as the declared Expression Parameter type.
            if (oscItem.OscType == FloatOscTypeF && oscItem.WriteOnlyValueRef is float)
            {
                float f = (float)oscItem.WriteOnlyValueRef;
                if (ImGui.SliderFloat($"###{key}.slider", ref f, sliderMin, 1))
                {
                    _routine.UpdateMessage(oscItem.Key, f);
                }

                ImGui.SameLine();
                if (ImGuiVR.HapticButton($"0##{key}=0"))
                {
                    _routine.UpdateMessage(oscItem.Key, 0f);
                }

                ImGui.SameLine();
                if (ImGuiVR.HapticButton($"1##{key}=1"))
                {
                    _routine.UpdateMessage(oscItem.Key, 1f);
                }
            }
            else if (oscItem.OscType == BoolOscTypeT && oscItem.WriteOnlyValueRef is bool)
            {
                var b = (bool)oscItem.WriteOnlyValueRef;
                var doit = b;
                if (doit) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 1, 1, 0.75f));
                if (ImGuiVR.HapticButton($"{b}##{key}.toggle", new Vector2(ImGui.GetContentRegionAvail().X - 50 - 20, 0f)))
                {
                    _routine.UpdateMessage(oscItem.Key, !b);
                }
                if (doit) ImGui.PopStyleColor();
                ImGui.SameLine();
                if (ImGuiVR.HapticButton($"{HLocalizationPhrase.HoldLabel}##{key}.hold",
                        new Vector2(50, 0f))) ;
                // {
                //     _routine.UpdateMessage(oscItem.Key, !b);
                // }
                _routine.EmitOscFlipEventOnChange(oscItem.Key, ImGui.IsItemActive());

                ImGui.SameLine();
                // ImGui.PushButtonRepeat(true);
                // if (ImGuiVR.HapticButton($"!!##{key}.punch"))
                // {
                // _routine.SendOsc(oscItem.key, !b);
                // }
                // ImGui.PopButtonRepeat();
            }
            else if (oscItem.OscType == IntOscTypeI && oscItem.WriteOnlyValueRef is int)
            {
                var ii = (int)oscItem.WriteOnlyValueRef;
                if (ImGuiVR.HapticButton($"-##{key}.-"))
                {
                    _routine.UpdateMessage(oscItem.Key, Math.Clamp(ii - 1, 0, 255));
                }

                ImGui.SameLine();
                ImGui.PushItemWidth(80);
                var doit = ii == 1;
                if (doit) ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(0, 1, 1, 0.75f));
                if (ImGui.SliderInt($"##{key}.slider", ref ii, 0, 255))
                {
                    _routine.UpdateMessage(oscItem.Key, ii);
                }
                if (doit) ImGui.PopStyleColor();

                ImGui.PopItemWidth();
                ImGui.SameLine();
                if (ImGuiVR.HapticButton($"+##{key}.+"))
                {
                    _routine.UpdateMessage(oscItem.Key, Math.Clamp(ii + 1, 0, 255));
                }

                ImGui.SameLine();
                if (ImGuiVR.HapticButton($"0##{key}=0"))
                {
                    _routine.UpdateMessage(oscItem.Key, 0);
                }

                ImGui.SameLine();
                if (ImGuiVR.HapticButton($"1##{key}=1"))
                {
                    _routine.UpdateMessage(oscItem.Key, 1);
                }

                ImGui.SameLine();
                if (ImGuiVR.HapticButton($"!##{key}=0"))
                {
                    _routine.UpdateMessage(oscItem.Key, ii > 0 ? 0 : 1);
                }
            }
            else if (oscItem.OscType == ChatboxOscSpecialtyTypeSTT)
            {
                ImGui.InputText($"##{oscItem.Key}.input", ref _chatboxBuffer, 10_000);
                ImGui.SameLine();
                if (ImGuiVR.HapticButton(_chatboxB ? $"{HLocalizationPhrase.SendLabel}##{oscItem.Key}.press" : $"{HLocalizationPhrase.KeyboardLabel}##{oscItem.Key}.press"))
                {
                    var userinput_message = _chatboxBuffer;
                    Console.WriteLine(userinput_message);
                    _routine.UpdateMessageMultivalue(oscItem.Key, new object[] {userinput_message, _chatboxB, _chatboxN});
                }
                ImGui.Checkbox($"Send##{oscItem.Key}.check_b", ref _chatboxB);
                ImGui.SameLine();
                ImGui.Checkbox($"Notify##{oscItem.Key}.check_b", ref _chatboxN);
            }
            else
            {
                // TODO: Handle type mismatches
                var value = oscItem.WriteOnlyValueRef;
                ImGui.PushStyleColor(ImGuiCol.Text, _redColor);
                ImGui.Text($"{value} ({oscItem.OscType} | {oscItem.WriteOnlyValueRef.GetType().Name})");
                ImGui.PopStyleColor();
            }
        }
        else
        {
            if (oscItem.Values == null || oscItem.Values.Length == 0)
            {
                ImGui.Text("");
            }
            else if (oscItem.Key == AvatarChangePath)
            {
                var avatarIdStr = (string)oscItem.Values[0];
                if (ImGuiVR.HapticButton($"{HLocalizationPhrase.OpenBrowserLabel} ({avatarIdStr})"))
                {
                    UiUtil.OpenAvatarUrl(avatarIdStr);
                }
            }
            else if (IsSpecial(oscItem.Key))
            {
                ImGui.Text($"{string.Join(",", oscItem.Values.Select(o => o.ToString()))} ({Specialty(oscItem)})");
            }
            else
            {
                var isTruthy = IsTruthy(oscItem);
                if (isTruthy) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 1, 0.75f));
                ImGui.Text($"{string.Join(",", oscItem.Values.Select(o => o is float ? $"{o:0.###}" : o.ToString()))}");
                if (isTruthy) ImGui.PopStyleColor();
            }
        }
    }

    private static string MakeOscTypeReadable(HOscItem oscItem)
    {
        switch (oscItem.OscType)
        {
            case BoolOscTypeT: return "bool";
            case FloatOscTypeF: return "float";
            case IntOscTypeI: return "int";
            case "s": return "string";
            default: return oscItem.OscType;
        }
    }

    private static bool IsTruthy(HOscItem oscItem)
    {
        var hasValue = oscItem.Values != null && oscItem.Values.Length == 1;
        if (!hasValue) return false;
        
        var value = oscItem.Values[0];
        return (value is int && (int)value == 1)
               || (value is float f && f == 1f)
               || (value is long l && l == 1L)
               || (value is double d && d == 1d)
               || (value is bool b && b);

    }

    private bool IsSpecial(string key)
    {
        switch (key)
        {
            case GestureLeftPath:
            case GestureRightPath:
            case VisemePath:
            case TrackingTypePath:
                return true;
            default:
                return false;
        }
    }

    private string Specialty(HOscItem oscItem)
    {
        var intifiedValue = oscItem.Values[0] is long ? (int)(long)oscItem.Values[0]
            : oscItem.Values[0] is int ? (int)oscItem.Values[0]
            : -1;
        switch (oscItem.Key)
        {
            case GestureLeftPath:
            case GestureRightPath:
                switch (intifiedValue)
                {
                    case 0: return "Neutral";
                    case 1: return "Fist";
                    case 2: return "HandOpen";
                    case 3: return "FingerPoint";
                    case 4: return "Victory";
                    case 5: return "RockNRoll";
                    case 6: return "HandGun";
                    case 7: return "ThumbsUp";
                    default: return "?";
                }
            case VisemePath:
                switch (intifiedValue)
                {
                    case 0: return "sil";
                    case 1: return "pp";
                    case 2: return "ff";
                    case 3: return "th";
                    case 4: return "dd";
                    case 5: return "kk";
                    case 6: return "ch";
                    case 7: return "ss";
                    case 8: return "nn";
                    case 9: return "rr";
                    case 10: return "aa";
                    case 11: return "e";
                    case 12: return "i";
                    case 13: return "o";
                    case 14: return "u";
                    default: return "?";
                }
            case TrackingTypePath:
                switch (intifiedValue)
                {
                    case 0: return "Uninitialized";
                    case 1: return "Generic rig";
                    case 2: return "Avatars 2.0 No Fingers";
                    case 3: return "3pt VR/Desktop Humanoid";
                    case 4: return "Head, Hands and hip";
                    case 6: return "Full Body Tracking";
                    default: return "?";
                }
            default:
                return "?";
        }
    }
}