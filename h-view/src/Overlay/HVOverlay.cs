﻿using System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using Hai.HView.Gui;
using Valve.VR;
using Veldrid;

namespace Hai.HView.Overlay;

public class HVOverlayInstance : IOverlayable
{
    private const string DashboardKey = "hview-dashboard";
    public const string OverlayKey = "hview-overlay";

    // Specific to this overlay window
    private readonly HVInnerWindow _innerWindow;
    private readonly string _name;
    private readonly bool _isDashboard;
    private readonly float _ratio;

    private readonly HOverlayInputSnapshot _inputSnapshot = new();
    private readonly HVOverlayMovement _movement = new();
    
    private ulong _handle;
    private Texture_t _vrTexture;
    
    private HVPoseData _mgtPoseData;
    private ulong _thumbHandle;

    private bool _usingEyeTracking;
    private Vector3 _eyePos;
    private Quaternion _eyeGaze;

    public HVOverlayInstance(HVInnerWindow innerWindow, string name, bool isDashboard, float ratio)
    {
        _innerWindow = innerWindow;
        _name = name;
        _isDashboard = isDashboard;
        _ratio = ratio;
    }

    public void Start()
    {
        if (_isDashboard)
        {
            OpenVR.Overlay.CreateDashboardOverlay($"{DashboardKey}-{_name}", HVApp.AppTitle, ref _handle, ref _thumbHandle);
            OpenVR.Overlay.SetOverlayFromFile(_thumbHandle, Path.GetFullPath("DashboardThumb.png"));
        }
        else
        {
            OpenVR.Overlay.CreateOverlay($"{OverlayKey}-{_name}", HVApp.AppTitle, ref _handle);
        }
        ApplyOverlayProps();

        _vrTexture = new Texture_t
        {
            handle = _innerWindow.GetOverlayTexturePointer(),
            eType = ETextureType.DirectX,
            eColorSpace = EColorSpace.Auto
        };
    }

    public void ProcessThatOverlay(Stopwatch stopwatch)
    {
        if (!_isDashboard)
        {
            _movement.Evaluate(_handle, _mgtPoseData);
        }
        
        _innerWindow.SetAsActiveContext();
        // FIXME: For now, this prevents the window (that we're not even using) from freezing.
        // Now that the window is hidden, maybe this is no longer necessary? Not sure, if the task manager or other
        // system relies on us pulling those events.
        var snapshot = _innerWindow.DoPumpEvents();

        _inputSnapshot.Deaccumulate();
        _inputSnapshot.SetWindowSize(_innerWindow.WindowSize());
        PollOverlayEvents();
        ProcessEyeTracking();

        // Only render when the overlay is visible
        // TODO: Input events may need some special handling
        if (OpenVR.Overlay.IsOverlayVisible(_handle))
        {
            // TODO: Open the VR keyboard whenever a text field in ImGui asks for input capture.
            // TODO: Figure out how to make third-party keyboard apps like XSOverlay still able to write text into our windowless instance.
            
            _innerWindow.UpdateAndRender(stopwatch, _inputSnapshot);
        }
        
        OpenVR.Overlay.SetOverlayTexture(_handle, ref _vrTexture);
    }

    public void Teardown()
    {
        OpenVR.Overlay.DestroyOverlay(_handle);
    }

    private void ApplyOverlayProps()
    {
        var verticalTrim = (1 - (1 / _ratio)) / 2f;
        var bounds = new VRTextureBounds_t
        {
            uMin = 0, uMax = 1,
            vMin = verticalTrim, vMax = 1f - verticalTrim
        };
        OpenVR.Overlay.SetOverlayTextureBounds(_handle, ref bounds);
        OpenVR.Overlay.SetOverlayInputMethod(_handle, VROverlayInputMethod.Mouse);
        OpenVR.Overlay.SetOverlayFlag(_handle, VROverlayFlags.SendVRSmoothScrollEvents, true);
        
        if (!_isDashboard)
        {
            OpenVR.Overlay.SetOverlayWidthInMeters(_handle, 0.35f);
            OpenVR.Overlay.SetOverlayAlpha(_handle, 1f);
            OpenVR.Overlay.SetOverlayColor(_handle, 1f, 1f, 1f);
            OpenVR.Overlay.SetOverlayCurvature(_handle, 0.2f);
            OpenVR.Overlay.ShowOverlay(_handle);
        }
        else
        {
            OpenVR.Overlay.SetOverlayWidthInMeters(_handle, 3f);
        }
    }

