global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using RiftMusic.Core;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RiftMusic.App.Services;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application();
        var failures = new List<string>(); var passed = 0;
        void Check(bool condition, string label)
        { if (condition) { passed++; Console.WriteLine("PASS " + label); } else { failures.Add(label); Console.WriteLine("FAIL " + label); } }
        string Asset(Track track) => (ArtworkService.Get(track) as BitmapImage)?.UriSource?.ToString() ?? "neutral";
        foreach (var draft in new[] { false, true })
            Check(ArtworkService.Atmosphere(draft) is BitmapImage atmosphere && atmosphere.PixelWidth <= 768 && Math.Abs(atmosphere.Width / atmosphere.Height - 2) < .01,
                "generic AI atmosphere decodes at display size with its original uncropped proportions: " + (draft ? "BP" : "opening"));
        Check(ArtworkService.Category("worlds") is DrawingImage, "Worlds category uses its official logo instead of a photograph from an unrelated year");
        Check(ArtworkService.Category("draft") is DrawingImage, "BP all competitions has neutral tournament artwork rather than PROJECT Jhin");
        Check(ReferenceEquals(ArtworkService.DraftArtwork(new(){Competition="all",Year=2026}),ArtworkService.Category("draft")),"automatic all-competition BP range has neutral art instead of an incorrect Worlds label");
        Check(ArtworkService.Category("all") is DrawingImage, "all opening music has neutral music artwork rather than Aurelion Sol");
        foreach (var category in new[] { "all", "draft", "favorites", "skins" })
        {
            var image = (DrawingImage)ArtworkService.Category(category);
            var group = (DrawingGroup)image.Drawing;
            var icon = group.Children.OfType<GeometryDrawing>().Last();
            var bounds = icon.Geometry.Bounds;
            Check(Math.Abs(image.Width - image.Height) < .001, "neutral cover is square for square album slots: " + category);
            Check(Math.Abs(bounds.X + bounds.Width / 2 - image.Width / 2) < .01
                && Math.Abs(bounds.Y + bounds.Height / 2 - image.Height / 2) < .01
                && Math.Max(bounds.Width, bounds.Height) >= 90,
                "neutral glyph is centered and readable in album slots: " + category);
        }
        Check(Asset(new Track { Id="unknown", Category="skins", Name="不是 DJ娑娜", Theme="用户音乐" }) == "neutral", "track title substrings cannot assign an unrelated skin");
        Check(Asset(new Track { Id="unknown", Category="custom", Theme="DJ娑娜" }) == "neutral", "unverified imported category cannot inherit themed skin imagery");
        Check(Asset(new Track { Id="unknown", Category="skins", Theme="星之守护者", Year=2019 }).EndsWith("/star-guardian-rakan.jpg"), "Star Guardian 2019 gets a 2019 skin");
        Check(Asset(new Track { Id="unknown", Category="skins", Theme="星之守护者", Year=2022 }).EndsWith("/star-guardian-kaisa.jpg"), "Star Guardian 2022 gets a 2022 skin");
        Check(Asset(new Track { Id="unknown", Category="skins", Theme="星之守护者", Year=2024 }) == "neutral", "unverified annual theme does not borrow another year's artwork");
        Check(Asset(new Track { Id="official-theme-darkstar-chogath-2018", Category="skins" }).EndsWith("/dark-star-chogath.jpg"), "Cho'Gath is not represented by Dark Cosmic Jhin");
        Check(Asset(new Track { Id="official-theme-jhin", Category="skins", Theme="英雄主题" }).EndsWith("/champion-jhin.jpg"), "base Jhin music uses base Jhin");
        Check(Asset(new Track { Id="worlds-2025-sacrifice", Category="worlds", Year=2025 }).EndsWith("/worlds-2025-theme.jpg"), "Worlds 2025 uses its official year artwork");
        Check(ArtworkService.Get(new Track { Id="unknown", Category="worlds", Theme="DJ娑娜" }) is DrawingImage, "Worlds never inherits an unrelated skin from its theme field");

        for(var year=2012;year<=2025;year++)Check(Asset(new Track{Category="worlds",Year=year}).EndsWith($"/worlds-{year}-theme.jpg"),"opening uses verified annual World Championship theme: "+year);
        foreach(var competition in MusicLibrary.DraftCompetitions)Check(ArtworkService.CompetitionLogo(competition) is BitmapImage,"official competition logo decodes: "+competition);
        var multiUse=new Track{Category="draft",DraftUses=[new(){Competition="LPL",Year=2024},new(){Competition="Worlds",Year=2025}]};
        var view=new BrowseSelection();view.Toggle(new("competition:Worlds",2025),true,[]);ArtworkService.SetBrowsing(view);
        Check(Asset(multiUse).EndsWith("/worlds-2025-theme.jpg"),"BP art follows the selected verified event and year using Worlds 2025 Xin Zhao");
        multiUse.Year=2026;
        Check(ArtworkService.YearLabel(multiUse)=="2025","BP cover badge follows verified use rather than a different release year");
        Check(ArtworkService.Get(multiUse,new(){Competition="LPL",Year=2024}) is DrawingImage,"automatic use overrides browsing artwork without crossing competition years");
        view.Clear();view.Toggle(new("competition:LPL",2024),true,[]);ArtworkService.SetBrowsing(view);
        Check(ArtworkService.Get(multiUse) is DrawingImage && ArtworkService.Description(multiUse).Contains("LPL 2024"),"missing annual poster uses the correct logo and year rather than a Worlds poster");
        Check(ArtworkService.YearLabel(multiUse)=="2024","cover year badge updates with the selected competition pair");
        Check(ArtworkService.YearLabel(new(){Category="draft",Year=2026})=="","BP without a verified use cannot invent an event year badge");
        ArtworkService.SetBrowsing(new());
        var customDirectory=Path.Combine(Path.GetTempPath(),"rift-custom-art-"+Guid.NewGuid());Directory.CreateDirectory(Path.Combine(customDirectory,"Artwork"));
        try {
            var customPath=Path.Combine(customDirectory,"Artwork","bp-worlds2022-cant-stop.jpg");
            var customImage=new RenderTargetBitmap(16,16,96,96,PixelFormats.Pbgra32);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(customImage));using(var file=File.Create(customPath))encoder.Save(file);
            ArtworkService.Configure(customDirectory);
            var customTrack=new Track{Id="bp-worlds2022-cant-stop",Category="draft",CoverFile="bp-worlds2022-cant-stop.jpg",DraftUses=[new(){Competition="Worlds",Year=2022}]};
            Check(ArtworkService.Get(customTrack) is BitmapImage customBitmap && customBitmap.UriSource.IsFile && ArtworkService.Description(customTrack)=="用户提供封面","replacing a legacy thumbnail keeps the user's new artwork even with the same filename");
        } finally {ArtworkService.Configure("");Directory.Delete(customDirectory,true);}
        var family = new FontFamily(new Uri("pack://application:,,,/RiftMusic;component/Assets/Fonts/"), "./#Source Han Sans CN");
        var regular = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var medium = new Typeface(family, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
        Check(regular.TryGetGlyphTypeface(out var regularGlyph) && regularGlyph.FontUri.ToString().Contains("SourceHanSansCN-Regular.otf", StringComparison.OrdinalIgnoreCase), "embedded regular font resolves without a system font installation");
        Check(medium.TryGetGlyphTypeface(out var mediumGlyph) && mediumGlyph.FontUri.ToString().Contains("SourceHanSansCN-Medium.otf", StringComparison.OrdinalIgnoreCase), "embedded medium font resolves to the actual medium face");
        Console.WriteLine("Regular font URI: " + regularGlyph?.FontUri);
        Console.WriteLine("Medium font URI: " + mediumGlyph?.FontUri);
        Check(regularGlyph is not null && "全球总决赛开场赛事设置音乐".All(c => regularGlyph.CharacterToGlyphMap.ContainsKey(c)), "embedded font contains the Chinese navigation characters");
        using (var source = typeof(ArtworkService).Assembly.GetManifestResourceStream("RiftMusic.artwork.json")!)
        using (var manifest = JsonDocument.Parse(source))
        {
            foreach (var asset in manifest.RootElement.GetProperty("assets").EnumerateArray())
            {
                var file = asset.GetProperty("file").GetString();
                var bitmap = new BitmapImage(new Uri($"pack://application:,,,/RiftMusic;component/Assets/Art/{file}"));
                Check(bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0, "official embedded image decodes: " + file);
            }
        }

        if (args.Length > 0)
        {
            ArtworkService.Configure(args[0]);
            using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(args[0], "settings.json")));
            var tracks = settings.RootElement.EnumerateObject().First(p => p.Name.Equals("tracks", StringComparison.OrdinalIgnoreCase)).Value;
            foreach (var item in tracks.EnumerateArray())
            {
                var track = JsonSerializer.Deserialize<Track>(item.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;
                var artwork = ArtworkService.Get(track);
                Check(artwork.Width > 0 && artwork.Height > 0, "catalog artwork decodes: " + track.Id);
                if (!string.IsNullOrWhiteSpace(track.CoverFile) && !(track.Category=="draft" && track.CoverFile==track.Id+".jpg"))
                    Check(artwork is BitmapImage bitmap && bitmap.UriSource.IsFile, "existing local publisher cover retains priority: " + track.Id);
            }
        }
        Console.WriteLine($"Artwork checks: {passed} passed, {failures.Count} failed.");
        app.Shutdown(); return failures.Count == 0 ? 0 : 1;
    }
}
