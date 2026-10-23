namespace RiftMusic.Core;

public enum SelectionMode { Fixed, Random }
public enum OpeningDecision { Waiting, Play, Handled, Skip }
public sealed record GameSnapshot(double GameTime, DateTimeOffset ObservedAt, string RosterKey, int MapNumber, bool HasStart, bool HasPlayer)
{
    public DateTimeOffset EstimatedStart => ObservedAt.AddSeconds(-GameTime);
}
public sealed record SessionStamp(string RosterKey, int MapNumber, DateTimeOffset StartedAt);
public sealed class DraftUse
{
    public int Year { get; set; }
    public string Competition { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Evidence { get; set; } = "";
}
public sealed class Track
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "未命名音乐";
    public string Season { get; set; } = "";
    public string Kind { get; set; } = "用户自定义";
    public string File { get; set; } = "";
    public string Source { get; set; } = "";
    public string Artist { get; set; } = "";
    public string CoverFile { get; set; } = "";
    public List<DraftUse> DraftUses { get; set; } = [];
    public string Category { get; set; } = "custom";
    public string Theme { get; set; } = "";
    public int Year { get; set; }
    public string Quality { get; set; } = "";
    public string SearchTags { get; set; } = "";
    public bool Favorite { get; set; }
    public double Duration { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    public double FadeIn { get; set; } = 0.8;
    public double FadeOut { get; set; } = 3;
    public bool Enabled { get; set; } = true;
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PlaybackError { get; set; }
    public string Display => $"{Name}";
    [System.Text.Json.Serialization.JsonIgnore] public string CategoryName => MusicLibrary.Category(Category).Name;
    [System.Text.Json.Serialization.JsonIgnore] public string ThemeLabel => string.IsNullOrWhiteSpace(Theme) ? CategoryName : Theme;
    [System.Text.Json.Serialization.JsonIgnore] public string LibrarySubtitle => Category == "draft" && DraftUses.Count > 0
        ? string.Join(" · ", DraftUses.GroupBy(u=>u.Competition).Select(g=>$"{(g.Key=="Worlds"?"S 赛":g.Key=="FirstStand"?"全球先锋赛":g.Key)} {string.Join(" / ",g.Select(u=>u.Year).Distinct().Order())}"))
        : $"{ThemeLabel} · {YearLabel}";
    [System.Text.Json.Serialization.JsonIgnore] public string YearLabel => Year > 0 ? Year.ToString() : "—";
    [System.Text.Json.Serialization.JsonIgnore] public string DurationLabel => IsValid() ? TimeSpan.FromSeconds(End - Start).ToString(@"m\:ss") : "—";
    [System.Text.Json.Serialization.JsonIgnore] public string Accent => MusicLibrary.Category(Category).Accent;
    [System.Text.Json.Serialization.JsonIgnore] public string Glyph => MusicLibrary.Category(Category).Glyph;
    [System.Text.Json.Serialization.JsonIgnore] public string EditionLabel => PlaybackError is null ? Kind : "音频不可用";
    public string Subtitle => $"{(string.IsNullOrWhiteSpace(Season) ? "自定义" : Season)}  ·  {Kind}  ·  {(PlaybackError is null ? $"{End - Start:0.#} 秒" : "音频不可用")}";
    public bool IsValid() => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Name)
        && !string.IsNullOrWhiteSpace(File) && double.IsFinite(Duration) && Duration > 0
        && double.IsFinite(Start) && double.IsFinite(End) && Start >= 0 && End > Start && End <= Duration
        && double.IsFinite(FadeIn) && double.IsFinite(FadeOut) && FadeIn >= 0 && FadeOut >= 0;
}
public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public bool AutoPlay { get; set; }
    public SelectionMode Mode { get; set; }
    public string? FixedTrackId { get; set; }
    public string? LastTrackId { get; set; }
    public double Volume { get; set; } = .35;
    public List<Track> Tracks { get; set; } = [];
    public List<SessionStamp> Sessions { get; set; } = [];
    public bool TrayHintShown { get; set; }
    public string RandomScope { get; set; } = "all";
    public bool AutoDraft { get; set; }
    public SelectionMode DraftMode { get; set; } = SelectionMode.Random;
    public string? FixedDraftTrackId { get; set; }
    public string? LastDraftTrackId { get; set; }
    public int DraftYear { get; set; }
    public string DraftCompetition { get; set; } = "all";
    public string LeagueClientDirectory { get; set; } = "";
}
