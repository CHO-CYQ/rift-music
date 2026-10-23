using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RiftMusic.App;
using RiftMusic.App.Services;
using RiftMusic.Core;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        var result = 1;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/RiftMusic;component/Styles.xaml") });
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        dispatcher.InvokeAsync(async () => {
            MainWindow? window = null; AppController? controller = null;
            try
            {
                var target = args.Length == 0 ? Path.Combine(Path.GetTempPath(), "rift-desktop-" + Guid.NewGuid()) : Path.GetFullPath(args[0]);
                Directory.CreateDirectory(target);
                controller = new AppController(Path.Combine(target,"Data"));
                window = new MainWindow(controller); window.Show(); window.UpdateLayout();
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Assert(window.Background is SolidColorBrush bg && (bg.Color.R+bg.Color.G+bg.Color.B)>700,"main window applies a light theme");
                Capture(window, Path.Combine(target, "window-empty.png"));
                var artworkFolder=Path.Combine(target,"Data","Artwork");Directory.CreateDirectory(artworkFolder);
                var coverPixels=Enumerable.Repeat((byte)128,8*8*4).ToArray();
                var coverBitmap=BitmapSource.Create(8,8,96,96,PixelFormats.Bgra32,null,coverPixels,8*4);
                var coverEncoder=new PngBitmapEncoder();coverEncoder.Frames.Add(BitmapFrame.Create(coverBitmap));
                using(var coverStream=new MemoryStream()) { coverEncoder.Save(coverStream);var bytes=coverStream.ToArray();File.WriteAllBytes(Path.Combine(artworkFolder,"broken-cover.png"),bytes[..(bytes.Length/2)]); }
                Assert(ArtworkService.Get(new Track{Id="bad-cover",Name="bad cover",Category="draft",CoverFile="broken-cover.png"}) is ImageSource,"corrupt local PNG falls back to a neutral cover without interrupting the app");
                Assert(((TextBlock)window.FindName("CountLabel")).Text.Contains("0 首"), "empty library represented honestly");
                var path = Path.Combine(target,"probe.wav"); MakeWave(path);
                var duration = await AudioPlayer.ProbeAsync(path,CancellationToken.None);
                Assert(Math.Abs(duration-4)<.1,"PCM WAV decoder duration");
                var audio = controller.Audio; audio.Volume=0;
                var track=new Track {Name="自动验证提示音（非世界赛音乐）",File="probe.wav",Duration=duration,Start=.5,End=1.5,FadeIn=.1,FadeOut=.2};
                await audio.PlayAsync(track,path,false);
                Assert(audio.IsBusy,"playback starts");
                await Task.Delay(550);
                Assert(audio.PositionSeconds>.1,"playback position advances");
                await Task.Delay(1200);
                Assert(!audio.IsBusy,"bounded clip stops automatically");
                var longer = new Track { Name="player control probe", File="probe.wav", Duration=duration, Start=.5, End=3.5, FadeIn=0, FadeOut=0 };
                await audio.PlayAsync(longer,path,false);
                await Task.Delay(300);
                var pause = typeof(AudioPlayer).GetMethod("PauseOrResume") ?? throw new Exception("pause/resume control is missing");
                pause.Invoke(audio,null);
                var pausedAt=audio.PositionSeconds;
                await Task.Delay(300);
                Assert(Math.Abs(audio.PositionSeconds-pausedAt)<.08,"pause keeps clip position stable");
                pause.Invoke(audio,null);
                await Task.Delay(250);
                Assert(audio.PositionSeconds>pausedAt+.1,"resume advances same clip");
                var seek = typeof(AudioPlayer).GetMethod("Seek") ?? throw new Exception("seek control is missing");
                seek.Invoke(audio,[1.8]);
                await Task.Delay(120);
                Assert(audio.PositionSeconds>1.7 && audio.PositionSeconds<2.2,"seek is relative to clip start");
                seek.Invoke(audio,[-50d]);
                await Task.Delay(100);
                Assert(audio.PositionSeconds>=0 && audio.PositionSeconds<.5,"seek clamps to clip boundary");
                audio.Stop();
                var looping = typeof(AudioPlayer).GetMethods().SingleOrDefault(m=>m.Name=="PlayAsync" && m.GetParameters().Length==4)
                    ?? throw new Exception("draft loop control is missing");
                await (Task)looping.Invoke(audio,[track,path,false,true])!;
                await Task.Delay(1500);
                Assert(audio.IsBusy,"draft clip loops beyond its first endpoint");
                audio.Stop();
                Assert(!audio.IsBusy,"stop terminates loop");
                var first=audio.PlayAsync(track,path,false); audio.Stop(); await first;
                Assert(!audio.IsBusy,"cancelling pending open leaves idle player");
                if (args.Length > 1)
                {
                    var mp3=Path.GetFullPath(args[1]);var mp3Duration=await AudioPlayer.ProbeAsync(mp3,CancellationToken.None);
                    Assert(Math.Abs(mp3Duration-4)<.15,"MP3 decoder duration");
                    await audio.PlayAsync(track,mp3,false);await Task.Delay(350);
                    Assert(audio.PositionSeconds>.1,"MP3 playback advances");audio.Stop();
                }
                var broken=Path.Combine(target,"broken.wav");File.WriteAllText(broken,"invalid media");
                foreach(var actualFile in args.Skip(2))
                {
                    var actualPath=Path.GetFullPath(actualFile);var actualDuration=await AudioPlayer.ProbeAsync(actualPath,CancellationToken.None);
                    Assert(actualDuration>80 && actualDuration<90,"packaged client cue duration: "+Path.GetFileName(actualPath));
                    var actualTrack=new Track{Name=Path.GetFileName(actualPath),File=Path.GetFileName(actualPath),Duration=actualDuration,End=1};
                    await audio.PlayAsync(actualTrack,actualPath,false);await Task.Delay(350);
                    Assert(audio.PositionSeconds>.1,"packaged client cue playback advances: "+Path.GetFileName(actualPath));audio.Stop();
                }
                var rejected=false;try{await AudioPlayer.ProbeAsync(broken,CancellationToken.None);}catch(Exception){rejected=true;}
                Assert(rejected,"invalid audio is rejected");
                var managed=controller.Store.ResolveMusic("managed.wav");Directory.CreateDirectory(Path.GetDirectoryName(managed)!);File.Copy(path,managed,true);
                var managedTrack=new Track{Name="可播放测试",File="managed.wav",Duration=duration,End=2};controller.Settings.Tracks.Add(managedTrack);controller.Settings.FixedTrackId=managedTrack.Id;
                Assert(controller.CanPlay(managedTrack),"valid managed audio eligible");File.WriteAllText(managed,"broken media");
                try{await controller.PreviewAsync(managedTrack);}catch(InvalidDataException){}
                Assert(!controller.CanPlay(managedTrack),"known undecodable audio excluded from autoplay");
                File.Copy(path,managed,true);Assert(controller.CanPlay(managedTrack),"replaced media can be probed again");
                var editor=new TrackEditor(track){Owner=window};editor.Show();editor.UpdateLayout();
                Capture(editor,Path.Combine(target,"editor.png"));
                ((TextBox)editor.FindName("StartBox")).Text="-1";
                ((Button)editor.FindName("SaveTrackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(!string.IsNullOrWhiteSpace(((TextBlock)editor.FindName("ErrorLabel")).Text),"invalid editor clip stays open with message");
                editor.Close();
                controller.Settings.Volume=.62;controller.Save();
                Assert(Math.Abs(new SettingsStore(Path.Combine(target,"Data")).Load().Volume-.62)<.001,"desktop settings persist");
                result=0;Console.WriteLine("DESKTOP TESTS PASSED");
            }
            catch(Exception e){Console.WriteLine("DESKTOP TEST FAILURE: "+e);}
            finally{if(window is not null){window.AllowClose=true;window.Close();}controller?.Dispose();dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
        });
        Dispatcher.Run();return result;
    }
    private static void Assert(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static void Capture(Window window,string path)
    {
        var surface=(FrameworkElement)VisualTreeHelper.GetChild(window,0);
        var bitmap=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
    private static void MakeWave(string path)
    {
        const int rate=44100, samples=rate*4;using var file=File.Create(path);using var w=new BinaryWriter(file);
        w.Write("RIFF"u8);w.Write(36+samples*2);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write("data"u8);w.Write(samples*2);
        for(var i=0;i<samples;i++)w.Write((short)(Math.Sin(i*2*Math.PI*440/rate)*1000));
    }
}
