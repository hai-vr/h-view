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
*/

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Hai.HView.Rendering;
using SharpGen.Runtime;
using Valve.VR;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using Color = System.Drawing.Color;

namespace Hai.HView.OVR;

public class HVCapture : IDisposable
{
    private const EVREye WhichEye = EVREye.Eye_Right;
    
    private readonly HVImageLoader _imageLoader;
    
    private ID3D11Device _device;
    private ID3D11DeviceContext _context;
    private bool _onlySubresource;
    private Box _srcBox;
    private int _inW;
    private int _inH;
    public static byte[] TEMP_testdata;
    public static int TEMP_testdata_w;
    public static int TEMP_testdata_h;

    public HVCapture(HVImageLoader imageLoader)
    {
        _imageLoader = imageLoader;
    }

    public void Start()
    {
        // Create a DirectX Device
        // ID3D11Device* pDevice;
        // ID3D11DeviceContext* pDeviceContext;
        // enum D3D_FEATURE_LEVEL pFeatureLevels[1] =  {
        //     D3D_FEATURE_LEVEL_11_0
        // }
        // ;
        // enum D3D_FEATURE_LEVEL pOutFeatureLevels;
        // HRESULT hr;
        // hr = D3D11CreateDevice(
        //     0,
        //     D3D_DRIVER_TYPE_HARDWARE,
        //     0,
        //     0,
        //     0,
        //     0,
        //     D3D11_SDK_VERSION,
        //     &pDevice,
        //     &pOutFeatureLevels,
        //     &pDeviceContext);
        Result hr = D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.None,
            new []{ FeatureLevel.Level_11_0 },
            // https://github.com/amerkoleci/Vortice.Windows/blob/eded19beac7d0b80373b10253414fa5a233b5c43/src/samples/HelloDirect3D11/D3D11GraphicsDevice.cs#L105C1-L107C80
            out var tempDevice,
            out var tempContext
        );
        _device = tempDevice.QueryInterface<ID3D11Device1>();
        _context = tempContext.QueryInterface<ID3D11DeviceContext1>();
    }

    public void SetCopyOnlySubresource(int x, int y, int w, int h)
    {
        var box = new Box(x, y, 0, x + w, y + h, 1);
        _inW = w;
        _inH = h;
        _onlySubresource = true;
        _srcBox = box;
        // var box = new Box(0, 0, 0, 512, 512, 1);
        // _inW = 512;
        // _inH = 512;
        // _onlySubresource = true;
        // _srcBox = box;
    }

    public void SetCopyAll()
    {
        _onlySubresource = false;
    }

    public bool DoCapture(out IntPtr image)
    {
        // // Create a DirectX Device
        // ID3D11Device * pDevice;
        // ID3D11DeviceContext * pDeviceContext;
        // enum D3D_FEATURE_LEVEL pFeatureLevels[1] = { D3D_FEATURE_LEVEL_11_0 };
        // enum D3D_FEATURE_LEVEL pOutFeatureLevels;
        // HRESULT hr;
        // hr = D3D11CreateDevice(
        //     0,
        //     D3D_DRIVER_TYPE_HARDWARE,
        //     0,
        //     0,
        //     0,
        //     0,
        //     D3D11_SDK_VERSION,
        //     &pDevice,
        //     &pOutFeatureLevels,
        //     &pDeviceContext );

        // Get the Shader Resource View from OpenVR
        // ID3D11ShaderResourceView* pD3D11ShaderResourceView;
        // var ce = oCompositor.GetMirrorTextureD3D11(WhichEye, pDevice, (void**)&pD3D11ShaderResourceView);
        // ID3D11ShaderResourceViewVtbl* srvVT = pD3D11ShaderResourceView.lpVtbl;
        IntPtr pResourceView = 0;
        EVRCompositorError ce = OpenVR.Compositor.GetMirrorTextureD3D11(WhichEye, _device.NativePointer, ref pResourceView);
        var tResourceView = new ID3D11ShaderResourceView(pResourceView);
        var resourceView = tResourceView.QueryInterface<ID3D11ShaderResourceView>();

        // Get a Texture2D from that resource view.
        // ID3D11Texture2D* resource = 0;
        // srvVT.GetResource(resourceView, &resource);
        // D3D11_TEXTURE2D_DESC desc2d;
        // resource.lpVtbl.GetDesc(resource, &desc2d);
        ID3D11Texture2D resource = resourceView.Resource.QueryInterface<ID3D11Texture2D>();
        Texture2DDescription desc2d = resource.Description;

//         ctsbuff += sprintf(ctsbuff, "Tex In: %d %d %d\n", desc2d.Width, desc2d.Height, desc2dFormat);
//
//         // We have to generate our texture based off of the pixel format of the OpenVR Backing texture.
//         if (texture2DID == 0)
//         {
//             D3D11_TEXTURE2D_DESC texDesc;
//             ZeroMemory(&texDesc, sizeof(D3D11_TEXTURE2D_DESC));
//             printf("Allocating %d x %d / %d\n", desc2d.Width, desc2d.Height, desc2dFormat);
// #ifdef ONLY_COPY_SUBRESOURCE
//             texDesc.Width = CAPTURE_W;
//             texDesc.Height = CAPTURE_H;
// #else
//             texDesc.Width = desc2d.Width;
//             texDesc.Height = desc2d.Height;
// #endif
//             texDesc.Format = desc2dFormat;
//             texDesc.Usage = D3D11_USAGE_STAGING;
//             texDesc.SampleDesc.Count = 1;
//             texDesc.SampleDesc.Quality = 0;
//             texDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
//             texDesc.ArraySize = 1;
//             texDesc.BindFlags = 0;
//             texDesc.MiscFlags = D3D11_RESOURCE_MISC_SHARED;
//             texDesc.MipLevels = 1;
//             printf("Creating Texture %p\n", pDeviceFn.CreateTexture2D);
//             hr = pDeviceFn.CreateTexture2D(pDevice, &texDesc, NULL, &texture2DID);
//             printf("HR: %08x DEV: %08x\n", hr, texture2DID);
//         }

        int CaptureW = _inW;
        int CaptureH = _inH;
        ID3D11Texture2D texture2DID = _device.CreateTexture2D(
            width: CaptureW,
            height: CaptureH,
            format: desc2d.Format,
            usage: ResourceUsage.Staging,
            cpuAccessFlags: CpuAccessFlags.Read,
            bindFlags: BindFlags.None,
            miscFlags: ResourceOptionFlags.Shared,
            mipLevels: 1
        );
        
// Copy the OpenVR texture into our texture.
//         int w = CAPTURE_W;
//         int h = CAPTURE_H;
// #ifdef ONLY_COPY_SUBRESOURCE
//         D3D11_BOX src_box = { 0, 0, 0, CAPTURE_W, CAPTURE_H, 1 };
//         pDeviceContext->lpVtbl->CopySubresourceRegion( pDeviceContext, texture2DID, 0, 0, 0, 0, resource, 0, &src_box );
// #else
//         pDeviceContext->lpVtbl->CopyResource( pDeviceContext, texture2DID, resource );
// #endif
        // Copy the OpenVR texture into our texture.
        // int w = (int)CaptureW;
        // int h = (int)CaptureH;
        int w = (int)desc2d.Width;
        int h = (int)desc2d.Height;

        if (_onlySubresource)
        {
            _context.CopySubresourceRegion(texture2DID, 0, 0, 0, 0, resource, 0, _srcBox);
        }
        else
        {
            _context.CopyResource(resource, texture2DID);
        }

        // Release the texture back to OpenVR ASAP
        // oCompositor->ReleaseMirrorTextureD3D11( pD3D11ShaderResourceView );
        // FIXME: If the following is called, this seems to cause an issue with the update??? I saw an issue on github (related to the mirror being exposed as a replacement) saying that release is only needed for GL calls, but why would D3D11 release exist here then?
        OpenVR.Compositor.ReleaseMirrorTextureD3D11(pResourceView);
        
        // We can then process through that pixel data, after we map our texture that we copied into.
        // D3D11_MAPPED_SUBRESOURCE mappedResource = { 0 };
        // hr = pDeviceContext->lpVtbl->Map( pDeviceContext, texture2DID, 0, D3D11_MAP_READ, 0, &mappedResource );
        // ctsbuff += sprintf( ctsbuff, "MAPPED: %p / HR: %08x (%d %d)\n", mappedResource.pData, hr, mappedResource.RowPitch, mappedResource.DepthPitch );
        // int x, y;
        // int z;
        // uint8_t * dataptr = mappedResource.pData;
        // if( mappedResource.pData )
        // {
        //     for( y = 0; y < h && y < desc2d.Height; y++ )
        //     {
        //         int mw = (w<desc2d.Height)?w:desc2d.Height;
        //         for( x = 0; x < mw; x++ )
        //         {
        //             rbuff[x+y*CAPTURE_W] = rgb_endian_flip( ((uint32_t*)( ((uint8_t*)mappedResource.pData) + mappedResource.RowPitch * y ))[x] );
        //         }
        //     }
        // }
        var result = _context.Map(texture2DID, 0, MapMode.Read, MapFlags.None, out MappedSubresource mappedResource);
        
        
        
        var desc2dHeight = CaptureH;
        var desc2dWidth = CaptureW;
        // Span<byte> data = mappedResource.AsSpan(desc2dHeight * desc2dWidth * 4);
        byte[] data = new byte[desc2dHeight * desc2dWidth * 4]; // FIXME: this wastes an array every time
        Marshal.Copy(mappedResource.DataPointer, data, 0, data.Length);

        if (result.Success)
        {
            var lmao = false;
            if (lmao)
            {
                var bitmap = new Bitmap(CaptureW, CaptureH);
            
                // var tex = new ImageSharpTexture(memoryStream, false, false);
                // image = _imageLoader.Allocate(tex);
                for (int y = 0; y < h && y < desc2dHeight; y++)
                {
                    int mw = (w < desc2dHeight) ? w : desc2dHeight;
                    for (int x = 0; x < mw; x++)
                    {
                        var yy = mappedResource.RowPitch * y;
                        byte b0 = data[yy + x * 4];
                        byte b1 = data[yy + x * 4 + 1];
                        byte b2 = data[yy + x * 4 + 2];
                        byte b3 = data[yy + x * 4 + 3];
                        var color = Color.FromArgb(255, b0, b1, b2);
                        bitmap.SetPixel(x, y, color);
                        if ((x + yy) % 100_000 == 0)
                        {
                            Console.WriteLine($"({x},{y}) {b0} {b1} {b2} {b3}");
                        }
                        // rbuff[x + y * desc2dWidth] = rgb_endian_flip(((uint32_t*)(((uint8_t*)mappedResource.pData) + yy))[x]);
                    }
                }

                image = 0;
                bitmap.Save("output.png", ImageFormat.Png);
                TEMP_testdata = data;
                TEMP_testdata_w = CaptureW;
                TEMP_testdata_h = CaptureH;
            }
            else
            {
                TEMP_testdata = data;
                TEMP_testdata_w = CaptureW;
                TEMP_testdata_h = CaptureH;
                image = 0;
                // using var mem = new MemoryStream(data);
                // var bitmap = Bitmap.FromStream(mem);
                // bitmap.Save("output.png", ImageFormat.Png);
            }
        }
        else
        {
            image = 0;
        }
        
        // Cleanup
        // pDeviceContext->lpVtbl->Unmap( pDeviceContext, texture2DID, 0 );
        // resource->lpVtbl->Release( resource );
        _context.Unmap(texture2DID, 0);
        resource.Release();
        
        return result.Success;
    }

    public void Dispose()
    {
        _context.Release();
        _device.Release();
    }
}