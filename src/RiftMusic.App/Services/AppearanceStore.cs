using System.Text.Json;

namespace RiftMusic.App.Services;

public sealed class AppearanceOptions { public bool MotionEnabled { get; set; } = true; }
public sealed class AppearanceStore(string directory)
{
    private readonly string file = Path.Combine(directory,"appearance.json");
    public AppearanceOptions Load()
    {
        try { return File.Exists(file)?JsonSerializer.Deserialize<AppearanceOptions>(File.ReadAllText(file))??new():new(); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save(AppearanceOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary=file+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(options,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(temporary,file,true);
    }
}
