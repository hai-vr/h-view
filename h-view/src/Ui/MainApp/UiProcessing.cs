using System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using Hai.HView.Data;
using Hai.HView.OVR;
using Hai.HView.Rendering;
using ImGuiNET;
#if INCLUDES_OCR
using Windows.Media.Ocr;
using Hai.HView.OCR;
#endif

namespace Hai.HView.Ui.MainApp;

internal class UiProcessing
{
    private readonly ImGuiVRCore VrGui;
    private readonly HVRoutine _routine;
    private readonly SavedData _config;
    private readonly UiHardware _hardwareTab;
    private readonly HVImageLoader _imageLoader;
    private HVCapture _captureLateInit;
    
    private IntPtr _ourResultZeroable;
#if INCLUDES_OCR
    private static OcrResult _ocr;
#endif
    private readonly Stopwatch _time;
    private bool _wasDown;
    private static bool isProcessingTabOpen;
    private bool _tt = false;
    private bool _processing;

    public UiProcessing(ImGuiVRCore imGuiVr, HVRoutine routine, SavedData config, UiHardware hardwareTab, HVImageLoader imageLoader)
    {
        VrGui = imGuiVr;
        _routine = routine;
        _config = config;
        _hardwareTab = hardwareTab;
        _imageLoader = imageLoader;

        _time = new Stopwatch();
        _time.Start();
    }

    public void ProcessingTab()
    {
        isProcessingTabOpen = true;
        EnsureInitialized();

        if (_routine.IsOpenVrAvailable())
        {
            var isInteractDown = _routine.IsInteractDown();
            if (VrGui.HapticButton(HLocalizationPhrase.CaptureLabel, new Vector2(300, 40))
                || isInteractDown != _wasDown && isInteractDown
                || !_processing && _time.ElapsedMilliseconds > 50)
            {
                _processing = true;

                try
                {
                    // TODO: Only capture eye region
                    _captureLateInit.SetCopyOnlySubresource(256, 512, 2048, 2048);
                    if (_captureLateInit.DoCapture(out IntPtr result))
                    {
                        // _ourResultZeroable = result;
                        // _imageLoader.FreeImagesFromMemory();
                        try
                        {
                            // _ourResultZeroable = _imageLoader.GetOrLoadImage("output2.png");
                        }
                        catch (Exception e)
                        {
                        }

                        ExecuteOCRAsync();
                    }
                }
                finally
                {
                    _processing = false;
                    _time.Restart();
                }
            }
ImGui.SameLine();
            ImGui.Checkbox("Language test", ref _tt);
            _wasDown = isInteractDown;
        }
        else
        {
            _hardwareTab.OpenVrUnavailableBlinker();
        }

#if INCLUDES_OCR
        if (_ocr != null)
        {
            var posX = ImGui.GetCursorPosX();
            var posY = ImGui.GetCursorPosY();
            var avail = ImGui.GetContentRegionAvail();
            
            if (_ourResultZeroable != 0)
            {
                ImGui.Image(_ourResultZeroable, new Vector2(avail.X, avail.Y), new Vector2(0, 0), new Vector2(1, 1), new Vector4(1, 1, 1, 0.2f));
                ImGui.SetCursorPosX(posX);
                ImGui.SetCursorPosX(posY);
            }
            
            var borderX = 0;
            var borderY = 0;
            
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 0.2f));
            foreach (var line in _ocr.Lines)
            {
                var text = line.Text;
                
                FindMinMaxes(line, out var minX, out var minY, out var maxX, out var maxY);
                SetCursorToPrintText(minX, minY, maxX, maxY, posX, posY, borderX, borderY, text, avail, 0f);
                ImGui.Text(text);
                // if (VrGui.HapticButton(text))
                {
                    
                }
            }
            ImGui.PopStyleColor();
            
            foreach (var lineWord in _ocr.Lines.SelectMany(line => line.Words))
            {
                var text = lineWord.Text;
                
                var rect = lineWord.BoundingRect;
                var minX = (float)rect.X;
                var minY = (float)rect.Y;
                var maxX = (float)(rect.X + rect.Width);
                var maxY = (float)(rect.Y + rect.Height);
                SetCursorToPrintText(minX, minY, maxX, maxY, posX, posY, borderX, borderY, text, avail, 0.25f);
                ImGui.Text(text);
            }
        }
#endif
    }

    private static void SetCursorToPrintText(float minX, float minY, float maxX, float maxY, float posX, float posY, int borderX, int borderY, string text, Vector2 avail, float verticalMul)
    {
        var centerX = minX + (maxX - minX) * 0.5f;
        var centerY = minY + (maxY - minY) * 0.5f;
        var textSize = ImGui.CalcTextSize(text);
        var centerXX = posX + borderX + (avail.X - borderX * 2) * (centerX / 2048f);
        var centerYY = posY + borderY + (avail.Y - borderY * 2) * (centerY / 2048f);
        ImGui.SetCursorPosX(centerXX - textSize.X * 0.5f);
        ImGui.SetCursorPosY(centerYY - textSize.Y * 0.5f + textSize.Y * verticalMul);
    }

    private static void FindMinMaxes(OcrLine line, out float minX, out float minY, out float maxX, out float maxY)
    {
        minX = float.MaxValue;
        minY = float.MaxValue;
        maxX = float.MinValue;
        maxY = float.MinValue;
        foreach (var lineWord in line.Words)
        {
            var rect = lineWord.BoundingRect;
            var xx = (float)rect.X;
            var yy = (float)rect.Y;
            var xxp = (float)(rect.X + rect.Width);
            var yyp = (float)(rect.Y + rect.Height);
            if (xx < minX) minX = xx;
            if (yy < minY) minY = yy;
            if (xxp > maxX) maxX = xxp;
            if (yyp > maxY) maxY = yyp;
        }
    }

    private void ExecuteOCRAsync()
    {
#if INCLUDES_OCR
        Task.Run(async () =>
        {
            var result = await HVOcr.GenericOcr(HVOcr.OpenBytes(HVCapture.TEMP_testdata, HVCapture.TEMP_testdata_w, HVCapture.TEMP_testdata_h), _tt ? "ja-JP" : "en-US");
            _ocr = result;
        });
#endif
    }

    private void EnsureInitialized()
    {
        if (_captureLateInit == null)
        {
            _captureLateInit = new HVCapture(_imageLoader);
            _captureLateInit.Start();
        }
    }
}