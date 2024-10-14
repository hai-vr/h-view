using System.Numerics;
using Raylib_cs;

new HVRaylib().Run();

class HVRaylib
{
    private const bool ShowRenderTextureInWindow = true;

    private readonly Vector3 _initCamPos = new(2, 2, 2);
    
    private Camera3D _cam;
    private RenderTexture2D _rt;

    public void Run()
    {
        Raylib.InitWindow(500, 500, "Hello World");
        unsafe
        {
            var windowHandle = Raylib.GetWindowHandle();
            // TODO: Use the window handle to make the window invisible.
        }

        _cam = new Camera3D();
        _cam.Position = _initCamPos;
        _cam.Target = Vector3.Zero;
        _cam.Up = Vector3.UnitY;
        _cam.FovY = 45f;
        _cam.Projection = CameraProjection.Perspective;
        // TODO: How to create oblique projection matrices in order to be able to
        // render flat overlay textures that are an oblique projection of what's behind it, from each eye? 
        
        _rt = Raylib.LoadRenderTexture(256, 256);
        Console.WriteLine($"_rt FBO ID = {_rt.Id}");
        
        // TODO:
        // - Get OpenGL texture pointer, or copy texture to something we own
        // - Pass it to OpenVR overlay

        while (!Raylib.WindowShouldClose())
        {
            InnerLoop();
        }

        Raylib.CloseWindow();
    }

    private void InnerLoop()
    {
        Raylib.BeginTextureMode(_rt);
        Raylib.ClearBackground(new Color(0, 0, 0, 0));

        var time = Raylib.GetTime();
        var rot = Matrix4x4.CreateRotationY((float)time * MathF.PI);
        var postRot = Matrix4x4.CreateTranslation(_initCamPos) * rot;
        _cam.Position = postRot.Translation;
        
        Raylib.BeginMode3D(_cam);
        RenderScene();
        Raylib.EndMode3D();
        Raylib.EndTextureMode();

        if (ShowRenderTextureInWindow)
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(0, 255, 255, 255));
            Raylib.DrawTexture(_rt.Texture, 0, 0, Color.White);
            Raylib.EndDrawing();
        }
        
        // TODO: Signal OpenVR to update the overlay texture
    }

    private static void RenderScene()
    {
        Raylib.DrawCube(Vector3.Zero, 1, 1, 1, Color.Red);
    }
}