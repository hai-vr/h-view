using System.Text.RegularExpressions;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Hai.HView.OCR;

public class HVOcr
{
    private static readonly Regex LobbyRegexPatternCapture = new Regex("HV-([0-9]{4})-([0-9]{4})");
    private static Dictionary<string, OcrEngine> langToEngine = new Dictionary<string, OcrEngine>();
    
    public const string InvariantCultureLanguage = "en-US";

    public static async Task<OcrResult> GenericOcr(SoftwareBitmap bitmap, string language)
    {
        // await DebugDumpBitmapToFile(bitmap);
        
        // # Adding more languages:
        // - Windows > Language settings
        // - Preferred Languages > Add a language
        // - Add "Japanese" or another language
        // - Uncheck all "Optional language features (!!!)
        //   - (Required language features should have OCR in it)
        // - Install
        EnsureInitialized(language);
        var ocrResult = await langToEngine[language].RecognizeAsync(bitmap);
        
        return ocrResult;
    }

    public static async Task<string[]> FindLobbyCodesInScreenshot(SoftwareBitmap bitmap)
    {
        // https://stackoverflow.com/a/73515937
        EnsureInitialized(InvariantCultureLanguage);
        var ocrResult = await langToEngine[InvariantCultureLanguage].RecognizeAsync(bitmap);
        
        var matches = ocrResult.Lines
            .SelectMany(line => LobbyRegexPatternCapture.Matches(line.Text).Select(match => match.Captures[0].Value))
            .ToArray();
        return matches;
    }

    private static void EnsureInitialized(string language)
    {
        if (!langToEngine.ContainsKey(language))
        {
            var lang = new Windows.Globalization.Language(language);
            var ocr = OcrEngine.TryCreateFromLanguage(lang);
            langToEngine[language] = ocr;
        }
    }

    public static async Task<SoftwareBitmap> OpenFile(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open);
        using var asRandomAccessStream = stream.AsRandomAccessStream();
        
        var bitmap = await BitmapDecoder.CreateAsync(asRandomAccessStream);
        return await bitmap.GetSoftwareBitmapAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight);
    }

    public static SoftwareBitmap BitmapFromBytes(byte[] bytes, int width, int height)
    {
        var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba8, width, height);
        softwareBitmap.CopyFromBuffer(bytes.AsBuffer());

        // return await OpenFile("output2.png");
        return softwareBitmap;
    }

    private static async Task DebugDumpBitmapToFile(SoftwareBitmap softwareBitmap)
    {
        try
        {
            // output2.png needs to already exist (???)
            var fileFromPathAsync = await StorageFile.GetFileFromPathAsync(Path.GetFullPath("output2.png"));
            using (IRandomAccessStream fileStream = await fileFromPathAsync.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                encoder.SetSoftwareBitmap(softwareBitmap);
                try
                {
                    await encoder.FlushAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static async Task RunTestCode()
    {
        var bitmap = await OpenFile("test.png");
        var codes = await FindLobbyCodesInScreenshot(bitmap);
        foreach (var code in codes)
        {
            Console.WriteLine(code);
        }
    }
}