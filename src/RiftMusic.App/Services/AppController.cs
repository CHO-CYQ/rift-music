using System.Diagnostics;
namespace RiftMusic.App.Services;

public sealed class AppController : IDisposable
{
    public SettingsStore Store { get; }
    public AppSettings Settings { get; }
    public AudioPlayer Audio { get; } = new();
    public string Status { get; private set; } = "自动播放已关闭";
    public string Detail { get; private set; } = "添加音乐，让下一次开局响起熟悉的旋律。";
    public string Diagnostics { get; private set; } = "本机接口：127.0.0.1:2999 · 等待首次检测";
    public string DraftStatus { get; private set; } = "自动 BP 已关闭";
    public string DraftDiagnostics { get; private set; } = "本机 LCU · 等待首次检测";
    public event Action? Changed;
    private readonly LiveGameClient client;
    private readonly DraftClient draftClient;
    private DraftDetector draftDetector = new();
    private bool draftPlayback;
    private SessionGate gate;
    private readonly Dictionary<string, (long Length, long Changed)> failedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource lifetime = new();
    private Task? polling;
    private DateTimeOffset lastValid = DateTimeOffset.UtcNow;
    private DateTimeOffset lastDraftValid = DateTimeOffset.UtcNow;
    public AppController(string dataDirectory)
    {
        Store = new(dataDirectory); Settings = Store.Load(); Audio.Volume = Settings.Volume;
        gate = new(Settings, () => Store.Save(Settings)); client = new(); draftClient = new(Settings.LeagueClientDirectory);
        Audio.Failed += (id, message) => {
            var wasDraft = draftPlayback; if (wasDraft) draftPlayback = false;
            if (Settings.Tracks.FirstOrDefault(t => t.Id == id) is { } t) MarkFailed(t, message);
            if (wasDraft) SetDraftStatus("BP 音乐不可用"); else SetStatus("音乐不可用", message);
        };
        if (Settings.Tracks.Count > 0) Detail = "选择一首音乐试听，打开“自动开场”后等待下一局。";
        if (Settings.AutoPlay) { Status = "等待游戏"; Detail = "进入召唤师峡谷后，开场音乐会自动播放。"; }
        if (Settings.AutoDraft) DraftStatus = "等待选人";
        if (Store.Warning is not null) Detail = Store.Warning;
    }
    public void Start() => polling ??= RunAsync();
    public bool Exists(string file) { try { return File.Exists(Store.ResolveMusic(file)); } catch (ArgumentException) { return false; } }
    public bool CanPlay(Track track) => track.Enabled && track.IsValid() && Usable(track.File);
    private (long Length, long Changed) Fingerprint(string file)
    {
        try { var info = new FileInfo(Store.ResolveMusic(file)); return info.Exists ? (info.Length, info.LastWriteTimeUtc.Ticks) : (-1, 0); }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return (-1, 0); }
    }
    private bool Usable(string file)
    {
        if (!Exists(file)) return false;
        if (!failedFiles.TryGetValue(file, out var failed)) return true;
        if (failed == Fingerprint(file)) return false;
        ClearFailure(file); return true;
    }
    private void ClearFailure(string file)
    { failedFiles.Remove(file); foreach (var t in Settings.Tracks.Where(t => t.File == file)) t.PlaybackError = null; }
    private void MarkFailed(Track track, string message)
    { failedFiles[track.File] = Fingerprint(track.File); track.PlaybackError = message; Changed?.Invoke(); }
    public void Save() { Store.Save(Settings); Changed?.Invoke(); }
    public void SetEnabled(bool enabled)
    {
        if (enabled && SelectTrack() is null) throw new InvalidOperationException("请先添加并选择一首可播放的音乐。");
        var old = Settings.AutoPlay; Settings.AutoPlay = enabled;
        try { Save(); } catch { Settings.AutoPlay = old; throw; }
        if (enabled && !old) gate = new(Settings, () => Store.Save(Settings));
        if (!enabled && Audio.IsAutomatic && !draftPlayback) Audio.Stop();
        SetStatus(enabled ? "等待游戏" : "自动播放已关闭", enabled ? "进入召唤师峡谷后，开场音乐会自动播放。" : "你仍然可以选择一首音乐试听。");
    }
    public void SetDraftEnabled(bool enabled)
    {
        if (enabled && SelectDraftTrack() is null) throw new InvalidOperationException("当前年份和赛事没有可播放的 BP 音乐，请导入音乐或重新选择固定曲目。");
        var old = Settings.AutoDraft; Settings.AutoDraft = enabled;
        try { Save(); } catch { Settings.AutoDraft = old; throw; }
        if (enabled && !old) { draftDetector = new(); lastDraftValid = DateTimeOffset.UtcNow; }
        if (!enabled) StopDraftPlayback();
        SetDraftStatus(enabled ? "等待选人" : "自动 BP 已关闭");
    }
    private Track? SelectTrack() => TrackSelector.Choose(MusicLibrary.OpeningScope(Settings.Tracks, Settings.Mode == SelectionMode.Random ? Settings.RandomScope : "all"),
        Settings.Mode, Settings.FixedTrackId, Settings.LastTrackId, Usable);
    private Track? SelectDraftTrack() => TrackSelector.Choose(MusicLibrary.Filter(Settings.Tracks, "draft", year: Settings.DraftYear, competition: Settings.DraftCompetition),
        Settings.DraftMode, Settings.FixedDraftTrackId, Settings.LastDraftTrackId, Usable);
    public async Task PreviewAsync(Track track)
    {
        if (Audio.IsAutomatic) throw new InvalidOperationException("自动音乐正在播放，请先停止再试听。");
        try { await Audio.PlayAsync(track, Store.ResolveMusic(track.File), false); ClearFailure(track.File); Changed?.Invoke(); }
        catch (Exception e) when (e is IOException or InvalidDataException) { MarkFailed(track, e.Message); throw; }
    }
    public void StopPlayback()
    {
        var auto = Audio.IsAutomatic; var draft = draftPlayback; Audio.Stop(); draftPlayback = false;
        if (draft) SetDraftStatus("本轮 BP 已停止；下一轮继续自动检测");
        else if (auto) SetStatus("本局已播放", "已停止本局开场音乐；下一局继续自动检测。");
    }
    private async Task RunAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                try
                {
                    if (!Settings.AutoPlay && !Settings.AutoDraft) { await Task.Delay(1000, lifetime.Token); continue; }
                    var liveRead = client.ReadAsync(lifetime.Token);
                    var draftRead = Settings.AutoDraft ? draftClient.ReadAsync(lifetime.Token) : Task.FromResult<string?>(null);
                    await Task.WhenAll(liveRead, draftRead);
                    var snapshot = await liveRead;
                    await ProcessDraftAsync(await draftRead, snapshot is not null);
                    if (snapshot is null)
                    {
                        Diagnostics = client.LastError ?? "尚未连接到对局。";
                        if (Audio.IsAutomatic && !draftPlayback && DateTimeOffset.UtcNow - lastValid > TimeSpan.FromSeconds(5)) Audio.Stop();
                        if (Settings.AutoPlay) SetStatus("等待游戏", "请进入召唤师峡谷。加载期间不会播放。");
                        else Changed?.Invoke();
                        await Task.Delay(500, lifetime.Token); continue;
                    }
                    lastValid = DateTimeOffset.UtcNow;
                    Diagnostics = $"已连接本机接口 · 游戏时间 {snapshot.GameTime:0.0} 秒 · 地图 {snapshot.MapNumber} · {(snapshot.HasStart ? "已收到开局事件" : "等待开局事件")}";
                    if (!Settings.AutoPlay) { await Task.Delay(500, lifetime.Token); continue; }
                    var decision = gate.Process(snapshot);
                    switch (decision)
                    {
                        case OpeningDecision.Play:
                            var track = SelectTrack();
                            if (track is null) { SetStatus("本局已跳过", "没有可播放的音乐。请检查文件、勾选状态和固定曲目。"); break; }
                            StopDraftPlayback(); Settings.LastTrackId = track.Id; Save();
                            SetStatus("本局已播放", $"本局曲目：{track.Name}。播放结束后会等待下一局。");
                            try { await Audio.PlayAsync(track, Store.ResolveMusic(track.File), true); ClearFailure(track.File); }
                            catch (Exception e) when (e is IOException or InvalidDataException) { MarkFailed(track, e.Message); throw; }
                            break;
                        case OpeningDecision.Skip: SetStatus("本局已跳过", "本局已超过开场时间，下一局再播放。"); break;
                        case OpeningDecision.Handled:
                            if (Status is not ("本局已播放" or "本局已跳过" or "音乐不可用"))
                                SetStatus("本局已处理", "本局不再重复播放；下一局会自动检测。");
                            else Changed?.Invoke(); break;
                        default: SetStatus("正在确认开局", "等待开局事件、玩家信息和游戏计时。"); break;
                    }
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
                { Diagnostics = e.GetType().Name; SetStatus("暂时无法自动播放", e.Message); }
                await Task.Delay(500, lifetime.Token);
            }
        }
        catch (OperationCanceledException) { }
    }
    private async Task ProcessDraftAsync(string? phase, bool liveGame)
    {
        if (!Settings.AutoDraft) return;
        var decision = draftDetector.Observe(phase);
        if (phase is not null)
        {
            lastDraftValid = DateTimeOffset.UtcNow;
            DraftDiagnostics = $"已连接本机 LCU · 阶段 {phase}";
        }
        else DraftDiagnostics = draftClient.LastError ?? "LCU 暂时不可达。";
        if (liveGame)
        {
            StopDraftPlayback(); SetDraftStatus("已进入游戏，等待下一轮 BP"); return;
        }
        if (decision == DraftDecision.Stop)
        {
            StopDraftPlayback(); SetDraftStatus("已离开选人，等待下一轮 BP"); return;
        }
        if (phase is null)
        {
            if (draftPlayback && DateTimeOffset.UtcNow - lastDraftValid > TimeSpan.FromSeconds(5))
            { StopDraftPlayback(); SetDraftStatus("客户端连接中断，本轮 BP 已停止"); }
            else SetDraftStatus(draftDetector.IsChampSelect ? "连接暂时中断，保留本轮 BP 状态" : "等待客户端选人");
            return;
        }
        if (decision != DraftDecision.Play)
        {
            if (draftPlayback) SetDraftStatus($"BP 循环播放 · {Audio.CurrentName}");
            else SetDraftStatus(draftDetector.IsChampSelect ? "本轮 BP 已处理" : "等待选人");
            return;
        }
        // A live-game opening already using the shared player always keeps priority.
        if (Audio.IsAutomatic && !draftPlayback) { SetDraftStatus("开场音乐正在播放，本轮 BP 已跳过"); return; }
        var track = SelectDraftTrack();
        if (track is null) { SetDraftStatus("本轮 BP 已跳过：当前范围没有可播放音乐"); return; }
        var previous = Settings.LastDraftTrackId; Settings.LastDraftTrackId = track.Id;
        try { Save(); } catch { Settings.LastDraftTrackId = previous; throw; }
        draftPlayback = true; SetDraftStatus($"BP 循环播放 · {track.Name}");
        try { await Audio.PlayAsync(track, Store.ResolveMusic(track.File), true, loop: true); ClearFailure(track.File); }
        catch (Exception error) when (error is IOException or InvalidDataException)
        { draftPlayback = false; MarkFailed(track, error.Message); SetDraftStatus("BP 音乐不可用，本轮已跳过"); }
    }
    private void StopDraftPlayback()
    {
        if (!draftPlayback) return;
        Audio.Stop(); draftPlayback = false;
    }
    private void SetDraftStatus(string title) { DraftStatus = title; Changed?.Invoke(); }
    public void SetStatus(string title, string detail) { Status = title; Detail = detail; Changed?.Invoke(); }
    public void Dispose()
    {
        lifetime.Cancel(); client.Dispose(); draftClient.Dispose(); Audio.Dispose();
        if (polling is null || polling.IsCompleted) lifetime.Dispose();
        else _ = polling.ContinueWith(task => { _ = task.Exception; lifetime.Dispose(); }, TaskScheduler.Default);
    }
}
