namespace RiftMusic.Core;

/// <summary>Browsing follows playback purpose; storage categories remain compatible.</summary>
public static class WorkspaceLibrary
{
    public static IReadOnlyList<MusicCategory> Navigation(bool draft) => draft ? [
        new("all", "全部 BP 音乐", "CHAMP SELECT", "\uE8F1", "#7066A9"),
        new("favorites", "我的收藏", "FAVORITES", "\uEB51", "#9F5770"),
        new("competition:Worlds", "全球总决赛", "WORLDS", "\uE734", "#927044"),
        new("competition:MSI", "MSI 季中赛", "MSI", "\uE734", "#927044"),
        new("competition:FirstStand", "全球先锋赛", "FIRST STAND", "\uE734", "#526F9E"),
        new("competition:LCK", "LCK", "LCK", "\uE909", "#526F9E"),
        new("competition:LPL", "LPL", "LPL", "\uE909", "#9F5770")
    ] : MusicLibrary.Categories.Where(c => c.Id != "draft").ToList();

    public static IEnumerable<Track> InScope(IEnumerable<Track> tracks, bool draft, string scope = "all")
    {
        var purpose = tracks.Where(t => (t.Category == "draft") == draft);
        if (draft && scope.StartsWith("competition:", StringComparison.Ordinal))
            return purpose.Where(t => t.DraftUses.Any(u => u.Competition.Equals(scope[12..], StringComparison.OrdinalIgnoreCase)));
        return MusicLibrary.InScope(purpose, scope);
    }

    public static IEnumerable<Track> Filter(IEnumerable<Track> tracks, bool draft, string scope = "all", string? query = null,
        int year = 0, string? theme = null, string? competition = null)
    {
        if (draft && scope.StartsWith("competition:", StringComparison.Ordinal)) competition = scope[12..];
        return MusicLibrary.Filter(InScope(tracks, draft, scope), "all", query, year, theme, draft ? competition : null);
    }
}
