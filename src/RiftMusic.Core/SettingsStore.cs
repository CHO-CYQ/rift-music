using System.Text.Json;
using System.Text.Json.Serialization;
namespace RiftMusic.Core;
public sealed class SettingsStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string? Warning { get; private set; }
    public AppSettings Load()
    {
        Warning = null;
        var path = Path.Combine(DirectoryPath, "settings.json");
        if (!System.IO.File.Exists(path)) return new();
        try { return Read(path); }
        catch (Exception e) when (e is JsonException or IOException or ArgumentException or NotSupportedException)
        {
            Warning = "设置无法读取，已尝试恢复上次有效备份。";
            try { System.IO.File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")); } catch (IOException) { }
            try { var restored = Read(path + ".bak"); System.IO.File.Copy(path + ".bak", path, true); return restored; }
            catch (Exception backup) when (backup is IOException or JsonException or ArgumentException or NotSupportedException)
            { Warning = "设置和备份无法读取，使用默认设置；原文件已保留。"; return new(); }
        }
    }
    private static AppSettings Read(string path)
    {
        var data = JsonSerializer.Deserialize<AppSettings>(System.IO.File.ReadAllText(path), Options) ?? throw new JsonException("空设置");
        if (data.Version != 1) throw new NotSupportedException("不支持的设置版本");
        data.Volume = double.IsFinite(data.Volume) ? Math.Clamp(data.Volume, 0, 1) : .35;
        data.Tracks ??= []; data.Sessions ??= [];
        data.Tracks.RemoveAll(t => t is null);
        if (data.Tracks.Any(t => !double.IsFinite(t.Duration) || !double.IsFinite(t.Start) || !double.IsFinite(t.End)
            || !double.IsFinite(t.FadeIn) || !double.IsFinite(t.FadeOut))) throw new JsonException("音频设置含无效数字");
        data.Sessions.RemoveAll(s => s is null || string.IsNullOrWhiteSpace(s.RosterKey) || s.StartedAt < DateTimeOffset.UtcNow.AddHours(-24));
        foreach (var t in data.Tracks) if (!t.IsValid()) t.Enabled = false;
        if (!Enum.IsDefined(data.Mode)) data.Mode = SelectionMode.Fixed;
        if (!Enum.IsDefined(data.DraftMode)) data.DraftMode = SelectionMode.Random;
        if (!MusicLibrary.Categories.Any(c => c.Id == data.RandomScope)) data.RandomScope = "all";
        data.DraftCompetition = MusicLibrary.DraftCompetitions.FirstOrDefault(c => c.Equals(data.DraftCompetition, StringComparison.OrdinalIgnoreCase)) ?? "all";
        if (data.DraftYear < 0 || data.DraftYear > 9999) data.DraftYear = 0;
        data.LeagueClientDirectory ??= "";
        foreach (var t in data.Tracks)
        {
            if (!MusicLibrary.Categories.Skip(2).Any(c => c.Id == t.Category)) t.Category = "custom";
            t.Theme ??= ""; t.Quality ??= ""; t.SearchTags ??= ""; t.Artist ??= ""; t.CoverFile ??= "";
            t.DraftUses ??= []; t.DraftUses.RemoveAll(u => u is null);
            foreach (var use in t.DraftUses)
            {
                use.Competition = MusicLibrary.DraftCompetitions.FirstOrDefault(c => c.Equals(use.Competition, StringComparison.OrdinalIgnoreCase)) ?? use.Competition ?? "";
                use.Stage ??= ""; use.Evidence ??= "";
            }
        }
        return data;
    }
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, "settings.json"); var temp = path + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(settings, Options);
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
        // Keep the previous valid contents, then commit by same-directory rename.
        // Windows ReplaceFile can fail in restricted environments even when ordinary writes succeed.
        if (System.IO.File.Exists(path)) System.IO.File.Copy(path, path + ".bak", true);
        System.IO.File.Move(temp, path, true);
    }
    public string ResolveMusic(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Contains('/') || relative.Contains('\\'))
            throw new ArgumentException("音乐文件名不合法");
        var root = Path.GetFullPath(Path.Combine(DirectoryPath, "Music")) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(root, relative));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("音乐路径超出目录");
        return resolved;
    }
}
