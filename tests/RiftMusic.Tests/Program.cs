using RiftMusic.Core;
var tests = new List<(string, Action)>();
var origin = DateTimeOffset.UtcNow.AddMinutes(-5);
GameSnapshot Snap(double t, int nextGame = 0, bool start = true, bool player = true) => new(t, origin.AddSeconds(t + nextGame), "roster-a", 11, start, player);
Track Song(string id = "a") => new() { Id = id, Name = "开场", File = id + ".wav", Duration = 80, Start = 0, End = 60 };
void Test(string name, Action body) => tests.Add((name, body));
void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}"); }
void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Unsafe input was accepted"); }

Test("opening waits for advancing time, then triggers once after persisted claim", () => {
    var history = new List<SessionStamp>(); var d = new OpeningDetector(history);
    Equal(OpeningDecision.Waiting, d.Observe(Snap(.5)));
    Equal(OpeningDecision.Waiting, d.Observe(Snap(.5)));
    Equal(OpeningDecision.Play, d.Observe(Snap(1)));
    history.Add(OpeningDetector.Stamp(Snap(1)));
    Equal(OpeningDecision.Handled, d.Observe(Snap(1.5)));
});
Test("historical start does not play when attached mid-game", () => Equal(OpeningDecision.Skip, new OpeningDetector([]).Observe(Snap(120))));
Test("loading with frozen zero clock cannot play", () => {
    var d = new OpeningDetector([]); Equal(OpeningDecision.Waiting, d.Observe(Snap(0))); Equal(OpeningDecision.Waiting, d.Observe(Snap(0)));
});
Test("missing event or active player prevents playing", () => {
    var d = new OpeningDetector([]); d.Observe(Snap(.5));
    Equal(OpeningDecision.Waiting, d.Observe(Snap(1, start:false)));
    Equal(OpeningDecision.Waiting, d.Observe(Snap(2, player:false)));
});
Test("restarted app recognizes game claim and real next game rearms", () => {
    var d = new OpeningDetector([OpeningDetector.Stamp(Snap(1))]);
    Equal(OpeningDecision.Handled, d.Observe(Snap(4)));
    Equal(OpeningDecision.Waiting, d.Observe(Snap(.5, 1200)));
    Equal(OpeningDecision.Play, d.Observe(Snap(1, 1200)));
});
Test("malformed time and other map never trigger", () => {
    var d = new OpeningDetector([]);
    foreach (var t in new[]{ double.NaN, double.PositiveInfinity, -1d }) Equal(OpeningDecision.Waiting,d.Observe(Snap(1) with { GameTime=t }));
    Equal(OpeningDecision.Waiting,d.Observe(Snap(1) with {MapNumber=12}));
});
Test("random excludes disabled/missing and previous track", () => {
    var disabled = Song("c"); disabled.Enabled=false;
    Equal("b", TrackSelector.Choose([Song(),Song("b"),disabled,Song("missing")],SelectionMode.Random,null,"a",f=>f!="missing.wav")?.Id);
});
Test("single random track may repeat, invalid fixed never substitutes", () => {
    Equal("a",TrackSelector.Choose([Song()],SelectionMode.Random,null,"a",_=>true)?.Id);
    Equal<Track?>(null,TrackSelector.Choose([Song()],SelectionMode.Fixed,"missing",null,_=>true));
});
Test("clip rejects negative, NaN and end beyond real media", () => {
    var s=Song(); Equal(true,s.IsValid()); s.Start=-1; Equal(false,s.IsValid()); s.Start=0;s.End=81;Equal(false,s.IsValid());
    s.End=60;s.FadeIn=double.NaN;Equal(false,s.IsValid());s.FadeIn=0;s.End=s.Start;Equal(false,s.IsValid());
});
Test("fade envelope starts and ends silent and never exceeds one", () => {
    Equal(0d,TrackSelector.Envelope(0,10,1,3)); Equal(.5d,TrackSelector.Envelope(.5,10,1,3));
    Equal(1d,TrackSelector.Envelope(3,10,1,3)); Equal(.5d,TrackSelector.Envelope(8.5,10,1,3));
    Equal(0d,TrackSelector.Envelope(10,10,1,3));
    for(double t=0;t<1;t+=.01) { var v=TrackSelector.Envelope(t,1,3,3); if(v<0 || v>1) throw new Exception("invalid short-clip gain"); }
});
Test("store roundtrip backup recovery retains valid user settings", () => {
    var dir=Path.Combine(Path.GetTempPath(),"rift-test-"+Guid.NewGuid());
    try { var store=new SettingsStore(dir);store.Save(new(){Volume=.6,Tracks=[Song()]});store.Save(new(){Volume=.8});
      Equal(.8,store.Load().Volume);File.WriteAllText(Path.Combine(dir,"settings.json"),"broken");
      Equal(.6,store.Load().Volume);Equal(true,store.Warning is not null);
    } finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
});
Test("failed backup cannot overwrite the last valid personal settings", () => {
    var dir=Path.Combine(Path.GetTempPath(),"rift-backup-failure-"+Guid.NewGuid());
    try {var store=new SettingsStore(dir);store.Save(new(){Volume=.63,Tracks=[Song()]});Directory.CreateDirectory(Path.Combine(dir,"settings.json.bak"));
        var denied=false;try {store.Save(new(){Volume=.12});}catch(Exception e) when(e is IOException or UnauthorizedAccessException){denied=true;}
        Equal(true,denied);Equal(.63,store.Load().Volume);Equal("a",store.Load().Tracks.Single().Id);
    } finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
});
Test("music paths cannot escape managed directory", () => {
    var store=new SettingsStore(Path.Combine(Path.GetTempPath(),"rift-data"));
    Reject(()=>store.ResolveMusic("../secret.wav")); Reject(()=>store.ResolveMusic(@"C:\secret.wav"));Reject(()=>store.ResolveMusic(""));
    Equal(true,store.ResolveMusic("my song.wav").EndsWith(Path.Combine("Music","my song.wav")));
});

