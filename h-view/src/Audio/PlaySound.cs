using System.Runtime.InteropServices;

namespace Hai.HView.Audio;

public class PlaySound
{
    private readonly System.Media.SoundPlayer _media;
    private readonly bool _isPlayable;

    public PlaySound(string file)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _media = new System.Media.SoundPlayer(file);
            _isPlayable = true;
        }
    }

    public void Play()
    {
        if (_isPlayable)
        {
#pragma warning disable CA1416
            _media.Stop();
            _media.Play();
#pragma warning restore CA1416
        }
    }
}