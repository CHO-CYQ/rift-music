using System.IO;
using System.Text.Json;
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
    [STAThread] static int Main(string[] args)
    {
        if(args.Length<2 || args.Length>3)throw new ArgumentException("Pass app directory, report directory and optional partition such as 1/4 or ids:id1,id2");
        var part=0;var partitions=1;
        HashSet<string>? selectedIds=null;
        if(args.Length==3 && args[2].StartsWith("ids:"))selectedIds=args[2][4..].Split(',').ToHashSet();
        else if(args.Length==3){var pieces=args[2].Split('/');if(pieces.Length!=2 || !int.TryParse(pieces[0],out var number) || !int.TryParse(pieces[1],out partitions) || number<1 || number>partitions || partitions>8)throw new ArgumentException("Invalid partition");part=number-1;}
        var appDir=Path.GetFullPath(args[0]);var report=Path.GetFullPath(args[1]);Directory.CreateDirectory(report);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/RiftMusic;component/Styles.xaml")});
        var dispatcher=Dispatcher.CurrentDispatcher;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var exit=1;
        dispatcher.InvokeAsync(async ()=>{
            MainWindow? window=null;
            using var controller=new AppController(Path.Combine(appDir,"Data"));
            var results=new List<object>();
            try
            {
                if(controller.Settings.Tracks.Count<20)throw new Exception("Expansion must contain at least 20 real tracks");
                controller.Audio.Volume=0;
                if(selectedIds is not null && (selectedIds.Count==0 || selectedIds.Any(id=>!controller.Settings.Tracks.Any(t=>t.Id==id))))throw new Exception("Unknown or empty requested audio ids");
                foreach(var track in controller.Settings.Tracks.Where((track,index)=>index%partitions==part && (selectedIds is null||selectedIds.Contains(track.Id))))
                {
                    if(!track.IsValid())throw new Exception("Invalid track: "+track.Name);
                    var file=controller.Store.ResolveMusic(track.File);var duration=await AudioPlayer.ProbeAsync(file,CancellationToken.None);
                    if(Math.Abs(duration-track.Duration)>.2 || track.End>duration+.05)throw new Exception("Duration mismatch: "+track.Name);
                    string? playbackError=null;void Failed(string? id,string message)=>playbackError=message;
                    controller.Audio.Failed+=Failed;
                    await controller.Audio.PlayAsync(track,file,false);
                    var progressDeadline=DateTime.UtcNow.AddSeconds(5);
                    while(playbackError is null && controller.Audio.IsBusy && controller.Audio.PositionSeconds<.1 && DateTime.UtcNow<progressDeadline)await Task.Delay(50);
                    if(playbackError is not null || controller.Audio.PositionSeconds<.1)
                    {
                        var raw=(MediaPlayer?)typeof(AudioPlayer).GetField("player",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(controller.Audio);
                        throw new Exception($"Playback failed: {track.Name}; error={playbackError}; busy={controller.Audio.IsBusy}; position={controller.Audio.PositionSeconds}; raw={raw?.Position.TotalSeconds}; clip={track.Start}-{track.End}");
                    }
                    controller.Audio.Stop();controller.Audio.Failed-=Failed;
                    results.Add(new{track.Id,track.Name,File=track.File,Duration=duration,Passed=true});
                    Console.WriteLine("PASS "+track.Name);
                }
                window=new MainWindow(controller);window.Show();window.UpdateLayout();
                await dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                var surface=(FrameworkElement)VisualTreeHelper.GetChild(window,0);
                var bitmap=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);
                var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(report,"catalog-window.png")))png.Save(f);
                foreach(var kind in controller.Settings.Tracks.Select(t=>t.Kind).Distinct())
                {
                    var sample=controller.Settings.Tracks.First(t=>t.Kind==kind);var editor=new TrackEditor(sample);
                    if(((ComboBoxItem)((ComboBox)editor.FindName("KindBox")).SelectedItem).Content.ToString()!=kind)throw new Exception("Editor loses audio version: "+kind);
                    if((string)((ComboBox)editor.FindName("CategoryBox")).SelectedValue!=sample.Category)throw new Exception("Editor loses category: "+sample.Name);
                }
                File.WriteAllText(Path.Combine(report,"catalog-check.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
                exit=0;Console.WriteLine($"CATALOG PASSED: {results.Count} real tracks decoded and played; version labels preserved");
            }
            catch(Exception error){Console.WriteLine("FAIL "+error);}
            finally{if(window is not null){window.AllowClose=true;window.Close();}dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
        });
        Dispatcher.Run();return exit;
    }
}
