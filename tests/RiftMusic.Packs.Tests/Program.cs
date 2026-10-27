using System.IO.Compression;
using System.Security.Cryptography;
using RiftMusic.Core;

int passed=0;
void Check(bool ok) {if(!ok)throw new Exception("Assertion failed");}
void Reject(Action action) {try{action();}catch(Exception e) when(e is InvalidDataException or IOException or ArgumentException){return;}throw new Exception("Invalid package accepted");}
void Test(string name,Action<Fixture> body) {using var f=new Fixture();body(f);Console.WriteLine("PASS "+name);passed++;}
Test("valid package merges without replacing personal settings or existing metadata",f=>{
 f.Settings.Tracks.Add(new(){Id="a",Name="我的片段",File="a.wav",Duration=10,End=5,Favorite=true});
 f.Settings.Volume=.63;f.Settings.AutoPlay=true;
 f.Install();Check(f.Settings.Tracks.Count==1&&f.Settings.Tracks[0].Name=="我的片段"&&f.Settings.Tracks[0].Favorite);
 Check(f.Settings.Volume==.63&&f.Settings.AutoPlay&&File.Exists(f.Store.ResolveMusic("a.wav")));
 f.Install();Check(f.Settings.Tracks.Count==1);
});
Test("new tracks receive catalog metadata",f=>{f.Install();Check(f.Settings.Tracks.Single().Name=="音轨"&&f.Store.Load().Tracks.Count==1);});
Test("tampered archive leaves library untouched",f=>{File.AppendAllText(f.Zip,"corrupt");Reject(f.Install);Check(f.Settings.Tracks.Count==0&&!File.Exists(f.Store.ResolveMusic("a.wav")));});
Test("unsafe catalog paths are rejected",f=>{f.Pack.Files[0].Path="Music/../escape.wav";Reject(f.Install);Check(f.Settings.Tracks.Count==0);});
Test("duplicate catalog entries are rejected",f=>{f.Pack.Files.Add(f.Pack.Files[0]);Reject(f.Install);});
Test("unexpected zip entry is rejected even with matching archive digest",f=>{using(var z=ZipFile.Open(f.Zip,ZipArchiveMode.Update)){using var w=new StreamWriter(z.CreateEntry("../outside").Open());w.Write("bad");}f.Rehash();Reject(f.Install);});
Test("existing different file is preserved",f=>{Directory.CreateDirectory(Path.GetDirectoryName(f.Store.ResolveMusic("a.wav"))!);File.WriteAllText(f.Store.ResolveMusic("a.wav"),"personal");Reject(f.Install);Check(File.ReadAllText(f.Store.ResolveMusic("a.wav"))=="personal"&&f.Settings.Tracks.Count==0);});
Test("save failure rolls back new tracks and files",f=>{f.Store.Save(f.Settings);Directory.CreateDirectory(Path.Combine(f.Store.DirectoryPath,"settings.json.bak"));Reject(f.Install);Check(f.Settings.Tracks.Count==0&&!File.Exists(f.Store.ResolveMusic("a.wav"))&&f.Store.Load().Tracks.Count==0);});
Test("cancelled preparation installs nothing",f=>{var c=new CancellationToken(true);try{using var p=MusicPackInstaller.PrepareAsync(f.Zip,f.Pack,f.Store,c).GetAwaiter().GetResult();throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}Check(f.Settings.Tracks.Count==0);});
Test("downloaded bytes are independently verified before installation",f=>{using var client=new HttpClient(new Reply(File.ReadAllBytes(f.Zip)));var path=Path.Combine(f.Root,"download.zip");MusicPackDownloader.DownloadAsync(client,f.Pack,path,null,CancellationToken.None).GetAwaiter().GetResult();using var prepared=MusicPackInstaller.PrepareAsync(path,f.Pack,f.Store,CancellationToken.None).GetAwaiter().GetResult();prepared.Install(f.Store,f.Settings);Check(f.Settings.Tracks.Count==1);});
Test("truncated response cannot become an installed package",f=>{using var client=new HttpClient(new Reply([1,2,3]));var path=Path.Combine(f.Root,"download.zip");Reject(()=>MusicPackDownloader.DownloadAsync(client,f.Pack,path,null,CancellationToken.None).GetAwaiter().GetResult());Check(!File.Exists(path)&&f.Settings.Tracks.Count==0);});
Test("downloader never overwrites an existing target",f=>{using var client=new HttpClient(new Reply(File.ReadAllBytes(f.Zip)));var path=Path.Combine(f.Root,"existing.zip");File.WriteAllText(path,"personal");Reject(()=>MusicPackDownloader.DownloadAsync(client,f.Pack,path,null,CancellationToken.None).GetAwaiter().GetResult());Check(File.ReadAllText(path)=="personal");});
Test("stalled body times out and removes partially written download",f=>{using var client=new HttpClient(new StalledReply(f.Pack.Bytes));var path=Path.Combine(f.Root,"partial.zip");try{MusicPackDownloader.DownloadAsync(client,f.Pack,path,null,CancellationToken.None,TimeSpan.FromMilliseconds(80)).GetAwaiter().GetResult();throw new Exception("Timeout ignored");}catch(OperationCanceledException){}Check(!File.Exists(path));});
Console.WriteLine($"{passed}/{passed} passed");

sealed class Reply(byte[] bytes):HttpMessageHandler
{
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){RequestMessage=request,Content=new ByteArrayContent(bytes)});
}
sealed class StalledReply(long bytes):HttpMessageHandler
{
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)
 {var content=new StreamContent(new StalledStream());content.Headers.ContentLength=bytes;return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){RequestMessage=request,Content=content});}
}
sealed class StalledStream:Stream
{
 bool first=true;public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
 public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
 public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancel=default){if(first){first=false;buffer.Span[0]=1;return 1;}await Task.Delay(Timeout.Infinite,cancel);return 0;}
 public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Flush(){}public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
}

sealed class Fixture:IDisposable
{
 public string Root=Path.Combine(Path.GetTempPath(),"rift-pack-test-"+Guid.NewGuid());
 public SettingsStore Store;public AppSettings Settings=new();public string Zip;public MusicPack Pack;
 public Fixture(){Directory.CreateDirectory(Root);Store=new(Path.Combine(Root,"Data"));Zip=Path.Combine(Root,"pack.zip");
 var bytes="clean audio fixture"u8.ToArray();using(var z=ZipFile.Open(Zip,ZipArchiveMode.Create)){using var s=z.CreateEntry("Music/a.wav").Open();s.Write(bytes);}
 Pack=new(){Id="test",Name="测试",Asset="test.zip",Tracks=[new(){Id="a",Name="音轨",File="a.wav",Category="worlds",Duration=10,End=10}],Files=[new(){Path="Music/a.wav",Bytes=bytes.Length,Sha256=Convert.ToHexStringLower(SHA256.HashData(bytes))}]};Rehash();}
 public void Rehash(){Pack.Bytes=new FileInfo(Zip).Length;using var s=File.OpenRead(Zip);Pack.Sha256=Convert.ToHexStringLower(SHA256.HashData(s));}
 public void Install(){using var p=MusicPackInstaller.PrepareAsync(Zip,Pack,Store,CancellationToken.None).GetAwaiter().GetResult();p.Install(Store,Settings);}
 public void Dispose(){Directory.Delete(Root,true);}
}
