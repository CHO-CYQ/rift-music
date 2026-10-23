using System.Text.Json;
using RiftMusic.Core;

var tests = new List<(string Name, Action Body)>();
void Test(string name, Action body) => tests.Add((name, body));
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}"); }
Track Song(string id = "opening") => new() { Id = id, Name = id, File = id + ".wav", Duration = 60, End = 60 };
Track Draft() => JsonSerializer.Deserialize<Track>("""{"Id":"draft","Name":"Draft track","File":"draft.wav","Category":"draft","Year":2026,"Duration":60,"End":60,"DraftUses":[{"Year":2021,"Competition":"Worlds","Stage":"Final","Evidence":"official-2021"},{"Year":2022,"Competition":"LCK","Stage":"Spring","Evidence":"official-2022"}]}""")!;
IEnumerable<Track> Filter(Track track, int year, string competition)
    => MusicLibrary.Filter([track], "draft", year: year, competition: competition);
IEnumerable<Track> Openings(IEnumerable<Track> tracks, string scope)
    => MusicLibrary.OpeningScope(tracks, scope);

Test("draft years come from documented use rather than track release year", () => {
    Equal("draft", MusicLibrary.Filter([Draft()], "draft", year: 2021).SingleOrDefault()?.Id);
});
Test("combined year and competition cannot join two different uses", () => {
    Equal(1, Filter(Draft(), 2021, "Worlds").Count());
    Equal(0, Filter(Draft(), 2021, "LCK").Count());
    Equal(1, Filter(Draft(), 2022, "lck").Count());
    Equal(0, Filter(Draft(), 2026, "Worlds").Count());
});
Test("opening scopes reject draft including favorites and fixed selectors", () => {
    var opening = Song(); opening.Favorite = true; var draft = Draft(); draft.Favorite = true;
    foreach (var scope in new[] { "all", "favorites", "draft" }) {
        Equal(false, Openings([opening, draft], scope).Any(t => t.Category == "draft"));
        Equal<Track?>(null, TrackSelector.Choose(Openings([opening, draft], scope), SelectionMode.Fixed, "draft", null, _ => true));
    }
    Equal(2, MusicLibrary.InScope([opening, draft], "favorites").Count());
});
Test("additive settings roundtrip retains BP metadata and existing personal choices", () => {
    var directory = Path.Combine(Path.GetTempPath(), "rift-draft-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
    try {
        File.WriteAllText(Path.Combine(directory, "settings.json"), """{"Version":1,"AutoPlay":true,"Mode":"Random","RandomScope":"favorites","Volume":0.73,"FixedTrackId":"old","LastTrackId":"old","AutoDraft":true,"DraftMode":"Fixed","FixedDraftTrackId":"bp","LastDraftTrackId":"bp-last","DraftYear":2021,"DraftCompetition":"Worlds","Tracks":[{"Id":"bp","Name":"BP","File":"bp.wav","Category":"draft","Artist":"Composer","CoverFile":"draft.jpg","Duration":60,"End":60,"Favorite":true,"DraftUses":[{"Year":2021,"Competition":"Worlds","Stage":"Final","Evidence":"publisher"}]}]}""");
        var store = new SettingsStore(directory); var settings = store.Load(); store.Save(settings);
        using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "settings.json")));
        var root = saved.RootElement;
        Equal(true, root.GetProperty("AutoDraft").GetBoolean()); Equal("Fixed", root.GetProperty("DraftMode").GetString());
        Equal("bp", root.GetProperty("FixedDraftTrackId").GetString()); Equal("bp-last", root.GetProperty("LastDraftTrackId").GetString());
        Equal(2021, root.GetProperty("DraftYear").GetInt32()); Equal("Worlds", root.GetProperty("DraftCompetition").GetString());
        Equal(.73, settings.Volume); Equal("favorites", settings.RandomScope); Equal("old", settings.LastTrackId); Equal(true, settings.AutoPlay);
        var track = root.GetProperty("Tracks")[0]; Equal("draft", track.GetProperty("Category").GetString());
        Equal("Composer", track.GetProperty("Artist").GetString()); Equal("draft.jpg", track.GetProperty("CoverFile").GetString());
        Equal("publisher", track.GetProperty("DraftUses")[0].GetProperty("Evidence").GetString()); Equal(true, settings.Tracks[0].Favorite);
    } finally { Directory.Delete(directory, true); }
});
Test("old version one preferences load with draft automation disabled and random default", () => {
    var directory = Path.Combine(Path.GetTempPath(), "rift-legacy-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
    try {
        File.WriteAllText(Path.Combine(directory, "settings.json"), """{"Version":1,"AutoPlay":true,"Mode":"Fixed","FixedTrackId":"old","Volume":0.63,"Tracks":[{"Id":"old","Name":"Old","File":"old.wav","Duration":60,"End":60,"Favorite":true}]}""");
        var store = new SettingsStore(directory); var settings = store.Load(); store.Save(settings);
        using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "settings.json")));
        Equal(false, saved.RootElement.GetProperty("AutoDraft").GetBoolean()); Equal("Random", saved.RootElement.GetProperty("DraftMode").GetString());
        Equal("all", saved.RootElement.GetProperty("DraftCompetition").GetString()); Equal(.63, settings.Volume); Equal("old", settings.FixedTrackId);
        Equal(true, settings.Tracks.Single().Favorite); Equal("custom", settings.Tracks.Single().Category);
    } finally { Directory.Delete(directory, true); }
});

