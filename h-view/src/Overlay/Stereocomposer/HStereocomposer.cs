using System.Diagnostics;
using System.Numerics;
using System.Text;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.OVR;
using Hai.HView.Rendering;
using Hai.HView.Ui;
using Hai.HView.Ui.MainApp;
using ImGuiNET;
using Valve.VR;
using Veldrid.Sdl2;

namespace Hai.HView.Overlay.Stereocomposer;

// This class is an experiment to render stereoscopic content using ImGui.
// It makes rendering gizmos and rays in an overlay possible, in order to visualize and test math operations.
//
// The code has not been cleaned out.
public class HStereocomposer : IOverlayable
{
    private readonly SavedData _config;
    private readonly HVRoutine _routine;
    private readonly HGrabMachine _grabMachine;
    private readonly HVOpenVRManagement _ovr;
    private const string Name = "stereocomposer";

    private readonly HVRendering _rendering;
    
    private readonly HOverlayInputSnapshot _inputSnapshot = new();
    private ulong _handle;
    private HVPoseData _poseData;
    private Texture_t _vrTexture;
    
    private int Width;
    private int Height;
    private long _nextRefreshTime;
    private bool _refreshing;
    private ulong _xsowindowZeroable;
    private Stopwatch _sw;
    private List<ulong> _xsoWindows = new();
    private List<ulong> _allWindows = new();
    private readonly UiThemeUpdater _theme;
    private Matrix4x4 absToHead;
    private Matrix4x4 headToEyeL;
    private Matrix4x4 camToScreenL;
    private Matrix4x4 headToEyeR;
    private Matrix4x4 camToScreenR;
    private float _scale;
    private float _moveAmount;
    private float _near;
    private float _far;
    private ImDrawListPtr drawList;
    
    private Vector3 _virtualThingPos = Vector3.Zero;
    private Quaternion _virtualThingRot = Quaternion.Identity;
    private int _lastGrabHandle;
    private ulong _lastGrabOverlay;
    private long _tookMs;
    private readonly Dictionary<ulong, string> _handleToNameCache = new Dictionary<ulong, string>();

    public HStereocomposer(SavedData config, HVRoutine routine, HGrabMachine grabMachine, HVOpenVRManagement ovr)
    {
        _config = config;
        _routine = routine;
        _grabMachine = grabMachine;
        _ovr = ovr;
        uint rtW = 0;
        uint rtH = 0;
        OpenVR.System.GetRecommendedRenderTargetSize(ref rtW, ref rtH);
        // Width = (int)rtW / 2;
        // Height = (int)rtH / 2;
        // var size = 500;
        var size = 1250;
        Width = size;
        Height = size;
        
        var imageLoader = new HVImageLoader();
        _rendering = new HVRendering(true, Width * 2, Width, imageLoader, config);
        _rendering.OnSubmitUi += SubmitUi;

        _sw = new Stopwatch();
        _sw.Start();

        _theme = new UiThemeUpdater();
    }

    public void ProvidePoseData(HVPoseData poseData)
    {
        _poseData = poseData;
        _near = 0.001f;
        _far = 100f;

        absToHead = HVOvrGeofunctions.OvrToOvrnum(_poseData.PredictedPoses[0].mDeviceToAbsoluteTracking);
        headToEyeL = HVOvrGeofunctions.OvrToOvrnum(OpenVR.System.GetEyeToHeadTransform(EVREye.Eye_Left));
        headToEyeR = HVOvrGeofunctions.OvrToOvrnum(OpenVR.System.GetEyeToHeadTransform(EVREye.Eye_Right));
        // Using the camera projection matrices does not appear to be the correct solution here.
        // camToScreenL = HVOvrGeofunctions.OvrToOvrnum(OpenVR.System.GetProjectionMatrix(EVREye.Eye_Left, _near, _far));
        // camToScreenR = HVOvrGeofunctions.OvrToOvrnum(OpenVR.System.GetProjectionMatrix(EVREye.Eye_Right, _near, _far));
        
        // _scale = 1f;
        // _moveAmount = 100f;
        _scale = _config.devTools__Scale;
        _moveAmount = _config.devTools__MoveAmount;
        
        // NOTE: I am confused why I need to provide the inverse of the half-scale, instead of half-scale,
        // since (_scale * _moveAmount) represents the width of the viewport,
        // and _moveAmount is the distance to the viewport, making (_scale * _moveAmount * 0.5f) / _moveAmount
        // the tangent of half the FOV.
        var formula = 1 / _scale * 0.5f;
        float fovRad = MathF.Atan(formula) * 2f * _config.devTools__FovTest;
        
        // https://github.com/cnlohr/openvr_overlay_model/blob/master/overlay_model_test.c
        camToScreenL = CNLUtils.OvrPerspective(fovRad, 1, _near, _far);
        camToScreenR = camToScreenL;
    }

