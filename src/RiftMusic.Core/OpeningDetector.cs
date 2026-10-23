namespace RiftMusic.Core;
public sealed class OpeningDetector(IReadOnlyList<SessionStamp> history)
{
    private GameSnapshot? previous;
    private GameSnapshot? handled;
    public OpeningDecision Observe(GameSnapshot snapshot)
    {
        if (!double.IsFinite(snapshot.GameTime) || snapshot.GameTime < 0 || snapshot.GameTime > 86400
            || snapshot.MapNumber != 11 || !snapshot.HasPlayer || !snapshot.HasStart
            || string.IsNullOrWhiteSpace(snapshot.RosterKey)) { previous = null; return OpeningDecision.Waiting; }
        // Claim persistence happens after Observe returns. Latch that successful claim
        // using the previous sample even if the clock stalls before the next poll.
        if (handled is null && previous is { } claimed && history.Any(h => Matches(h, claimed)))
            handled = claimed;
        // A stalled game clock changes the estimated UTC origin, not the current game.
        // A real clock rollback (or different roster/map) rearms the observer.
        if (handled is { } seen && seen.RosterKey == snapshot.RosterKey && seen.MapNumber == snapshot.MapNumber
            && snapshot.GameTime >= seen.GameTime - .25)
        { handled = snapshot; previous = snapshot; return OpeningDecision.Handled; }
        handled = null;
        if (history.Any(h => Matches(h, snapshot))) { handled = snapshot; previous = snapshot; return OpeningDecision.Handled; }
        if (snapshot.GameTime > 20) { previous = snapshot; return OpeningDecision.Skip; }
        var last = previous; previous = snapshot;
        if (snapshot.GameTime > 0 && last is not null && Matches(Stamp(last), snapshot)
            && snapshot.GameTime > last.GameTime && snapshot.ObservedAt > last.ObservedAt)
            return OpeningDecision.Play;
        return OpeningDecision.Waiting;
    }
    public static bool Matches(SessionStamp stamp, GameSnapshot snapshot) => stamp.RosterKey == snapshot.RosterKey
        && stamp.MapNumber == snapshot.MapNumber && Math.Abs((stamp.StartedAt - snapshot.EstimatedStart).TotalSeconds) <= 15;
    public static SessionStamp Stamp(GameSnapshot snapshot) => new(snapshot.RosterKey, snapshot.MapNumber, snapshot.EstimatedStart);
}