    private void PollOverlayEvents()
    {
        VREvent_t evt = default;
        
        // TODO: Keyboard events
        while (OpenVR.Overlay.PollNextOverlayEvent(_handle, ref evt, HVOpenVRManagement.SizeOfVrEvent))
        {
            // https://github.com/ValveSoftware/openvr/blob/master/headers/openvr.h#L773
            var type = (EVREventType)evt.eventType;
            
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            ProcessEvent(type, evt);
        }
    }

    private void ProcessEvent(EVREventType type, VREvent_t evt)
    {
        switch (type)
        {
            case EVREventType.VREvent_MouseMove:
            {
                var data = evt.data.mouse;
                _inputSnapshot.MouseMove(new Vector2(data.x, 1 - data.y));
                break;
            }
            case EVREventType.VREvent_ScrollDiscrete:
            {
                var data = evt.data.scroll;
                _inputSnapshot.Scrolling(data.ydelta);
                break;
            }
            case EVREventType.VREvent_ScrollSmooth:
            {
                var data = evt.data.scroll;
                _inputSnapshot.Scrolling(data.ydelta);
                break;
            }
            case EVREventType.VREvent_MouseButtonDown:
            {
                if (TryAsVeldrid(evt.data.mouse.button, out var veldridButton)) _inputSnapshot.MouseDown(veldridButton);
                break;
            }
            case EVREventType.VREvent_MouseButtonUp:
            {
                if (TryAsVeldrid(evt.data.mouse.button, out var veldridButton)) _inputSnapshot.MouseUp(veldridButton);
                break;
            }
        }
    }

    private bool TryAsVeldrid(uint vrButton, out MouseButton veldridButton)
    {
        // EVRMouseButton
        switch (vrButton)
        {
            case 1:
                veldridButton = MouseButton.Left;
                return true;
            case 2:
                veldridButton = MouseButton.Right;
                return true;
            case 3:
                veldridButton = MouseButton.Middle;
                return true;
            default:
                veldridButton = MouseButton.LastButton;
                return false;
        }
    }

    public void ProvidePoseData(HVPoseData poseData)
    {
        _mgtPoseData = poseData;
    }

    public ulong GetOverlayHandle()
    {
        return _handle;
    }

    public void ProvideEyeTracking(Vector3 eyePos, Quaternion eyeGaze)
    {
        _usingEyeTracking = true;
        _eyePos = eyePos;
        _eyeGaze = eyeGaze;
    }

    private void ProcessEyeTracking()
    {
        if (!_usingEyeTracking) return;

        // (??????) Why do I have to inverse the eye gaze quaternion? (??????)
        var quaternion = Quaternion.Inverse(_eyeGaze);
        
        var gazeDir = Vector3.Transform(new Vector3(0, 0, -1), quaternion);
        VROverlayIntersectionParams_t intersectionParams = new VROverlayIntersectionParams_t
        {
            eOrigin = OpenVR.Compositor.GetTrackingSpace(),
            vSource = HVOvrGeofunctions.Vec(_eyePos),
            vDirection = HVOvrGeofunctions.Vec(gazeDir)
        };
        VROverlayIntersectionResults_t results = default;
        var success = OpenVR.Overlay.ComputeOverlayIntersection(_handle, ref intersectionParams, ref results);
        if (success)
        {
            var x01 = results.vUVs.v0;
            var y01 = results.vUVs.v1;
            if (x01 is > 0f and < 1f && y01 is > 0f and < 1f)
            {
                _inputSnapshot.MouseMove(new Vector2(x01, 1 - y01));
            }
        }
    }
}