    private void SubmitUi(CustomImGuiController controller, Sdl2Window window)
    {
        var start = _sw.ElapsedMilliseconds;
        // TODO: It would be nicer to "render" the same UI twice (L/R) with the same screen positions,
        // on the same texture, with a different region in the rendering process.
        SubmitSidedUi(controller, headToEyeL, camToScreenL, false);
        SubmitSidedUi(controller, headToEyeR, camToScreenR, true);
        
        _tookMs = _sw.ElapsedMilliseconds - start;
    }

    private void SubmitSidedUi(CustomImGuiController controller, Matrix4x4 headToEye, Matrix4x4 camToScreen, bool isRight)
    {
        _theme.OverrideStyleWithTheme(UiColors.V3(UiColors.ActiveButton));
        ImGui.PushFont(_config.useSmallFontVR ? controller.SmallFont : controller.MainFont);
        ImGui.SetNextWindowPos(new Vector2(isRight ? Width : 0, (Width - Height) / 2), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(Width, Height), ImGuiCond.Always);
        ImGui.Begin($"stereocomposer-{(isRight ? "right" : "left")}", ImGuiWindowFlags.NoMove
                                      | ImGuiWindowFlags.NoDocking
                                      | ImGuiWindowFlags.NoResize
                                      | ImGuiWindowFlags.NoCollapse
                                      //| ImGuiWindowFlags.NoTitleBar
                                      | ImGuiWindowFlags.NoBackground
                                      | ImGuiWindowFlags.NoScrollbar);
        ImGui.Text($"{_tookMs}ms");
        drawList = ImGui.GetWindowDrawList();

        var ws = new Vector2(isRight ? Width : 0, 0);
        drawList.AddRect(ws + Vector2.One, ws + new Vector2(Width, Height) - Vector2.One, 0xFFFFFFFF);
        
        var absToEye = absToHead * headToEye;
        var eyeToAbs = HVGeofunctions.QuickInvert(absToEye);

        var rightHandMatrix = OpenVRUtils.IsValidDeviceIndex(_poseData.RightHandDeviceIndex) ? HVOvrGeofunctions.OvrToOvrnum(_poseData.Poses[_poseData.RightHandDeviceIndex].mDeviceToAbsoluteTracking) : Matrix4x4.Identity;
        var leftHandMatrix = OpenVRUtils.IsValidDeviceIndex(_poseData.LeftHandDeviceIndex) ? HVOvrGeofunctions.OvrToOvrnum(_poseData.Poses[_poseData.LeftHandDeviceIndex].mDeviceToAbsoluteTracking) : Matrix4x4.Identity;
        HVGeofunctions.ToPosRotV3__RectifiedRotation(rightHandMatrix, out var rightHandPos, out var rightHandRot);
        HVGeofunctions.ToPosRotV3__RectifiedRotation(leftHandMatrix, out var leftHandPos, out var leftHandRot);
        
        var handRaycastDirNorm = Vector3.Normalize(Vector3.Transform(new Vector3(0, -0.84f, -1), rightHandRot));
        DRAW_GIZMO(camToScreen, eyeToAbs, rightHandPos, rightHandRot, ws);
        DRAW_GIZMO(camToScreen, eyeToAbs, leftHandPos, leftHandRot, ws);
        var minOverlay = DRAW_HAND_RAYCAST_ONTO_APPLICABLE_WINDOWS(camToScreen, rightHandPos, eyeToAbs, ws, handRaycastDirNorm);
        // DRAW_LINES_BETWEEN_XSOVERLAY_WINDOWS(camToScreen, eyeToAbs, ws);
        SHOW_BATTERY_LEVEL(camToScreen, eyeToAbs);
        SHOW_XSOVERLAY_INTERNAL_NAMES(camToScreen, eyeToAbs);
        // CREATE_OSC_TRACKERS();
        EXECUTE_GRABMACHINE(camToScreen, rightHandPos, handRaycastDirNorm, eyeToAbs, ws, rightHandMatrix, minOverlay, isRight);

        ImGui.End();
        ImGui.PopFont();
    }

