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

class Program
{
 [STAThread] static int Main(string[] args)
 {
  var report=Path.GetFullPath(args[0]);Directory.CreateDirectory(report);
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/RiftMusic;component/Styles.xaml")});
  using var controller=new AppController(Path.Combine(report,"Data"));var main=new MainWindow(controller);main.Show();
  var window=new MusicDownloads(controller){Owner=main};window.Show();window.UpdateLayout();int count=0;
  void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);count++;}
  T Named<T>(string name)=>(T)window.FindName(name);
  IEnumerable<T> Visuals<T>(DependencyObject d) where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++){var c=VisualTreeHelper.GetChild(d,i);if(c is T t)yield return t;foreach(var item in Visuals<T>(c))yield return item;}}
  void Pump()=>window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
  void Snapshot(string name){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(report,name));png.Save(file);}
  Check(Named<ItemsControl>("PackList").Items.Count==9,"download catalog exposes nine optional packs");
  var boxes=Visuals<CheckBox>(window).ToList();Check(boxes.Count==9,"all packages have independent accessible selection");
  Check(!Named<Button>("DownloadButton").IsEnabled,"empty selection cannot start a download");
  boxes[0].IsChecked=true;boxes[1].IsChecked=true;Pump();Check(Named<Button>("DownloadButton").IsEnabled&&Named<TextBlock>("SelectionLabel").Text.Contains("2 个曲包"),"multi-selection updates size summary and download action");
  Check(!controller.Settings.AutoPlay&&!controller.Settings.AutoDraft&&controller.Settings.Tracks.Count==0,"selecting downloads never enables autoplay or invents local tracks");
  Snapshot("optional-downloads.png");window.Width=620;window.Height=560;window.UpdateLayout();
  foreach(var name in new[]{"DownloadButton","ImportPackButton","SelectionLabel"}){var element=Named<FrameworkElement>(name);var p=element.TransformToAncestor(window).Transform(new Point());Check(p.Y>=0&&p.Y+element.ActualHeight<=window.ActualHeight&&p.X+element.ActualWidth<=window.ActualWidth,"minimum size keeps "+name+" visible");}
  Snapshot("optional-downloads-minimum.png");window.Close();
  using var resource=typeof(MusicDownloads).Assembly.GetManifestResourceStream("RiftMusic.music-packs.json")!;var packs=JsonSerializer.Deserialize<List<MusicPack>>(resource)!;var pack=packs.Single(p=>p.Id=="opening-events");
  Task.Run(async()=>{using var prepared=await MusicPackInstaller.PrepareAsync(Path.Combine(Path.GetFullPath(args[1]),pack.Asset),pack,controller.Store,CancellationToken.None);prepared.Install(controller.Store,controller.Settings);}).GetAwaiter().GetResult();
  Check(controller.Settings.Tracks.Count==6&&controller.Store.Load().Tracks.Count==6,"real published-format pack installs all six tracks and persists metadata");
  var installed=new MusicDownloads(controller){Owner=main};installed.Show();installed.UpdateLayout();var checks=Visuals<CheckBox>(installed).ToList();Check(checks.Count(c=>!c.IsEnabled)==1,"installed pack is marked and excluded from redundant selection");installed.Close();
  main.AllowClose=true;main.Close();app.Shutdown();Console.WriteLine($"{count}/{count} passed");return 0;
 }
}
