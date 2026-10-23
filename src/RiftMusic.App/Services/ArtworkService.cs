using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RiftMusic.App.Services;

public static class ArtworkService
{
    private static readonly Dictionary<string, ImageSource> cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> exact = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> themes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> themeYears = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> categories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> worldsYears = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> draftYears = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> logos = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> legacyHashes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, (long Length, long Changed, bool Legacy)> thumbnailChecks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, (long Length, long Changed)> decodedVersions = new(StringComparer.OrdinalIgnoreCase);
    private static BrowseSelection browsing = new();
    private static string? artworkDirectory;

    static ArtworkService()
    {
        using var source = typeof(ArtworkService).Assembly.GetManifestResourceStream("RiftMusic.artwork.json");
        if (source is null) return;
        using var json = JsonDocument.Parse(source);
        LoadMap(json.RootElement,"legacy_thumbnail_sha256",legacyHashes);
        if (!json.RootElement.TryGetProperty("recommended_mapping", out var mapping)) return;
        LoadMap(mapping, "exact_track_ids", exact); LoadMap(mapping, "themes", themes);
        LoadMap(mapping, "theme_years", themeYears); LoadMap(mapping, "category_fallbacks", categories);
        LoadMap(mapping,"worlds_years",worldsYears);LoadMap(mapping,"draft_years",draftYears);LoadMap(mapping,"competition_logos",logos);
    }
    private static void LoadMap(JsonElement root, string key, Dictionary<string, string> destination)
    {
        if (!root.TryGetProperty(key, out var values)) return;
        foreach (var value in values.EnumerateObject()) if (value.Value.GetString() is { } file) destination[value.Name] = file;
    }
    public static void Configure(string dataDirectory)
    { artworkDirectory = Path.Combine(dataDirectory, "Artwork"); cache.Clear(); thumbnailChecks.Clear(); decodedVersions.Clear(); }

