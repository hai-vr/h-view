using System.Diagnostics;
using Hai.HView.OVR;
#if INCLUDES_OCR
using Windows.Media.Ocr;
using Hai.HView.OCR;
#endif

namespace Hai.HView;

public class HVCaptureModule
{
    public const string EnglishLanguage = "en-US";
    public const string JapaneseLanguage = "ja-JP";
    
    public bool IsProcessing { get; private set; }
    public bool IsCaptureAvailable { get; private set; }
    
    private HVCapture _captureLateInit;
    private bool _captureRequiredAtLeastOnce;
    private Stopwatch _lastCaptureRequired = new Stopwatch();
    private string _language;
#if INCLUDES_OCR
    public OcrResult OcrResultNullable { get; private set; }
#endif

    public HVCaptureModule()
    {
        _lastCaptureRequired.Start();
    }

    public void EnsureInitialized()
    {
        if (_captureLateInit == null)
        {
            _captureLateInit = new HVCapture();
            IsCaptureAvailable = _captureLateInit.TryStart();
        }
    }

    public void TryCapture(Action doneCallback)
    {
        if (IsProcessing) return;
        IsProcessing = true;

        EnsureInitialized();
        _captureLateInit.SetCopyOnlySubresource(256, 512, 2048, 2048);
        if (_captureLateInit.DoCapture(out IntPtr result))
        {
            ExecuteOCRAsync();
        }
        doneCallback();
        IsProcessing = false;
    }

    private void ExecuteOCRAsync()
    {
#if INCLUDES_OCR
        Task.Run(async () =>
        {
            var bitmap = HVOcr.BitmapFromBytes(HVCapture.TEMP_testdata, HVCapture.TEMP_testdata_w, HVCapture.TEMP_testdata_h);
            var result = await HVOcr.GenericOcr(bitmap, _language);
            OcrResultNullable = result;
        });
#endif
    }

    public bool IsWarranted()
    {
        return _captureRequiredAtLeastOnce && _lastCaptureRequired.ElapsedMilliseconds < 10_000;
    }

    public void RequireCapture()
    {
        _captureRequiredAtLeastOnce = true;
        _lastCaptureRequired.Restart();
    }

    public void SetLanguage(string language)
    {
        _language = language;
    }
}