    private void EXECUTE_GRABMACHINE(Matrix4x4 camToScreen, Vector3 rightHandPos, Vector3 handRaycastDirNorm, Matrix4x4 eyeToAbs, Vector2 ws, Matrix4x4 rightHandMatrix, ulong minOverlay, bool isRight)
    {
        var handToThing = _virtualThingPos - rightHandPos;
        var projected = Vector3.Dot(handRaycastDirNorm, handToThing);
        var isRayTouchingSphere = projected > 0 && ((handRaycastDirNorm * projected) - handToThing).Length() < 0.2f;
        if (TryPosToScreen(eyeToAbs, camToScreen, _virtualThingPos, out var screenPos))
        {
            drawList.AddCircle(screenPos + ws, 100, isRayTouchingSphere ? 0xFF0000FF : 0xFFFFFFFF, 60, 10);
        }

        if (!isRight) return;
        
        var grab = OpenVRUtils.GetDigitalInput(_ovr.ActionGrabRight);
        if (grab.bChanged)
        {
            if (grab.bState)
            {
                _lastGrabHandle = isRayTouchingSphere ? _grabMachine.HandleFor("VirtualThing") : minOverlay != 0 ? _grabMachine.HandleFor($"Overlay-{minOverlay}") : 0;
                if (_lastGrabHandle != 0)
                {
                    OpenVRUtils.TriggerHapticPulse(_poseData.RightHandDeviceIndex, 25_000);
                    if (isRayTouchingSphere)
                    {
                        _grabMachine.InitiateGrab(_lastGrabHandle, rightHandMatrix, _virtualThingPos, _virtualThingRot, (pos, rot) =>
                        {
                            _virtualThingPos = pos;
                            _virtualThingRot = rot;
                        });
                    }
                    else
                    {
                        _lastGrabOverlay = minOverlay;
                        HmdMatrix34_t outMatrix = default;
                        var eTrackingUniverseOrigin = OpenVR.Compositor.GetTrackingSpace();
                        OpenVR.Overlay.GetOverlayTransformAbsolute(_lastGrabOverlay, ref eTrackingUniverseOrigin, ref outMatrix);
                        HVGeofunctions.ToPosRotV3(HVOvrGeofunctions.OvrToOvrnum(outMatrix), out var initPos, out var initRot);
                        
                        _grabMachine.InitiateGrab(_lastGrabHandle, rightHandMatrix, initPos, initRot, (pos, rot) =>
                        {
                            var matrix = HVOvrGeofunctions.OvrnumToOvr(HVGeofunctions.TR(pos, rot));
                            EVROverlayError result = OpenVR.Overlay.SetOverlayTransformAbsolute(_lastGrabOverlay, eTrackingUniverseOrigin, ref matrix);
                        });
                    }
                }
            }
            else
            {
                if (_lastGrabHandle != 0) _grabMachine.ReleaseGrab(_lastGrabHandle);
                _lastGrabHandle = 0;
            }
        }
        
        _grabMachine.UpdateGrabbables(rightHandMatrix);
    }

    private ulong DRAW_HAND_RAYCAST_ONTO_APPLICABLE_WINDOWS(Matrix4x4 camToScreen, Vector3 handPos, Matrix4x4 eyeToAbs, Vector2 drawWindowShift, Vector3 handRaycastDirection)
    {
        ulong minOverlay = 0;
        var minDistance = float.MaxValue;
        VROverlayIntersectionResults_t minResultsx = default;
        foreach (var xsoWindow in _allWindows)
        {
            var vparams = new VROverlayIntersectionParams_t
            {
                eOrigin = OpenVR.Compositor.GetTrackingSpace(),
                vSource = HVOvrGeofunctions.Vec(handPos),
                vDirection = HVOvrGeofunctions.Vec(handRaycastDirection)
            };
            VROverlayIntersectionResults_t resultsx = default;
            if (OpenVR.Overlay.ComputeOverlayIntersection(xsoWindow, ref vparams, ref resultsx))
            {
                var x01 = resultsx.vUVs.v0;
                var y01 = resultsx.vUVs.v1;
                if (x01 is >= 0f and <= 1f && y01 is >= 0f and <= 1f)
                {
                    if (resultsx.fDistance < minDistance && resultsx.fDistance != 0)
                    {
                        minDistance = resultsx.fDistance;
                        minResultsx = resultsx;
                        minOverlay = xsoWindow;
                    }
                }
            }
        }

        if (minDistance != float.MaxValue)
        {
            var raycastTarget = new Vector3(minResultsx.vPoint.v0, minResultsx.vPoint.v1, minResultsx.vPoint.v2);
            DrawLine(camToScreen, eyeToAbs, handPos, raycastTarget, drawWindowShift, 0xFF0000FF);
            return minOverlay;
        }
        else
        {
            DrawLine(camToScreen, eyeToAbs, handPos, handPos + handRaycastDirection * 100f, drawWindowShift, 0xFF00FFFF, 5f);
            return 0;
        }
    }

