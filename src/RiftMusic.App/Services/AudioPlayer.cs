using System.Windows.Media;
using System.Windows.Threading;
namespace RiftMusic.App.Services;

public sealed class AudioPlayer : IDisposable
{
    private MediaPlayer? player;
    private CancellationTokenSource? operation;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private Track? active;
    private double volume = .35;
    private bool starting;
    public bool IsBusy => operation is not null;
    public bool IsAutomatic { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsLooping { get; private set; }
    public string? CurrentName { get; private set; }
    public double Progress { get; private set; }
    public double PositionSeconds { get; private set; }
    public string? CurrentId => active?.Id;
    public double ClipDuration => active is null ? 0 : active.End - active.Start;
    public event Action? Changed;
    public event Action<string?, string>? Failed;
    public AudioPlayer() { timer.Tick += (_, _) => Tick(); }
    public double Volume { get => volume; set { volume = Math.Clamp(value, 0, 1); Tick(); } }

    private static async Task<double> OpenAsync(MediaPlayer media, string path, CancellationToken cancellation)
    {
        var completion = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler opened = (_, _) => {
            if (media.NaturalDuration.HasTimeSpan && media.NaturalDuration.TimeSpan.TotalSeconds > 0)
                completion.TrySetResult(media.NaturalDuration.TimeSpan.TotalSeconds);
            else completion.TrySetException(new InvalidDataException("无法读取音频时长。"));
        };
        EventHandler<System.Windows.Media.ExceptionEventArgs> failed = (_, e) => completion.TrySetException(new InvalidDataException("音频无法解码，请使用 MP3 或 PCM WAV。", e.ErrorException));
        media.MediaOpened += opened; media.MediaFailed += failed;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var registration = timeout.Token.Register(() => completion.TrySetCanceled(timeout.Token));
        try { media.Volume = 0; media.Open(new Uri(Path.GetFullPath(path))); return await completion.Task; }
        finally { media.MediaOpened -= opened; media.MediaFailed -= failed; }
    }
    public static async Task<double> ProbeAsync(string path, CancellationToken cancellation)
    {
        var probe = new MediaPlayer();
        try { return await OpenAsync(probe, path, cancellation); }
        finally { probe.Close(); }
    }
    public async Task PlayAsync(Track track, string path, bool automatic, bool loop = false)
    {
        Stop();
        if (!track.IsValid()) throw new InvalidDataException("音乐片段设置无效。");
        var current = new CancellationTokenSource(); operation = current;
        var media = new MediaPlayer(); player = media;
        IsAutomatic = automatic; IsLooping = loop; IsPaused = false; CurrentName = track.Name; starting = true;
        active = new Track { Name = track.Name, Id = track.Id, File = track.File, Start = track.Start, End = track.End,
            Duration = track.Duration, FadeIn = track.FadeIn, FadeOut = track.FadeOut };
        Changed?.Invoke();
        try
        {
            var actualDuration = await OpenAsync(media, path, current.Token);
            if (current.IsCancellationRequested || operation != current) return;
            if (track.End > actualDuration + .05) throw new InvalidDataException("音频长度发生变化，请重新设置片段。");
            media.MediaEnded += Ended; media.MediaFailed += PlaybackFailed;
            media.Position = TimeSpan.FromSeconds(track.Start); starting = false; media.Play(); timer.Start(); Tick();
        }
        catch (OperationCanceledException)
        {
            if (operation == current) { Stop(); throw new InvalidDataException("读取音频超时，请尝试转换为 PCM WAV。"); }
        }
        catch { if (operation == current) Stop(); throw; }
    }
    private void Ended(object? sender, EventArgs e)
    {
        if (sender != player) return;
        if (IsLooping && active is not null && player is not null)
        { player.Position = TimeSpan.FromSeconds(active.Start); PositionSeconds = Progress = 0; if (!IsPaused) player.Play(); Changed?.Invoke(); }
        else Stop();
    }
    public void PauseOrResume()
    {
        if (player is null || active is null || starting) return;
        IsPaused = !IsPaused;
        if (IsPaused) { player.Pause(); timer.Stop(); }
        else { player.Play(); timer.Start(); }
        Changed?.Invoke();
    }
    public void Seek(double clipSeconds)
    {
        if (player is null || active is null || starting || !double.IsFinite(clipSeconds)) return;
        var position = Math.Clamp(clipSeconds, 0, Math.Max(0, ClipDuration - .02));
        player.Position = TimeSpan.FromSeconds(active.Start + position);
        PositionSeconds = position; Progress = position / ClipDuration; Changed?.Invoke();
    }
    private void PlaybackFailed(object? sender, System.Windows.Media.ExceptionEventArgs e)
    { if (sender != player) return; var id = active?.Id; Stop(); Failed?.Invoke(id, "播放失败，请检查音频文件和系统输出设备。"); }
    private void Tick()
    {
        if (player is null || active is null || starting) return;
        var elapsed = Math.Max(0, player.Position.TotalSeconds - active.Start); var length = active.End - active.Start;
        if (elapsed >= length)
        {
            if (!IsLooping) { Stop(); return; }
            player.Position = TimeSpan.FromSeconds(active.Start); elapsed = 0;
            if (!IsPaused) player.Play();
        }
        player.Volume = volume * TrackSelector.Envelope(elapsed, length, active.FadeIn, active.FadeOut);
        PositionSeconds = elapsed; Progress = elapsed / length; Changed?.Invoke();
    }
    public void Stop()
    {
        timer.Stop(); var old = operation; operation = null; old?.Cancel(); old?.Dispose();
        if (player is not null) { player.MediaEnded -= Ended; player.MediaFailed -= PlaybackFailed; player.Stop(); player.Close(); player = null; }
        active = null; IsAutomatic = IsPaused = IsLooping = false; CurrentName = null; Progress = 0; PositionSeconds = 0; starting = false; Changed?.Invoke();
    }
    public void Dispose() => Stop();
}
