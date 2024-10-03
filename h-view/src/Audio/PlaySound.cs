namespace Hai.HView.Audio;

public class PlaySound
{
    private readonly string _file;
    private System.Media.SoundPlayer _media;

    public PlaySound(string file)
    {
        _media = new System.Media.SoundPlayer(file);
    }

    public void Play()
    {
        _media.Stop();
        _media.Play();
    }
}