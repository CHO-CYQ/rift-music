namespace RiftMusic.Core;

public sealed record MusicCategory(string Id, string Name, string Caption, string Glyph, string Accent)
{ public override string ToString() => Name; }

public static class MusicLibrary
{
    public static IReadOnlyList<MusicCategory> Categories { get; } = [
        new("all", "全部音乐", "YOUR MUSIC, YOUR OPENING", "\uE8F1", "#72DCC6"),
        new("favorites", "我的收藏", "THE ONES YOU COME BACK TO", "\uEB51", "#E3A3B6"),
        new("worlds", "全球总决赛", "WORLDS · 2011 — 2025", "\uE734", "#E6C58E"),
        new("skins", "英雄与皮肤", "CHAMPIONS & SKIN UNIVERSES", "\uE77B", "#B8ACED"),
        new("events", "活动与宇宙", "EVENTS & STORIES", "\uE734", "#81BDDE"),
        new("seasons", "赛季与地区", "SEASONS & REGIONS", "\uE909", "#91CFA5"),
        new("custom", "本地导入", "YOUR OWN COLLECTION", "\uE8B7", "#ADBBCD"),
        new("draft", "BP 音乐", "CHAMP SELECT · TOURNAMENT MUSIC", "\uE8D6", "#8DB9F3")
    ];
    public static IReadOnlyList<string> DraftCompetitions { get; } = ["Worlds", "MSI", "FirstStand", "LCK", "LPL"];
    public static MusicCategory Category(string? id) => Categories.FirstOrDefault(c => c.Id == id) ?? Categories.First(c => c.Id == "custom");
    public static IEnumerable<Track> InScope(IEnumerable<Track> tracks, string? scope) => scope switch
    {
        "all" or null or "" => tracks,
        "favorites" => tracks.Where(t => t.Favorite),
        _ => tracks.Where(t => t.Category == scope)
    };
    public static IEnumerable<Track> OpeningScope(IEnumerable<Track> tracks, string? scope = "all") => InScope(tracks, scope).Where(t => t.Category != "draft");
    public static IEnumerable<int> Years(IEnumerable<Track> tracks, string scope = "all") => InScope(tracks, scope)
        .SelectMany(t => t.Category == "draft" ? t.DraftUses.Select(u => u.Year) : [t.Year]).Where(y => y > 0).Distinct().OrderDescending();
    public static IEnumerable<Track> Filter(IEnumerable<Track> tracks, string scope = "all", string? query = null, int year = 0, string? theme = null, string? competition = null)
    {
        var result = InScope(tracks, scope);
        var hasCompetition = !string.IsNullOrWhiteSpace(competition) && !competition.Equals("all", StringComparison.OrdinalIgnoreCase);
        if (year > 0 || hasCompetition) result = result.Where(t => t.Category == "draft"
            ? t.DraftUses.Any(u => (year <= 0 || u.Year == year) && (!hasCompetition || u.Competition.Equals(competition, StringComparison.OrdinalIgnoreCase)))
            : !hasCompetition && (year <= 0 || t.Year == year));
        if (!string.IsNullOrWhiteSpace(theme)) result = result.Where(t => t.ThemeLabel == theme);
        var terms = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return result.Where(t => terms.All(term => string.Join(' ', t.Name, t.Artist, t.Theme, t.Season, t.Kind, t.Quality, t.SearchTags, t.YearLabel, t.CategoryName,
            string.Join(' ', t.DraftUses.Select(u => $"{u.Year} {u.Competition} {u.Stage}")))
            .Contains(term, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.Category == "worlds" ? 0 : 1).ThenBy(t => t.Category).ThenBy(t => t.ThemeLabel).ThenBy(t => t.Year).ThenBy(t => t.Name);
    }
}
