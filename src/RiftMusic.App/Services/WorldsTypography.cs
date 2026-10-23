using System.Windows;
using System.Windows.Media;

namespace RiftMusic.App.Services;

/// <summary>Load the licensed original families when available, with explicitly documented style alternatives.</summary>
public static class WorldsTypography
{
    private static readonly Uri fontBase=new("pack://application:,,,/RiftMusic;component/Assets/Fonts/");
    private static FontFamily Embedded(string family) => new(fontBase, "./#" + family);
    public static string Configure(ResourceDictionary resources, string fontDirectory, bool includeSystemFonts=true)
    {
        var available = new List<FontFamily>();
        if (Directory.Exists(fontDirectory))
        {
            try { available.AddRange(Fonts.GetFontFamilies(new Uri(Path.GetFullPath(fontDirectory) + Path.DirectorySeparatorChar))); }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or FormatException) { }
        }
        if(includeSystemFonts) available.AddRange(Fonts.SystemFontFamilies);
        FontFamily? Original(string name) => available.FirstOrDefault(f => f.FamilyNames.Values.Append(f.Source).Any(n => n.Contains(name,StringComparison.OrdinalIgnoreCase)));
        var manuka=Original("Manuka");var sans=Original("Fraktion Sans");var mono=Original("Fraktion Mono");
        // The Windows legacy family identifies this face directly; the typographic
        // family with Normal stretch can otherwise resolve to Barlow Regular.
        resources["WorldsDisplayFont"]=manuka??Embedded("Teko");
        resources["WorldsSansFont"]=sans??Embedded("Barlow");
        resources["WorldsMonoFont"]=mono??Embedded("IBM Plex Mono");
        resources["ChineseDisplayFont"]=Embedded("Alimama ShuHeiTi");
        // WPF's ordered family list uses the first Latin face and the embedded
        // Chinese family for glyphs that the Latin face does not contain.
        var sansReference=sans is null?"./#Barlow":sans.BaseUri is not null && sans.Source.Contains('#')
            ? new Uri(new Uri(sans.BaseUri.AbsoluteUri),sans.Source).ToString():sans.Source;
        var body=new FontFamily(fontBase,sansReference+", Microsoft YaHei UI, ./#Source Han Sans CN");
        resources["MusicFont"]=body;
        return $"标题：{(manuka is null?"Teko Bold · 免费窄体替代":"Manuka · 本地字库")}\n辅助：{(sans is null?"Barlow · 风格替代":"Fraktion Sans · 本地字库")} / {(mono is null?"IBM Plex Mono · 风格替代":"Fraktion Mono · 本地字库")}\n中文标题：阿里妈妈数黑体 · 风格替代\n中文正文：微软雅黑 UI / 思源备用";
    }
}
