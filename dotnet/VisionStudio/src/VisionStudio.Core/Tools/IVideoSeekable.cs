namespace VisionStudio.Core.Tools;

/// <summary>
/// Exposes timeline seeking, scrub, and playback restart controls for video stream tools.
/// </summary>
public interface IVideoSeekable
{
    int CurrentFrame { get; }
    int TotalFrames { get; }
    double Fps { get; }
    bool IsVideoLoaded { get; }
    void Seek(int frameIndex);
    void Restart();
}
