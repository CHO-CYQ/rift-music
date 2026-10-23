namespace RiftMusic.Core;
public sealed class SessionGate
{
    private readonly AppSettings settings;
    private readonly Action persist;
    private readonly OpeningDetector detector;
    public SessionGate(AppSettings settings, Action persist)
    { this.settings = settings; this.persist = persist; detector = new(settings.Sessions); }
    public OpeningDecision Process(GameSnapshot snapshot)
    {
        var decision = detector.Observe(snapshot);
        if (decision is OpeningDecision.Play or OpeningDecision.Skip)
        {
            var stamp = OpeningDetector.Stamp(snapshot); settings.Sessions.Add(stamp);
            try { persist(); }
            catch { settings.Sessions.Remove(stamp); throw; }
        }
        return decision;
    }
}
