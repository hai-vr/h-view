using System.Text.RegularExpressions;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Hai.HView.OCR;

public class HVOcr
{
    private static readonly Regex LobbyRegexPatternCapture = new Regex("HV-([0-9]{4})-([0-9]{4})");

    public static async Task<string[]> FindLobbyCodesInScreenshot(SoftwareBitmap bitmap)
    {
        // https://stackoverflow.com/a/73515937
        var lang = new Windows.Globalization.Language("en-US");
        var ocr = OcrEngine.TryCreateFromLanguage(lang);
        var ocrResult = await ocr.RecognizeAsync(bitmap);
        
        var matches = ocrResult.Lines
            .SelectMany(line => LobbyRegexPatternCapture.Matches(line.Text)
                .Select(match => match.Captures[0].Value)
                .ToArray())
            .ToArray();
        return matches;
    }

    public static async Task<SoftwareBitmap> OpenFile(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open);
        
        var bitmap = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
        return await bitmap.GetSoftwareBitmapAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight);
    }

    public static async Task RunTestCode()
    {
        var bitmap = await OpenFile(@"test.png");
        var codes = await FindLobbyCodesInScreenshot(bitmap);
        foreach (var code in codes)
        {
            Console.WriteLine(code);
        }
    }
}