Test("Riot parser uses stable roster order and active Riot ID", () => {
    const string stats="""{"gameTime":1.2,"mapNumber":11,"gameMode":"CLASSIC","mapName":"Map11","mapTerrain":"Default"}""";
    const string events="""{"Events":[{"EventID":0,"EventName":"GameStart","EventTime":0.03}]}""";
    const string active="\"User#CN\"";
    const string one="""{"riotId":"User#CN","summonerName":"User","championName":"Swain","team":"ORDER","skinID":0}""";
    const string two="""{"riotId":"Friend#CN","summonerName":"Friend","championName":"Lux","team":"CHAOS","skinID":7}""";
    var a=GameParser.Parse(stats,events,active,"["+one+","+two+"]",origin)!;
    var b=GameParser.Parse(stats,events,active,"["+two+","+one+"]",origin)!;
    Equal(true,a.HasStart);Equal(true,a.HasPlayer);Equal(1.2,a.GameTime);Equal(a.RosterKey,b.RosterKey);
    Equal(false,GameParser.Parse(stats,events,"\"Other#CN\"","["+one+"]",origin)!.HasPlayer);
    Equal<GameSnapshot?>(null,GameParser.Parse("broken",events,active,"[]",origin));
});
Test("persisted claim is on disk before play is returned", () => {
    var dir=Path.Combine(Path.GetTempPath(),"rift-test-"+Guid.NewGuid());
    try { var store=new SettingsStore(dir); var s=new AppSettings(); var g=new SessionGate(s,()=>store.Save(s));
        g.Process(Snap(.5));Equal(OpeningDecision.Play,g.Process(Snap(1)));
        Equal(1,store.Load().Sessions.Count);
        Equal(OpeningDecision.Handled,new SessionGate(s,()=>store.Save(s)).Process(Snap(2)));
    } finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
});
Test("failed claim persistence cannot report play or keep phantom claim", () => {
    var s=new AppSettings();var g=new SessionGate(s,()=>throw new IOException("disk full"));g.Process(Snap(.5));
    try {g.Process(Snap(1));throw new Exception("expected persistence failure");} catch(IOException) { }
    Equal(0,s.Sessions.Count);
});

