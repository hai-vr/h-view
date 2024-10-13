using System.Diagnostics;
using System.Numerics;
using Hai.HView.Core;
using ImGuiNET;
#if INCLUDES_OCR
using Windows.Media.Ocr;
#endif

namespace Hai.HView.Ui.MainApp;

internal class UiProcessing
{
    private const string InternalErrorFailedD3D11CreateDevice = "Failed to create D3D11 device";
    private readonly ImGuiVRCore VrGui;
    private readonly HVRoutine _routine;
    private readonly HVCaptureModule _captureModule;
    
    private IntPtr _pictureToShowZeroable;
    private readonly Stopwatch _time;
    private bool _main;
    private bool _continuousCapture;
    private static bool _detectJapanese;

    public UiProcessing(ImGuiVRCore imGuiVr, HVRoutine routine)
    {
        VrGui = imGuiVr;
        _routine = routine;
        _captureModule = _routine.CaptureModule;
        ApplyLanguage();

        _time = new Stopwatch();
        _time.Start();
    }

    public void ProcessingTab()
    {
        _captureModule.EnsureInitialized();

        if (!_captureModule.IsCaptureAvailable)
        {
            ImGui.TextWrapped(InternalErrorFailedD3D11CreateDevice);
        }
        else if (_routine.IsOpenVrAvailable())
        {
            _captureModule.RequireCapture();
            if (VrGui.HapticButton(HLocalizationPhrase.ScanImageLabel, new Vector2(300, 40))
                || _continuousCapture && !_captureModule.IsProcessing && _time.ElapsedMilliseconds > 200)
            {
                // TODO: We should run that in the OVR thread.
                _captureModule.TryCapture(() => _time.Restart());
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("Detect Japanese", ref _detectJapanese))
            {
                ApplyLanguage();
            }
            ImGui.SameLine();
            ImGui.Checkbox("Show individual words", ref _main);
            ImGui.SameLine();
            ImGui.Checkbox("Continuous capture", ref _continuousCapture);
        }
        else
        {
            UiMainApplication.OpenVrUnavailableBlinker(_time);
        }

#if INCLUDES_OCR
        var ocrResult = _captureModule.OcrResultNullable;
        if (ocrResult != null)
        {
            var posX = ImGui.GetCursorPosX();
            var posY = ImGui.GetCursorPosY();
            var avail = ImGui.GetContentRegionAvail();
            
            if (_pictureToShowZeroable != 0)
            {
                ImGui.Image(_pictureToShowZeroable, new Vector2(avail.X, avail.Y), new Vector2(0, 0), new Vector2(1, 1), new Vector4(1, 1, 1, 0.2f));
                ImGui.SetCursorPosX(posX);
                ImGui.SetCursorPosX(posY);
            }
            
            var borderX = 0;
            var borderY = 0;
            
            if (_main) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 0.2f));
            foreach (var line in ocrResult.Lines)
            {
                var text = line.Text;
                
                FindMinMaxes(line, out var minX, out var minY, out var maxX, out var maxY);
                SetCursorToPrintText(minX, minY, maxX, maxY, posX, posY, borderX, borderY, text, avail, 0f);
                ImGui.Text(text);
            }
            if (_main) ImGui.PopStyleColor();
            
            if (!_main) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 0.2f));
            foreach (var lineWord in ocrResult.Lines.SelectMany(line => line.Words))
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
            if (!_main) ImGui.PopStyleColor();
        }
#endif
    }

    private void ApplyLanguage()
    {
        _captureModule.SetLanguage(_detectJapanese ? HVCaptureModule.JapaneseLanguage : HVCaptureModule.EnglishLanguage);
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

#if INCLUDES_OCR
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
#endif
}