    private void DRAW_LINES_BETWEEN_XSOVERLAY_WINDOWS(Matrix4x4 camToScreen, Matrix4x4 eyeToAbs, Vector2 drawWindowShift)
    {
        for (var i = 0; i < _xsoWindows.Count; i++)
        {
            var from = _xsoWindows[i];

            if (TryOverlayAbsPosToScreen(from, eyeToAbs, camToScreen, out var fromScreen))
            {
                for (var j = i + 1; j < _xsoWindows.Count; j++)
                {
                    var to = _xsoWindows[j];

                    if (TryOverlayAbsPosToScreen(to, eyeToAbs, camToScreen, out var toScreen))
                    {
                        var thickness = 2;
                        drawList.AddLine(fromScreen + drawWindowShift, toScreen + drawWindowShift, 0xFFFFFF00, thickness);
                    }
                }
            }
        }
    }

    private void SHOW_BATTERY_LEVEL(Matrix4x4 camToScreen, Matrix4x4 eyeToAbs)
    {
        for (uint deviceIndex = 0; deviceIndex < _poseData.Poses.Length; deviceIndex++)
        {
            var pose = _poseData.Poses[deviceIndex];
            if (pose.bDeviceIsConnected && pose.bPoseIsValid)
            {
                if (TryAbsPosToScreen(eyeToAbs, camToScreen, pose.mDeviceToAbsoluteTracking, out var fromScreen))
                {
                    ETrackedPropertyError errrr = ETrackedPropertyError.TrackedProp_Success;
                    var value = OpenVR.System.GetFloatTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref errrr) * 100;

                    if (value > 0)
                    {
                        var text = $"Battery: {value:0}%";
                        ImGui.SetCursorPos(fromScreen - ImGui.CalcTextSize(text) * 0.5f);
                        bool b = true;
                        ImGui.Button(text);
                    }
                }
            }
        }
    }

    private void SHOW_XSOVERLAY_INTERNAL_NAMES(Matrix4x4 camToScreen, Matrix4x4 eyeToAbs)
    {
        for (var index = 0; index < _xsoWindows.Count; index++)
        {
            var xsoWindow = _xsoWindows[index];
            if (TryOverlayAbsPosToScreen(xsoWindow, eyeToAbs, camToScreen, out var shift))
            {
                float widthm = 0f;
                OpenVR.Overlay.GetOverlayWidthInMeters(xsoWindow, ref widthm);
                uint ww = 0;
                uint hh = 0;
                OpenVR.Overlay.GetOverlayTextureSize(xsoWindow, ref ww, ref hh);
                var text = GetOverlayNameCached(xsoWindow);
                ImGui.SetCursorPos(shift - ImGui.CalcTextSize(text) * 0.5f + new Vector2(0, 80 + widthm * 300 * (hh) / ((float)ww)));
                bool b = true;
                ImGui.Checkbox(text, ref b);
            }
        }
    }

    private void CREATE_OSC_TRACKERS()
    {
        for (var index = 0; index < _xsoWindows.Count; index++)
        {
            var xsoWindow = _xsoWindows[index];
            // spawn a vrc tracker
            ETrackingUniverseOrigin universe = ETrackingUniverseOrigin.TrackingUniverseStanding;
            HmdMatrix34_t absolute = new HmdMatrix34_t();
            if (OpenVR.Overlay.GetOverlayTransformAbsolute(xsoWindow, ref universe, ref absolute) == EVROverlayError.None)
            {
                HVGeofunctions.ToPosRotV3(HVOvrGeofunctions.OvrToUnity(absolute), out var pos, out var rot);

                // OSC trackers start at 1. Double Hip Tracker uses slot 1 and 2.
                var firstIndex = 3;
                _routine.SendVrcTracker(firstIndex + index, pos, rot);
            }
        }
    }

    private void DRAW_GIZMO(Matrix4x4 camToScreen, Matrix4x4 eyeToAbs, Vector3 pos, Quaternion rot, Vector2 drawWindowShift)
    {
        var rightHandX = Vector3.Transform(Vector3.UnitX, rot);
        var rightHandY = Vector3.Transform(Vector3.UnitY, rot);
        var rightHandZ = Vector3.Transform(Vector3.UnitZ, rot);

        DrawLine(camToScreen, eyeToAbs, pos, pos + rightHandX * 0.05f, drawWindowShift, 0xFF0000FF);
        DrawLine(camToScreen, eyeToAbs, pos, pos + rightHandY * 0.05f, drawWindowShift, 0xFF00FF00);
        DrawLine(camToScreen, eyeToAbs, pos, pos + rightHandZ * 0.05f, drawWindowShift, 0xFFFF0000);
    }

    private void DrawLine(Matrix4x4 camToScreen, Matrix4x4 eyeToAbs, Vector3 pointA, Vector3 pointB, Vector2 drawWindowShift, uint color, float thickness = 10)
    {
        var successA = TryPosToScreen(eyeToAbs, camToScreen, pointA, out var screenRaySource);
        var successB = TryPosToScreen(eyeToAbs, camToScreen, pointB, out var screenRayTarget);

        var atLeastOneIsInFront = successA || successB;
        if (atLeastOneIsInFront)
        {
            // FIXME: Should points located behind the camera have their X and Y inverted?
            drawList.AddLine(screenRaySource + drawWindowShift, screenRayTarget + drawWindowShift, color, thickness);
        }
    }

    private bool TryOverlayAbsPosToScreen(ulong otherHandle, Matrix4x4 eyeToAbs, Matrix4x4 camToScreen, out Vector2 shift)
    {
        HmdMatrix34_t absToObj = default;
        var trackingSpace = OpenVR.Compositor.GetTrackingSpace();
        var err = OpenVR.Overlay.GetOverlayTransformAbsolute(otherHandle, ref trackingSpace, ref absToObj);
        if (err != EVROverlayError.None)
        {
            shift = Vector2.Zero;;
            return false;
        }

        return TryAbsPosToScreen(eyeToAbs, camToScreen, absToObj, out shift);
    }

    private bool TryAbsPosToScreen(Matrix4x4 eyeToAbs, Matrix4x4 camToScreen, HmdMatrix34_t absToObjOvr, out Vector2 shift)
    {
        var absToObj_includesRot = HVOvrGeofunctions.OvrToOvrnum(absToObjOvr);
        HVGeofunctions.ToPosRotV3(absToObj_includesRot, out var pos, out var xsoRot);
        return TryPosToScreen(eyeToAbs, camToScreen, pos, out shift);
    }

    private bool TryPosToScreen(Matrix4x4 eyeToAbs, Matrix4x4 camToScreen, Vector3 pos, out Vector2 shift)
    {
        var absToPos = HVGeofunctions.TR(pos, Quaternion.Identity);

        var eyeToPos = eyeToAbs * absToPos;
        var posInCam = eyeToPos;

        var posInScreen = camToScreen * posInCam;
        HVGeofunctions.ToV4(posInScreen, out var screenPos);

        // var someAdjustmentValue = 1.25f;
        var someAdjustmentValue = 1;
        // var pp = someAdjustmentValue / _scale;
        var pp = someAdjustmentValue;
        var w = screenPos.W;
        var xx = (screenPos.X / w) * pp;
        var yy = (screenPos.Y / w) * pp;
        if (w < 0)
        {
            xx = 1 - xx;
            yy = 1 - yy;
        }

        shift = new Vector2(xx * Width, -yy * Height) + new Vector2(Width / 2, Height / 2);

        if (screenPos.Z < 0)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public void Start()
    {
        OpenVR.Overlay.CreateOverlay($"{HVImGuiOverlay.OverlayKey}-{Name}", $"{HVApp.AppTitle}-{Name}", ref _handle);
        OpenVR.Overlay.SetOverlayAlpha(_handle, 1f);
        OpenVR.Overlay.SetOverlayColor(_handle, 1f, 1f, 1f);
        OpenVR.Overlay.ShowOverlay(_handle);
        OpenVR.Overlay.SetOverlayFlag(_handle, VROverlayFlags.MakeOverlaysInteractiveIfVisible, false);
        OpenVR.Overlay.SetOverlayFlag(_handle, VROverlayFlags.SideBySide_Parallel, true);

        float ratio = Width / (float)Height;
        var verticalTrim = (1 - (1 / ratio)) / 2f;
        var bounds = new VRTextureBounds_t
        {
            uMin = 0, uMax = 1,
            vMin = verticalTrim, vMax = 1f - verticalTrim
        };
        OpenVR.Overlay.SetOverlayTextureBounds(_handle, ref bounds);
        
        _rendering.SetupUi(true);

        _vrTexture = new Texture_t
        {
            handle = _rendering.GetOverlayTexturePointer(),
            eType = ETextureType.DirectX,
            eColorSpace = EColorSpace.Auto
        };
    }

    private void PollOverlayEvents()
    {
        VREvent_t evt = default;
        
        while (OpenVR.Overlay.PollNextOverlayEvent(_handle, ref evt, HVOpenVRManagement.SizeOfVrEvent))
        {
        }
    }

    public void ProcessThatOverlay(Stopwatch stopwatch)
    {
        _rendering.SetAsActiveContext();
        
        if (OpenVR.Overlay.IsOverlayVisible(_handle))
        {
            if (!_refreshing && _sw.ElapsedMilliseconds > _nextRefreshTime)
            {
                _refreshing = true;
                Task.Run(() =>
                {
                    var results = OpenVRUtils.FindAllOverlayHandlesBrute();

                    var allList = new List<ulong>();
                    var list = new List<ulong>();
                    
                    foreach (var brutePair in results)
                    {
                        var handle = brutePair.Key;
                        var name = GetOverlayNameCached(handle);
                        if (name.StartsWith("XSOverlay Window"))
                        {
                            // _xsowindowZeroable = brutePair.Key;
                            list.Add(handle);
                        }

                        VROverlayTransformType transformType = VROverlayTransformType.VROverlayTransform_Absolute;
                        if (OpenVR.Overlay.GetOverlayTransformType(handle, ref transformType) == EVROverlayError.None && transformType == VROverlayTransformType.VROverlayTransform_Absolute)
                            if (name.StartsWith("XSOverlay Window"))
                            {
                                allList.Add(handle);
                            }
                    }

                    _xsoWindows = list;
                    _allWindows = allList;
                    
                    _refreshing = false;
                    _nextRefreshTime = _sw.ElapsedMilliseconds + 200;
                });
            }
            
            _rendering.UpdateAndRender(stopwatch, _inputSnapshot);

            var eyeTracking = new EyeTrackingData
            {
                XAvg = 0,
                Y = 0
            };
            var xx = (float)(Math.Asin(eyeTracking.XAvg) * (180 / Math.PI));
            var yy = (float)(Math.Asin(-eyeTracking.Y) * (180 / Math.PI));
            var eyeRot = HVGeofunctions.QuaternionFromAngles(new Vector3(yy, xx, 0), HVRotationMulOrder.YZX);

            var headToEyeTrackingFocus = HVGeofunctions.TR(new Vector3(0, 0, 0), eyeRot);
            var move = HVGeofunctions.TR(new Vector3(0, 0, -_moveAmount), Quaternion.Identity);
    
            var overlayPlace = HVOvrGeofunctions.OvrnumToOvr(absToHead * headToEyeTrackingFocus * move);
            OpenVR.Overlay.SetOverlayTransformAbsolute(_handle, OpenVR.Compositor.GetTrackingSpace(), ref overlayPlace);
            OpenVR.Overlay.SetOverlayWidthInMeters(_handle, _moveAmount * _scale);
        }
        
        OpenVR.Overlay.SetOverlayTexture(_handle, ref _vrTexture);
        /// ??? poll at the end?
        PollOverlayEvents();
    }

    private string GetOverlayNameCached(ulong handle)
    {
        var isCached = _handleToNameCache.TryGetValue(handle, out var name);
        if (!isCached)
        {
            var nameOrNull = OpenVRUtils.GetOverlayNameOrNull(handle);
            if (nameOrNull != null)
            {
                _handleToNameCache[handle] = nameOrNull;
                name = nameOrNull;
            }
            else
            {
                name = "";
            }
        }

        return name;
    }

    public void Teardown()
    {
        OpenVR.Overlay.DestroyOverlay(_handle);
    }
}