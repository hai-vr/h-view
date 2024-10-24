// MIT License
// 
// Copyright (c) 2021 CNLohr
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
using System.Numerics;
using Valve.VR;

namespace Hai.HView.OVR;

/**
    From https://github.com/cnlohr/openvr_overlay_model/blob/master/overlay_model_test.c
*/
public static class CNLUtils
{
    public static Matrix4x4 OvrPerspective(float f, float aspect, float zNear, float zFar)
    {
//---// void matrix44perspective( float * out, float fovyK, float aspect, float zNear, float zFar )
//---// {
//---// 	//float f = 1./tan(fovy * 3.1415926 / 360.0);
//---// 	float f = fovyK;
//---// 	out[m00] = f/aspect; out[m01] = 0; out[m02] = 0; out[m03] = 0;
//---// 	out[m10] = 0; out[m11] = f; out[m12] = 0; out[m13] = 0;
//---// 	out[m20] = 0; out[m21] = 0;
//---// 	out[m22] = (zFar + zNear)/(zNear - zFar);
//---// 	out[m23] = 2*zFar*zNear  /(zNear - zFar);
//---// 	out[m30] = 0; out[m31] = 0; out[m32] = -1; out[m33] = 0;
//---// }
        return new Matrix4x4(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (zFar + zNear) / (zNear - zFar), 2 * zFar * zNear / (zNear - zFar),
            0, 0, -1, 0
        );
    }

    /// The unit appears to be in "secondsToPhotonsFromNow"
    public static float OvrPredictedTime()
    {
        float last_vsync_time = 0;
        ulong frameCounter = 0;
        OpenVR.System.GetTimeSinceLastVsync( ref last_vsync_time, ref frameCounter);
        ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
        float display_frequency = OpenVR.System.GetFloatTrackedDeviceProperty(OpenVR.k_unTrackedDeviceIndex_Hmd, ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref error);
        float frame_period = 1f / display_frequency;
        float vsync_to_photons = OpenVR.System.GetFloatTrackedDeviceProperty( OpenVR.k_unTrackedDeviceIndex_Hmd, ETrackedDeviceProperty.Prop_SecondsFromVsyncToPhotons_Float, ref error);
        float predicted_time = frame_period * 3 - last_vsync_time + vsync_to_photons;
        return predicted_time;
    }
}