namespace RiftMusic.Core;
public static class TrackSelector
{
    public static Track? Choose(IEnumerable<Track> tracks, SelectionMode mode, string? fixedId, string? previousId, Func<string, bool> exists)
    {
        var available = tracks.Where(t => t.Enabled && t.IsValid() && exists(t.File)).ToList();
        if (mode == SelectionMode.Fixed) return available.FirstOrDefault(t => t.Id == fixedId);
        if (available.Count > 1) available.RemoveAll(t => t.Id == previousId);
        return available.Count == 0 ? null : available[Random.Shared.Next(available.Count)];
    }
    public static double Envelope(double elapsed, double duration, double fadeIn, double fadeOut)
    {
        if (!double.IsFinite(elapsed) || !double.IsFinite(duration) || duration <= 0 || elapsed < 0 || elapsed >= duration) return 0;
        var sum = fadeIn + fadeOut;
        if (sum > duration) { fadeIn *= duration / sum; fadeOut *= duration / sum; }
        var attack = fadeIn > 0 ? Math.Min(1, elapsed / fadeIn) : 1;
        var release = fadeOut > 0 ? Math.Min(1, (duration - elapsed) / fadeOut) : 1;
        return Math.Clamp(Math.Min(attack, release), 0, 1);
    }
}