    public static ImageSource Get(Track? track,DraftUse? chosenUse=null)
    {
        if (track is null) return Category("all");
        // Legacy BP covers are publisher video thumbnails, not tournament artwork.
        // Keep them on disk as provenance; a user-supplied cover still takes priority.
        var legacyThumbnail=IsLegacyThumbnail(track);
        if (!legacyThumbnail && SafeFile(track.CoverFile) && artworkDirectory is not null)
        {
            var local = Path.Combine(artworkDirectory, track.CoverFile);
            if (File.Exists(local) && TryLoad(local, out var image)) return image;
        }
        if(track.Category=="worlds" && worldsYears.TryGetValue(track.Year.ToString(),out var yearlyWorlds))return Embedded(yearlyWorlds);
        if(track.Category=="worlds" && track.Year>0)return AnnualLogo(new(){Competition="Worlds",Year=track.Year});
        if(track.Category=="draft")return DraftArtwork(chosenUse??browsing.RepresentativeUse(track)??track.DraftUses.OrderByDescending(u=>u.Year).FirstOrDefault());
        if (exact.TryGetValue(track.Id, out var exactFile)) return Embedded(exactFile);
        // A title word is not evidence of a skin, season or tournament association.
        // Annual theme artwork is used only for the explicitly verified year.
        if (track.Category is "skins" or "events")
        {
            if (themeYears.TryGetValue($"{track.Theme}|{track.Year}", out var yearly)) return Embedded(yearly);
            if (themes.TryGetValue(track.Theme, out var themed)) return Embedded(themed);
        }
        return Category(track.Category);
    }
    private static bool IsLegacyThumbnail(Track track)
    {
        if(track.Category!="draft" || artworkDirectory is null || !SafeFile(track.CoverFile) || !legacyHashes.TryGetValue(track.CoverFile,out var expected))return false;
        var file=Path.Combine(artworkDirectory,track.CoverFile);
        try {
            var info=new FileInfo(file);if(!info.Exists)return false;
            var changed=info.LastWriteTimeUtc.Ticks;
            if(thumbnailChecks.TryGetValue(file,out var known) && known.Length==info.Length && known.Changed==changed)return known.Legacy;
            // Replacing a user's cover invalidates both its provenance and decoded image.
            cache.Remove(file);
            using var stream=File.OpenRead(file);
            var legacy=Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.OrdinalIgnoreCase);
            thumbnailChecks[file]=(info.Length,changed,legacy);return legacy;
        }
        catch(IOException){return false;}
        catch(UnauthorizedAccessException){return false;}
    }
    public static ImageSource Category(string category) => category=="worlds"?AnnualLogo(new(){Competition="Worlds",Year=0}):categories.TryGetValue(category, out var file)
        ? Embedded(file) : Neutral(category);
    public static ImageSource Worlds22Theme() => Embedded("Worlds22Theme.jpg");
    public static ImageSource Atmosphere(bool draft) => Embedded(draft?"draft-atmosphere-v054.png":"opening-atmosphere-v054.png");
    public static void SetBrowsing(BrowseSelection selection) => browsing=selection.Clone();
    public static ImageSource? CompetitionLogo(string competition) => logos.TryGetValue(competition,out var file)?Embedded(file):null;
    public static ImageSource DraftArtwork(DraftUse? use)
    {
        if(use is null || use.Competition=="all")return Category("draft");
        if(draftYears.TryGetValue($"{use.Competition}|{use.Year}",out var annual))return Embedded(annual);
        // A verified logo and explicit year is preferable to an unrelated promotional picture.
        return AnnualLogo(use);
    }
    private static ImageSource AnnualLogo(DraftUse use)
    {
        var key=$"annual:{use.Competition}:{use.Year}";
        if(cache.TryGetValue(key,out var cached))return cached;
        var drawing=new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(24,24,24)),null,new RectangleGeometry(new Rect(0,0,160,100))));
        if(CompetitionLogo(use.Competition) is {} logo)drawing.Children.Add(new ImageDrawing(logo,new Rect(48,10,64,52)));
        var label=use.Year>0?use.Year.ToString():use.Competition switch {"Worlds"=>"WORLDS","FirstStand"=>"FST",_=>use.Competition};
        var text=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),20,new SolidColorBrush(Color.FromRgb(242,242,242)),1);
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(242,242,242)),null,text.BuildGeometry(new Point((160-text.Width)/2,68))));
        drawing.Freeze();var result=new DrawingImage(drawing);result.Freeze();cache[key]=result;return result;
    }
    public static string Description(Track track)
    {
        if(!IsLegacyThumbnail(track) && artworkDirectory is not null && SafeFile(track.CoverFile) && File.Exists(Path.Combine(artworkDirectory,track.CoverFile)))return "用户提供封面";
        if(track.Category=="draft") {
            var use=browsing.RepresentativeUse(track)??track.DraftUses.OrderByDescending(u=>u.Year).FirstOrDefault();
            return use is null?"BP 音乐 · 尚无赛事记录":$"{(use.Competition=="FirstStand"?"全球先锋赛":use.Competition)} {use.Year} · {(draftYears.TryGetValue($"{use.Competition}|{use.Year}",out var art)?art.Contains("-theme")?"当年世界赛皮肤主题图":"官方赛事主视觉":"赛事标志与年份")}";
        }
        return track.Category=="worlds"&&worldsYears.ContainsKey(track.Year.ToString())?$"全球总决赛 {track.Year} · 当年冠军 / 世界赛皮肤主题图":track.ThemeLabel;
    }
    public static string YearLabel(Track track)
    {
        var year=track.Category=="draft"?(browsing.RepresentativeUse(track)??track.DraftUses.OrderByDescending(u=>u.Year).FirstOrDefault())?.Year??0:track.Year;
        return year>0?year.ToString(CultureInfo.InvariantCulture):"";
    }
    private static bool SafeFile(string? file) => !string.IsNullOrWhiteSpace(file) && Path.GetFileName(file) == file
        && !file.Contains(':') && !file.Contains('/') && !file.Contains('\\')
        && new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
    private static ImageSource Embedded(string file)
    {
        if (TryLoad($"pack://application:,,,/RiftMusic;component/Assets/Art/{file}", out var image)) return image;
        return Neutral("all");
    }
    private static ImageSource Neutral(string category)
    {
        var key = "neutral:" + category;
        if (cache.TryGetValue(key, out var image)) return image;
        var accent = category switch
        {
            "draft" => Color.FromRgb(92, 90, 160),
            "worlds" => Color.FromRgb(161, 127, 58),
            "skins" => Color.FromRgb(123, 92, 158),
            "events" => Color.FromRgb(60, 123, 128),
            "seasons" => Color.FromRgb(90, 119, 153),
            "favorites" => Color.FromRgb(176, 93, 116),
            _ => Color.FromRgb(67, 123, 129)
        };
        var drawing = new DrawingGroup();
        var background = new LinearGradientBrush(Color.FromRgb(244, 248, 249), Color.FromRgb(226, 237, 239), 30);
        const double side = 320, glyphSize = 96;
        drawing.Children.Add(new GeometryDrawing(background, null, new RectangleGeometry(new Rect(0, 0, side, side))));
        var resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/RiftMusic;component/Assets/FluentIcons.xaml") };
        var icon = resources[category is "draft" or "worlds" ? "FluentTrophy" : category == "favorites" ? "FluentHeart" : "FluentMusic"] as Geometry;
        if (icon is not null)
        {
            var glyph = icon.Clone();
            var bounds = glyph.Bounds;
            var scale = glyphSize / Math.Max(bounds.Width, bounds.Height);
            var left = (side - bounds.Width * scale) / 2 - bounds.Left * scale;
            var top = (side - bounds.Height * scale) / 2 - bounds.Top * scale;
            glyph.Transform = new MatrixTransform(scale, 0, 0, scale, left, top);
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(accent), null, glyph));
        }
        drawing.Freeze(); image = new DrawingImage(drawing); image.Freeze();
        if (cache.Count >= 128) cache.Clear(); cache[key] = image; return image;
    }
    private static bool TryLoad(string location, out ImageSource result)
    {
        try
        {
            var uri=new Uri(location,UriKind.Absolute);
            if(uri.IsFile) {
                var info=new FileInfo(uri.LocalPath);if(!info.Exists){cache.Remove(location);decodedVersions.Remove(location);result=null!;return false;}
                var version=(info.Length,info.LastWriteTimeUtc.Ticks);
                if(!decodedVersions.TryGetValue(location,out var known) || known!=version)cache.Remove(location);
                decodedVersions[location]=version;
            }
            if(cache.TryGetValue(location,out result!))return true;
            using var metadataStream=location.StartsWith("pack:",StringComparison.OrdinalIgnoreCase)
                ? Application.GetResourceStream(new Uri(location))?.Stream : File.OpenRead(location);
            if(metadataStream is null){result=null!;return false;}
            var frame=BitmapDecoder.Create(metadataStream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None).Frames[0];
            var target=location.Contains("/logo-",StringComparison.OrdinalIgnoreCase)?192:768;
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            if(uri.IsFile)bitmap.CreateOptions=BitmapCreateOptions.IgnoreImageCache;
            bitmap.DecodePixelWidth=Math.Min(frame.PixelWidth,target);
            bitmap.UriSource = new Uri(location, UriKind.Absolute); bitmap.EndInit(); bitmap.Freeze();
            if (cache.Count >= 128) cache.Clear(); cache[location] = result = bitmap; return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FileFormatException or NotSupportedException or ArgumentException or InvalidOperationException)
        { result = null!; return false; }
    }
}

public sealed class ArtworkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ArtworkService.Get(value as Track);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class ArtworkDescriptionConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is Track track?ArtworkService.Description(track):"";
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class ArtworkYearConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is Track track?ArtworkService.YearLabel(track):"";
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
