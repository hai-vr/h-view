// MIT License
// 
// Copyright (c) 2024 CNLohr
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
// SOFTWARE.
/*
    Based on https://github.com/cnlohr/openvr-screengrab/blob/master/openvr-screengrab.c
    -----
    The contents of this class is based on "cnlohr/openvr-screengrab" on GitHub, which is 
    a C implementation that demonstrates the use of GetMirrorTextureD3D11.
*/

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Valve.VR;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using Color = System.Drawing.Color;

namespace Hai.HView.OVR;

/**
    Based on https://github.com/cnlohr/openvr-screengrab/blob/master/openvr-screengrab.c
    -----
    The contents of this class is based on "cnlohr/openvr-screengrab" on GitHub, which is
    a C implementation that demonstrates the use of GetMirrorTextureD3D11.

    This C# class below is a port by Haï of the "openvr-screengrab.c" file of that project,
    with some modifications, using Vortice to access the D3D11 API.
    Haï is not a graphics developer, so the specific implementation below should not be taken as authority.
    
    You should check the "openvr-screengrab.c" file instead if you're looking for a solid reference.
    
    Lines prefixed with //---// are from the original "openvr-screengrab.c" file.
*/
public class HVCapture : IDisposable
{
    private const EVREye WhichEye = EVREye.Eye_Right;
    
    // We diverge from "openvr-screengrab.c" here,
    // because I'm hitting a weird issue where the captured image remains the same.
    // Therefore, I'm not releasing the mirror texture, and I'm reusing the first received reference.
    // There are more comments about it inside the code below.
    private const bool ReuseMirrorTexture = true; // Would be false in "openvr-screengrab.c"
    private const bool ReleaseMirrorTextureEveryInvocation = false; // Would be true in "openvr-screengrab.c"
    
    public static byte[] TEMP_testdata;
    public static int TEMP_testdata_w;
    public static int TEMP_testdata_h;

    private ID3D11Device _device;
    private ID3D11DeviceContext _context;
    private bool _onlySubresource;
    private Box _srcBox;
    private int _inW;
    private int _inH;
    private IntPtr _pResourceView;
    private byte[] _holdingData;
    private ID3D11Texture2D _texture2DID;
    private int _texture2DID_W;
    private int _texture2DID_H;
    private Texture2DDescription _desc2d;
    private ID3D11Texture2D _resource;

    public bool TryStart()
    {
//---// Create a DirectX Device
//---// ID3D11Device* pDevice;
//---// ID3D11DeviceContext* pDeviceContext;
//---// enum D3D_FEATURE_LEVEL pFeatureLevels[1] =  {
//---//     D3D_FEATURE_LEVEL_11_0
//---// }
//---// ;
//---// enum D3D_FEATURE_LEVEL pOutFeatureLevels;
//---// HRESULT hr;
//---// hr = D3D11CreateDevice(
//---//     0,
//---//     D3D_DRIVER_TYPE_HARDWARE,
//---//     0,
//---//     0,
//---//     0,
//---//     0,
//---//     D3D11_SDK_VERSION,
//---//     &pDevice,
//---//     &pOutFeatureLevels,
//---//     &pDeviceContext);
        Result hr = D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.None,
            new []{ FeatureLevel.Level_11_0 },
            // https://github.com/amerkoleci/Vortice.Windows/blob/eded19beac7d0b80373b10253414fa5a233b5c43/src/samples/HelloDirect3D11/D3D11GraphicsDevice.cs#L105C1-L107C80
            out var tempDevice,
            out var tempContext
        );
        if (!hr.Success) return false;
        
        // If we don't do this, _device and _context may spontaneously become null
        _device = tempDevice.QueryInterface<ID3D11Device1>();
        _context = tempContext.QueryInterface<ID3D11DeviceContext1>();