Test("paused opening remains handled despite estimated start shifting", () => {
    var d=new OpeningDetector([OpeningDetector.Stamp(Snap(1))]);
    Equal(OpeningDecision.Handled,d.Observe(Snap(2)));
    Equal(OpeningDecision.Handled,d.Observe(Snap(2) with {ObservedAt=origin.AddSeconds(32)}));
    Equal(OpeningDecision.Handled,d.Observe(Snap(2.5) with {ObservedAt=origin.AddSeconds(32.5)}));
    Equal(OpeningDecision.Waiting,d.Observe(Snap(.5,1200)));
    Equal(OpeningDecision.Play,d.Observe(Snap(1,1200)));
});
Test("overflowing numeric configuration recovers and can be saved at startup", () => {
    var dir=Path.Combine(Path.GetTempPath(),"rift-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);
    try {File.WriteAllText(Path.Combine(dir,"settings.json"),"""{"Tracks":[{"Name":"overflow","File":"x.wav","Duration":1e400,"End":60}]}""");
        var store=new SettingsStore(dir);var restored=store.Load();store.Save(restored);
        Equal(true,store.Warning is not null);Equal(true,restored.Tracks.All(t=>double.IsFinite(t.Duration)));
    } finally {Directory.Delete(dir,true);}
});
Test("pause immediately after successful claim cannot replay", () => {
    var history=new List<SessionStamp>();var d=new OpeningDetector(history);d.Observe(Snap(.5));
    Equal(OpeningDecision.Play,d.Observe(Snap(1)));history.Add(OpeningDetector.Stamp(Snap(1)));
    Equal(OpeningDecision.Handled,d.Observe(Snap(1) with {ObservedAt=origin.AddSeconds(31)}));
    Equal(OpeningDecision.Handled,d.Observe(Snap(1.5) with {ObservedAt=origin.AddSeconds(31.5)}));
});
Test("library filters category, favorite, year and multilingual metadata together", () => {
    var a=Song(); a.Category="worlds"; a.Year=2017; a.Theme="全球总决赛"; a.SearchTags="Legends Never Die 传奇永不熄"; a.Favorite=true;
    var b=Song("b"); b.Category="skins"; b.Year=2022; b.Theme="星之守护者";
    Equal("a",MusicLibrary.Filter([a,b],"worlds","legends",2017).Single().Id);
    Equal(0,MusicLibrary.Filter([a,b],"worlds","",2022).Count());
    Equal("a",MusicLibrary.Filter([a,b],"favorites","传奇").Single().Id);
    Equal("b",MusicLibrary.Filter([a,b],"all","星之守护者").Single().Id);
});
Test("random scope is explicit and empty scope never falls back", () => {
    var a=Song(); a.Category="worlds"; var b=Song("b"); b.Category="skins";
    Equal("a",TrackSelector.Choose(MusicLibrary.InScope([a,b],"worlds"),SelectionMode.Random,null,null,_=>true)?.Id);
    Equal<Track?>(null,TrackSelector.Choose(MusicLibrary.InScope([a,b],"favorites"),SelectionMode.Random,null,null,_=>true));
});
Test("classification survives storage and older settings keep custom tracks", () => {
    var dir=Path.Combine(Path.GetTempPath(),"rift-library-"+Guid.NewGuid());
    try {var a=Song();a.Category="events";a.Theme="奥德赛";a.Year=2018;a.Favorite=true;a.Quality="原始游戏音频";
      var store=new SettingsStore(dir);store.Save(new(){RandomScope="events",Tracks=[a]});var data=store.Load();
      Equal("events",data.RandomScope);Equal("奥德赛",data.Tracks[0].Theme);Equal(true,data.Tracks[0].Favorite);Equal(2018,data.Tracks[0].Year);
      File.WriteAllText(Path.Combine(dir,"settings.json"),"""{"Version":1,"Tracks":[{"Id":"legacy","Name":"旧音乐","File":"old.wav","Duration":60,"End":60}]}""");
      Equal("custom",store.Load().Tracks.Single().Category);Equal("all",store.Load().RandomScope);
    } finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
});
Test("workspace isolates opening and BP including favorites", () => {
    var opening=Song("opening");opening.Category="worlds";opening.Favorite=true;
    var bp=Song("bp");bp.Category="draft";bp.Favorite=true;bp.DraftUses=[new(){Year=2024,Competition="LPL"},new(){Year=2025,Competition="Worlds"}];
    Equal("opening",WorkspaceLibrary.InScope([opening,bp],false,"all").Single().Id);
    Equal("bp",WorkspaceLibrary.InScope([opening,bp],true,"favorites").Single().Id);
    Equal("opening",WorkspaceLibrary.InScope([opening,bp],false,"favorites").Single().Id);
    Equal(0,WorkspaceLibrary.Filter([opening,bp],true,"competition:LPL",year:2025).Count());
    Equal("bp",WorkspaceLibrary.Filter([opening,bp],true,"competition:Worlds",year:2025).Single().Id);
});
Test("workspace navigation gives BP its own competition hierarchy", () => {
    Equal(false,WorkspaceLibrary.Navigation(false).Any(c=>c.Id=="draft"));
    Equal(5,WorkspaceLibrary.Navigation(true).Count(c=>c.Id.StartsWith("competition:")));
    Equal(false,WorkspaceLibrary.Navigation(true).Any(c=>c.Id=="skins"));
});
Test("multi-selection unions verified pairs without crossing years or duplicating tracks", () => {
    var a=Song("a");a.Category="draft";a.DraftUses=[new(){Competition="LPL",Year=2024},new(){Competition="Worlds",Year=2025}];
    var b=Song("b");b.Category="draft";b.DraftUses=[new(){Competition="LPL",Year=2025}];
    var selection=new BrowseSelection();selection.Toggle(new("competition:LPL",2024),true,[]);selection.Toggle(new("competition:Worlds",2025),true,[]);
    Equal("a",selection.Filter([a,b],true).Single().Id);
    selection.Clear();selection.Toggle(new("competition:LPL",2025),true,[]);selection.Toggle(new("competition:Worlds",2024),true,[]);
    Equal("b",selection.Filter([a,b],true).Single().Id);
});
Test("unchecking a child of an entire competition keeps the other years", () => {
    var selection=new BrowseSelection();BrowseRule[] leaves=[new("competition:Worlds",2024),new("competition:Worlds",2025)];
    selection.Toggle(new("competition:Worlds"),true,leaves);Equal(true,selection.State(leaves[0],leaves));
    selection.Toggle(leaves[0],false,leaves);Equal(false,selection.State(leaves[0],leaves));Equal(true,selection.State(leaves[1],leaves));
    Equal<bool?>(null,selection.State(new("competition:Worlds"),leaves));
});
Test("favorites and purpose remain independent of sidebar multi-selection", () => {
    var opening=Song("opening");opening.Favorite=true;opening.Category="worlds";opening.Year=2025;
    var draft=Song("draft");draft.Favorite=true;draft.Category="draft";draft.DraftUses=[new(){Competition="FirstStand",Year=2026}];
    var selection=new BrowseSelection{FavoritesOnly=true};selection.Toggle(new("competition:FirstStand",2026),true,[]);
    Equal("draft",selection.Filter([opening,draft],true).Single().Id);Equal(0,selection.Filter([opening,draft],false).Count());
    draft.Favorite=false;Equal(0,selection.Filter([opening,draft],true).Count());
});
Test("unmarked opening year is separate from the full parent and cloning isolates workspace state", () => {
    var a=Song();a.Category="skins";a.Year=0;var b=Song("b");b.Category="skins";b.Year=2022;
    var selection=new BrowseSelection();BrowseRule[] leaves=[new("skins",-1),new("skins",2022)];
    selection.Toggle(leaves[0],true,leaves);Equal("a",selection.Filter([a,b],false).Single().Id);
    var saved=selection.Clone();selection.Clear();Equal("a",saved.Filter([a,b],false).Single().Id);
    saved.Toggle(leaves[0],false,leaves);Equal(0,saved.Filter([a,b],false).Count());Equal(false,saved.ShowAll);
});
int failures=0;
foreach(var (name,body) in tests) { try { body();Console.WriteLine("PASS "+name); } catch(Exception e) { failures++;Console.WriteLine("FAIL "+name+": "+e.Message); } }
Console.WriteLine($"{tests.Count-failures}/{tests.Count} passed");return failures==0?0:1;