DraftDetector Detector() => new();
string Observe(DraftDetector detector, string? phase) => detector.Observe(phase).ToString();
LcuConnection? ParseLock(string content) => LcuConnection.Parse(content);
string? ParsePhase(string content) => DraftDetector.ParsePhase(content);
Test("champ select enters once and reconnecting does not rearm playback", () => {
    var detector = Detector(); Equal("Play", Observe(detector, "ChampSelect"));
    Equal("Handled", Observe(detector, "ChampSelect")); Equal("Waiting", Observe(detector, null));
    Equal("Waiting", Observe(detector, null)); Equal("Handled", Observe(detector, "ChampSelect"));
});
Test("only confirmed champ select exit stops and a later entry starts again", () => {
    var detector = Detector(); Equal("Waiting", Observe(detector, "Lobby")); Equal("Play", Observe(detector, "ChampSelect"));
    Equal("Stop", Observe(detector, "GameStart")); Equal("Waiting", Observe(detector, "InProgress"));
    Equal("Play", Observe(detector, "ChampSelect")); Equal("Stop", Observe(detector, "Lobby"));
});
Test("phase parser uses literal json strings and rejects malformed or unknown data", () => {
    Equal("ChampSelect", ParsePhase("\"ChampSelect\"")); Equal("InProgress", ParsePhase("\"InProgress\""));
    foreach (var invalid in new[] { "ChampSelect", "null", "{}", "42", "\"UnknownState\"", "\"\"" }) Equal<string?>(null, ParsePhase(invalid));
});
Test("lockfile credentials stay local https and do not appear in descriptions", () => {
    var connection = ParseLock("LeagueClient:1234:54321:literal-secret:https"); Equal(true, connection is not null);
    Equal(false, connection!.ToString()!.Contains("literal-secret"));
    Equal(false, JsonSerializer.Serialize(connection).Contains("literal-secret"));
    foreach (var invalid in new[] { "LeagueClient:1:54321:secret:http", "OtherApp:1:54321:secret:https", "LeagueClient:0:54321:secret:https", "LeagueClient:1:65536:secret:https", "LeagueClient:1:54321::https", "LeagueClient:1:abc:secret:https", "LeagueClient:1:54321:secret:extra:https" }) Equal<object?>(null, ParseLock(invalid));
});
Test("LCU credentials cannot authenticate a remote url or a write request", () => {
    var connection = LcuConnection.Parse("LeagueClient:1234:54321:literal-secret:https")!;
    using var local = new HttpRequestMessage(HttpMethod.Get, new Uri("https://127.0.0.1:54321/lol-gameflow/v1/gameflow-phase"));
    connection.Authenticate(local); Equal("Basic", local.Headers.Authorization?.Scheme);
    Equal("riot:literal-secret", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(local.Headers.Authorization!.Parameter!)));
    foreach (var request in new[] { new HttpRequestMessage(HttpMethod.Get, "https://example.com/lol-gameflow/v1/gameflow-phase"), new HttpRequestMessage(HttpMethod.Post, connection.LoopbackUri), new HttpRequestMessage(HttpMethod.Get, "https://127.0.0.1:54321/another-endpoint") })
    {
        using (request) { try { connection.Authenticate(request); throw new Exception("credentials escaped the local read-only boundary"); } catch (ArgumentException) { } }
    }
});
Test("draft random and fixed selection stay inside the combined use filter", () => {
    var draft = Draft(); var second = Draft(); second.Id = "draft-other"; second.File = "other.wav";
    var opening = Song();
    var range = MusicLibrary.Filter([opening, draft, second], "draft", year: 2021, competition: "Worlds");
    Equal("draft-other", TrackSelector.Choose(range, SelectionMode.Random, null, "draft", _ => true)?.Id);
    Equal<Track?>(null, TrackSelector.Choose(range, SelectionMode.Fixed, "opening", null, _ => true));
    Equal<Track?>(null, TrackSelector.Choose(MusicLibrary.Filter([draft], "draft", year: 2021, competition: "LCK"), SelectionMode.Random, null, null, _ => true));
});

var failures = 0;
foreach (var (name, body) in tests) { try { body(); Console.WriteLine("PASS " + name); } catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error.Message); } }
Console.WriteLine($"{tests.Count - failures}/{tests.Count} passed"); return failures == 0 ? 0 : 1;
