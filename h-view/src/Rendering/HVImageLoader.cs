using ImGuiNET;
using Veldrid;
using Veldrid.ImageSharp;

namespace Hai.HView.Rendering;

public class HVImageLoader
{
    private readonly Dictionary<int, IntPtr> _indexToPointers = new Dictionary<int, IntPtr>();
    private readonly List<Texture> _loadedTextures = new List<Texture>();
    private readonly Dictionary<int, ImageSharpTexture> _indexToTexture = new Dictionary<int, ImageSharpTexture>();
    private readonly Dictionary<string, IntPtr> _pathToPointers = new Dictionary<string, IntPtr>();
    private readonly Dictionary<string, ImageSharpTexture> _pathToTexture = new Dictionary<string, ImageSharpTexture>();
    
    private GraphicsDevice _gd;
    private CustomImGuiController _controller;

    internal IntPtr GetOrLoadImage(string[] icons, int index)
    {
        // TODO: Should we pre-load all the icons immediately, instead of doing it on request?
        if (_indexToPointers.TryGetValue(index, out var found)) return found;
        
        if (index == -1) return 0;
        if (index >= icons.Length) return 0;
        var base64png = icons[index];
        
        var pngBytes = Convert.FromBase64String(base64png);
        using (var stream = new MemoryStream(pngBytes))
        {
            var pointer = LoadTextureFromStream(stream, out var tex);
            _indexToPointers.Add(index, pointer);
            _indexToTexture.Add(index, tex);
            return pointer;
        }
    }

    internal IntPtr GetOrLoadImage(string path)
    {
        if (_pathToPointers.TryGetValue(path, out var found)) return found;

        using (var stream = new FileStream(path, FileMode.Open))
        {
            var pointer = LoadTextureFromStream(stream, out var tex);
            _pathToPointers.Add(path, pointer);
            _pathToTexture.Add(path, tex);
            return pointer;
        }
    }

    private IntPtr LoadTextureFromStream(Stream stream, out ImageSharpTexture texture)
    {
        // https://github.com/ImGuiNET/ImGui.NET/issues/141#issuecomment-905927496
        texture = new ImageSharpTexture(stream, true);
        return LoadFromImageSharp(texture);
    }

    private IntPtr LoadFromImageSharp(ImageSharpTexture img)
    {
        var deviceTexture = img.CreateDeviceTexture(_gd, _gd.ResourceFactory);
        _loadedTextures.Add(deviceTexture);
        var pointer = _controller.GetOrCreateImGuiBinding(_gd.ResourceFactory, deviceTexture);
        return pointer;
    }

    /// Free allocated images. This needs to be called from the UI thread.
    public void FreeImagesFromMemory()
    {
        // TODO: This may still leak within the custom ImGui controller.
        Console.WriteLine("Freeing images from memory");
        foreach (var loadedTexture in _loadedTextures)
        {
            loadedTexture.Dispose();
        }
        _loadedTextures.Clear();
        _indexToPointers.Clear();
        _indexToTexture.Clear();
        // TODO: Don't free avatar pictures that were loaded from disk.
        _pathToPointers.Clear();
        _pathToTexture.Clear();
    }

    public void Provide(GraphicsDevice gd, CustomImGuiController controller)
    {
        _gd = gd;
        _controller = controller;
    }
}