        return true;
    }

    public void SetCopyOnlySubresource(int x, int y, int w, int h)
    {
//---// D3D11_BOX src_box = { 0, 0, 0, CAPTURE_W, CAPTURE_H, 1 };
        var box = new Box(x, y, 0, x + w, y + h, 1);
        _inW = w;
        _inH = h;
        _onlySubresource = true;
        _srcBox = box;
    }

    public void SetCopyAll()
    {
        _onlySubresource = false;
    }

    public bool DoCapture(out IntPtr image)
    {
//---// Get the Shader Resource View from OpenVR
//---// ID3D11ShaderResourceView* pD3D11ShaderResourceView;
//---// var ce = oCompositor.GetMirrorTextureD3D11(WhichEye, pDevice, (void**)&pD3D11ShaderResourceView);
//---// ID3D11ShaderResourceViewVtbl* srvVT = pD3D11ShaderResourceView.lpVtbl;
        if (_pResourceView == 0 || !ReuseMirrorTexture)
        {
            var ce = OpenVR.Compositor.GetMirrorTextureD3D11(WhichEye, _device.NativePointer, ref _pResourceView);
            if (ce != EVRCompositorError.None)
            {
                image = 0;
                return false;
            }
            var tResourceView = new ID3D11ShaderResourceView(_pResourceView);
            var resourceView = tResourceView.QueryInterface<ID3D11ShaderResourceView>();

//---// Get a Texture2D from that resource view.
//---// ID3D11Texture2D* resource = 0;
//---// srvVT.GetResource(resourceView, &resource);
//---// D3D11_TEXTURE2D_DESC desc2d;
//---// resource.lpVtbl.GetDesc(resource, &desc2d);
            _resource = resourceView.Resource.QueryInterface<ID3D11Texture2D>();
            _desc2d = _resource.Description;
        }

//---// ctsbuff += sprintf(ctsbuff, "Tex In: %d %d %d\n", desc2d.Width, desc2d.Height, desc2dFormat);
//---//
//---// // We have to generate our texture based off of the pixel format of the OpenVR Backing texture.
//---// if (texture2DID == 0)
//---// {
//---//     D3D11_TEXTURE2D_DESC texDesc;
//---//     ZeroMemory(&texDesc, sizeof(D3D11_TEXTURE2D_DESC));
//---//     printf("Allocating %d x %d / %d\n", desc2d.Width, desc2d.Height, desc2dFormat);
//---// #ifdef ONLY_COPY_SUBRESOURCE
//---//     texDesc.Width = CAPTURE_W;
//---//     texDesc.Height = CAPTURE_H;
//---// #else
//---//     texDesc.Width = desc2d.Width;
//---//     texDesc.Height = desc2d.Height;
//---// #endif
//---//     texDesc.Format = desc2dFormat;
//---//     texDesc.Usage = D3D11_USAGE_STAGING;
//---//     texDesc.SampleDesc.Count = 1;
//---//     texDesc.SampleDesc.Quality = 0;
//---//     texDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
//---//     texDesc.ArraySize = 1;
//---//     texDesc.BindFlags = 0;
//---//     texDesc.MiscFlags = D3D11_RESOURCE_MISC_SHARED;
//---//     texDesc.MipLevels = 1;
//---//     printf("Creating Texture %p\n", pDeviceFn.CreateTexture2D);
//---//     hr = pDeviceFn.CreateTexture2D(pDevice, &texDesc, NULL, &texture2DID);
//---//     printf("HR: %08x DEV: %08x\n", hr, texture2DID);
//---// }

        int CaptureW = _onlySubresource ? _inW : _desc2d.Width;
        int CaptureH = _onlySubresource ? _inH : _desc2d.Height;
        if (_texture2DID == null || _texture2DID_W != CaptureW || _texture2DID_H != CaptureH)
        {
            _texture2DID_W = CaptureW;
            _texture2DID_H = CaptureH;
            _texture2DID = _device.CreateTexture2D(
                width: CaptureW,
                height: CaptureH,
                format: _desc2d.Format,
                usage: ResourceUsage.Staging,
                cpuAccessFlags: CpuAccessFlags.Read,
                bindFlags: BindFlags.None,
                miscFlags: ResourceOptionFlags.Shared,
                mipLevels: 1
            );
        }
        
//---// Copy the OpenVR texture into our texture.
//---// int w = CAPTURE_W;
//---// int h = CAPTURE_H;
//---// #ifdef ONLY_COPY_SUBRESOURCE
//---//     D3D11_BOX src_box = { 0, 0, 0, CAPTURE_W, CAPTURE_H, 1 };
//---//     pDeviceContext->lpVtbl->CopySubresourceRegion( pDeviceContext, texture2DID, 0, 0, 0, 0, resource, 0, &src_box );
//---// #else
//---//     pDeviceContext->lpVtbl->CopyResource( pDeviceContext, texture2DID, resource );
//---// #endif
        int w = (int)_desc2d.Width;
        int h = (int)_desc2d.Height;

        if (_onlySubresource)
        {
            _context.CopySubresourceRegion(_texture2DID, 0, 0, 0, 0, _resource, 0, _srcBox);
        }
        else
        {
            _context.CopyResource(_resource, _texture2DID);
        }

//---// Release the texture back to OpenVR ASAP
//---// oCompositor->ReleaseMirrorTextureD3D11( pD3D11ShaderResourceView );
        // FIXME: If the following is called, this seems to cause an issue with the update???
        // I saw an issue on github (related to the mirror being exposed as a replacement) saying that release is
        // only needed for GL calls, but why would D3D11 release exist here then?
        if (ReleaseMirrorTextureEveryInvocation)
        {
            OpenVR.Compositor.ReleaseMirrorTextureD3D11(_pResourceView);
            _pResourceView = 0;
        }
        
//---// We can then process through that pixel data, after we map our texture that we copied into.
//---// D3D11_MAPPED_SUBRESOURCE mappedResource = { 0 };
//---// hr = pDeviceContext->lpVtbl->Map( pDeviceContext, texture2DID, 0, D3D11_MAP_READ, 0, &mappedResource );
//---// ctsbuff += sprintf( ctsbuff, "MAPPED: %p / HR: %08x (%d %d)\n", mappedResource.pData, hr, mappedResource.RowPitch, mappedResource.DepthPitch );
        var result = _context.Map(_texture2DID, 0, MapMode.Read, MapFlags.None, out MappedSubresource mappedResource);

        if (result.Success)
        {
            // FIXME: the source of H and W may be inconsistent here (based on onlySubresource)
            var desc2dHeight = CaptureH;
            var desc2dWidth = CaptureW;
            // Span<byte> data = mappedResource.AsSpan(desc2dHeight * desc2dWidth * 4);
            var neededDataSize = desc2dHeight * desc2dWidth * 4;
            if (_holdingData == null || _holdingData.Length != neededDataSize)
            {
                _holdingData = new byte[neededDataSize];
            }
            // Span<byte> data = mappedResource.AsSpan(desc2dHeight * desc2dWidth * 4);
            Marshal.Copy(mappedResource.DataPointer, _holdingData, 0, _holdingData.Length);
            
            TEMP_testdata = _holdingData;
            TEMP_testdata_w = CaptureW;
            TEMP_testdata_h = CaptureH;
            image = 0;
            
//---// int x, y;
//---// int z;
//---// uint8_t * dataptr = mappedResource.pData;
//---// if( mappedResource.pData )
//---// {
//---//     for( y = 0; y < h && y < desc2d.Height; y++ )
//---//     {
//---//         int mw = (w<desc2d.Height)?w:desc2d.Height;
//---//         for( x = 0; x < mw; x++ )
//---//         {
//---//             rbuff[x+y*CAPTURE_W] = rgb_endian_flip( ((uint32_t*)( ((uint8_t*)mappedResource.pData) + mappedResource.RowPitch * y ))[x] );
//---//         }
//---//     }
//---// }
            if (false)
            {
                // It would be better to use BitmapEncoder or any other fast library to export to PNG.
                //
                // Just leaving the code here to have a working example of array access, but this is so slow
                // it should never be used outside of debugging.
                
                var bitmap = new Bitmap(CaptureW, CaptureH);
                for (int y = 0; y < h && y < desc2dHeight; y++)
                {
                    int mw = (w < desc2dHeight) ? w : desc2dHeight;
                    for (int x = 0; x < mw; x++)
                    {
                        var yy = mappedResource.RowPitch * y;
                        byte b0 = _holdingData[yy + x * 4];
                        byte b1 = _holdingData[yy + x * 4 + 1];
                        byte b2 = _holdingData[yy + x * 4 + 2];
                        byte b3 = _holdingData[yy + x * 4 + 3];
                        var color = Color.FromArgb(255, b0, b1, b2);
                        bitmap.SetPixel(x, y, color);
                        if ((x + yy) % 100_000 == 0)
                        {
                            Console.WriteLine($"({x},{y}) {b0} {b1} {b2} {b3}");
                        }
                    }
                }
                bitmap.Save("output.png", ImageFormat.Png);
            }
        }
        else
        {
            image = 0;
        }
        
//---// Cleanup
//---// pDeviceContext->lpVtbl->Unmap( pDeviceContext, texture2DID, 0 );
//---// resource->lpVtbl->Release( resource );
        _context.Unmap(_texture2DID, 0);
        if (!ReuseMirrorTexture)
        {
            _resource.Release();
            _pResourceView = 0;
        }
        
        return result.Success;
    }

    public void Dispose()
    {
        if (_pResourceView != 0)
        {
            OpenVR.Compositor.ReleaseMirrorTextureD3D11(_pResourceView);
        }
        _context.Release();
        _device.Release();
    }
}