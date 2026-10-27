using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace RiftMusic.Core;

public sealed class MusicPack
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Asset { get; set; } = "";
    public long Bytes { get; set; }
    public string Sha256 { get; set; } = "";
    public List<Track> Tracks { get; set; } = [];
    public List<MusicPackFile> Files { get; set; } = [];
    public string Url => "https://github.com/CHO-CYQ/rift-music/releases/download/v0.1/" + Uri.EscapeDataString(Asset);
    public int InstalledCount(AppSettings settings, SettingsStore store) => Tracks.Count(t => settings.Tracks.Any(x => x.Id == t.Id && File.Exists(store.ResolveMusic(x.File))));
    internal static bool HashValid(string hash) => hash.Length == 64 && hash.All(Uri.IsHexDigit);
    internal static string SafePath(string root, string path)
    {
        var parts=path.Split('/');
        if(parts.Length!=2 || parts[0] is not ("Music" or "Artwork") || string.IsNullOrWhiteSpace(parts[1])
            || parts[1] is "." or ".." || parts[1].IndexOfAny(['\\',':','\0'])>=0 || Path.GetFileName(parts[1])!=parts[1])
            throw new InvalidDataException("曲包包含不安全的文件路径。");
        var allowed=parts[0]=="Music"?new[]{".mp3",".wav"}:new[]{".png",".jpg",".jpeg",".webp"};
        if(!allowed.Contains(Path.GetExtension(parts[1]),StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("曲包包含不支持的文件。");
        return Path.Combine(root,parts[0],parts[1]);
    }
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Id)||string.IsNullOrWhiteSpace(Name)||Bytes<=0||Bytes>1_073_741_824||!HashValid(Sha256)
            ||Asset!=Path.GetFileName(Asset)||Asset.Contains(':')||Asset.Contains('\\')||!Asset.EndsWith(".zip",StringComparison.Ordinal))
            throw new InvalidDataException("曲包目录无效。");
        if(Tracks.Count is <1 or >500||Files.Count is <1 or >1000||Tracks.Select(t=>t.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Tracks.Count
            ||Files.Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Files.Count)throw new InvalidDataException("曲包存在重复或无效条目。");
        foreach(var f in Files){SafePath(Path.GetTempPath(),f.Path);if(f.Bytes<=0||f.Bytes>536_870_912||!HashValid(f.Sha256))throw new InvalidDataException("曲包文件信息无效。");}
        if(Files.Sum(f=>f.Bytes)>1_073_741_824)throw new InvalidDataException("曲包解压大小过大。");
        foreach(var t in Tracks)
            if(!t.IsValid()||!Files.Any(f=>f.Path=="Music/"+t.File)
                ||(!string.IsNullOrEmpty(t.CoverFile)&&!Files.Any(f=>f.Path=="Artwork/"+t.CoverFile)))throw new InvalidDataException("曲包的曲目信息不完整。");
    }
}
public sealed class MusicPackFile
{
    public string Path { get; set; } = "";
    public long Bytes { get; set; }
    public string Sha256 { get; set; } = "";
}

public static class MusicPackInstaller
{
    public static async Task<PreparedMusicPack> PrepareAsync(string archive,MusicPack pack,SettingsStore store,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();pack.Validate();
        if(new FileInfo(archive).Length!=pack.Bytes)throw new InvalidDataException("曲包不完整，文件大小与目录不符。请重新下载。");
        await using(var input=File.OpenRead(archive))
            if(!Convert.ToHexStringLower(await SHA256.HashDataAsync(input,cancel)).Equals(pack.Sha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("曲包校验未通过，请从本项目 Release 重新下载。");
        var stage=Path.Combine(store.DirectoryPath,"Downloads","install-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var prepared=new PreparedMusicPack(stage,pack);
        try
        {
            using var zip=ZipFile.OpenRead(archive);
            if(zip.Entries.Count!=pack.Files.Count||zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=zip.Entries.Count)
                throw new InvalidDataException("曲包文件数量不符。");
            foreach(var entry in zip.Entries)
            {
                cancel.ThrowIfCancellationRequested();
                var target=MusicPack.SafePath(stage,entry.FullName);
                var expected=pack.Files.SingleOrDefault(f=>f.Path==entry.FullName)??throw new InvalidDataException("曲包包含未声明的文件。");
                if(entry.Length!=expected.Bytes)throw new InvalidDataException("曲包文件大小不符。");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input=entry.Open();await using var output=File.Create(target);
                using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer=new byte[81920];long size=0;int n;
                while((n=await input.ReadAsync(buffer,cancel))>0)
                {size+=n;if(size>expected.Bytes)throw new InvalidDataException("曲包解压大小异常。");hash.AppendData(buffer,0,n);await output.WriteAsync(buffer.AsMemory(0,n),cancel);}
                if(size!=expected.Bytes||!Convert.ToHexStringLower(hash.GetHashAndReset()).Equals(expected.Sha256,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("曲包中的文件校验未通过。");
            }
            foreach(var f in pack.Files)
            {
                cancel.ThrowIfCancellationRequested();
                var path=MusicPack.SafePath(store.DirectoryPath,f.Path);
                if(!File.Exists(path))continue;
                await using var input=File.OpenRead(path);
                if(new FileInfo(path).Length!=f.Bytes||!Convert.ToHexStringLower(await SHA256.HashDataAsync(input,cancel)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase))
                    throw new IOException("已有同名文件与曲包不同，已保留原文件："+Path.GetFileName(path));
                prepared.VerifiedExisting[path]=(new FileInfo(path).Length,File.GetLastWriteTimeUtc(path));
            }
            return prepared;
        }
        catch{prepared.Dispose();throw;}
    }
}

public sealed class PreparedMusicPack(string stage,MusicPack pack):IDisposable
{
    internal Dictionary<string,(long Length,DateTime Changed)> VerifiedExisting { get; }=new(StringComparer.OrdinalIgnoreCase);
    public void Install(SettingsStore store,AppSettings settings)
    {
        var added=new List<string>();var before=settings.Tracks.ToList();
        try
        {
            // Preflight every destination before modifying the user's library.
            foreach(var f in pack.Files)
            {
                var path=MusicPack.SafePath(store.DirectoryPath,f.Path);
                if(File.Exists(path))
                {
                    if(!VerifiedExisting.TryGetValue(path,out var verified)||new FileInfo(path).Length!=verified.Length||File.GetLastWriteTimeUtc(path)!=verified.Changed)
                        throw new IOException("已有文件在校验后发生变化，请重试；原文件已保留："+Path.GetFileName(path));
                }
            }
            foreach(var f in pack.Files)
            {
                var path=MusicPack.SafePath(store.DirectoryPath,f.Path);
                if(File.Exists(path))continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Move(MusicPack.SafePath(stage,f.Path),path);added.Add(path);
            }
            foreach(var track in pack.Tracks.Where(t=>!settings.Tracks.Any(x=>x.Id==t.Id)))
            {
                var copy=JsonSerializer.Deserialize<Track>(JsonSerializer.Serialize(track))!;
                copy.Favorite=false;copy.Enabled=true;settings.Tracks.Add(copy);
            }
            store.Save(settings);
        }
        catch
        {
            settings.Tracks.Clear();settings.Tracks.AddRange(before);
            foreach(var file in added)try{File.Delete(file);}catch(IOException){}
            throw;
        }
    }
    public void Dispose(){try{if(Directory.Exists(stage))Directory.Delete(stage,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
