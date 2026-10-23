using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RiftMusic.App;
using RiftMusic.App.Services;
using RiftMusic.Core;
using Track = RiftMusic.Core.Track;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        var report=Path.GetFullPath(args[1]);Directory.CreateDirectory(report);
        var data=Path.Combine(report,"Data");Directory.CreateDirectory(data);
        File.Copy(Path.Combine(args[0],"Data/settings.json"),Path.Combine(data,"settings.json"));
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/RiftMusic;component/Styles.xaml")});
        var dispatcher=Dispatcher.CurrentDispatcher;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        int passed=0,failed=0;
        void Check(bool ok,string name){if(ok)passed++;else failed++;Console.WriteLine((ok?"PASS ":"FAIL ")+name);}
        dispatcher.InvokeAsync(async()=>{
            using var controller=new AppController(data);MainWindow? window=null;
            try {
                controller.Settings.AutoPlay=controller.Settings.AutoDraft=false;
                window=new MainWindow(controller);window.Show();window.UpdateLayout();await Task.Delay(400);
                var search=(TextBox)window.FindName("SearchBox");var tree=(TreeView)window.FindName("CategoryTree");var list=(ListBox)window.FindName("TrackList");
                var first=tree.ItemContainerGenerator.ContainerFromIndex(0);
                search.Text="never-match-release-check";Check(list.Items.Count>0,"rapid typing waits briefly instead of rebuilding on every keystroke");await Task.Delay(220);window.UpdateLayout();
                Check(list.Items.Count==0,"search produces an honest empty result");
                Check(ReferenceEquals(first,tree.ItemContainerGenerator.ContainerFromIndex(0)),"typing preserves expanded navigation controls and keyboard state");
                Check(window.FindName("SearchClearButton") is Button {Visibility:Visibility.Visible},"nonempty search exposes an accessible clear action");
                Check(window.FindName("SearchResultCount") is TextBlock count && count.Text.Contains("0"),"search reports the filtered result count");
                if(window.FindName("SearchClearButton") is Button clear)clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));else search.Clear();
                Check(list.Items.Count==controller.Settings.Tracks.Count(t=>t.Category!="draft"),"clearing search restores its current library");
                var original=(Track)list.Items[0];var replacement=System.Text.Json.JsonSerializer.Deserialize<Track>(System.Text.Json.JsonSerializer.Serialize(original))!;replacement.End-=.1;
                controller.Settings.Tracks[controller.Settings.Tracks.IndexOf(original)]=replacement;
                typeof(MainWindow).GetMethod("RefreshLibrary",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[replacement.Id,false,null]);window.UpdateLayout();
                Check(ReferenceEquals(list.SelectedItem,replacement) && !list.Items.Contains(original),"editing a clip binds the updated track and keeps selection even when its id is unchanged");
                var scroll=(ScrollViewer)window.FindName("SettingsScrollViewer")!;
                ((Button)window.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
                if(scroll is not null){scroll.ScrollToBottom();window.UpdateLayout();}
                ((Button)window.FindName("SettingsCloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)window.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
                Check(scroll is not null && scroll.VerticalOffset<1,"reopening automatic settings begins at its heading");
                ((Button)window.FindName("SettingsCloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));list.SelectedIndex=0;window.UpdateLayout();
                if(window.FindName("TrackDetailsButton") is Button details){details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();await dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);}
                Check(scroll is not null && scroll.VerticalOffset>0 && ((FrameworkElement)window.FindName("SourceSection")).TranslatePoint(new Point(),scroll).Y>=-1,"track details opens at the selected music instead of unrelated automatic settings");
                ((Button)window.FindName("SettingsCloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var slider=(Slider)window.FindName("PlaybackProgress");slider.IsEnabled=true;slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent});
                slider.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.LostMouseCaptureEvent});
                Check(!(bool)typeof(MainWindow).GetField("seeking",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!,"losing a drag capture cannot freeze playback progress");
                ((Button)window.FindName("DraftWorkspaceButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
                Check(list.SelectedItem is null && ReferenceEquals(((Image)window.FindName("PlayerCover")).Source,ArtworkService.Get(null)),"switching to a library without the selected track clears its stale player cover");
                var worlds=tree.Items.Cast<object>().Single(n=>n.GetType().GetProperty("Id")!.GetValue(n)?.ToString()=="competition:Worlds");
                var parent=(TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(worlds);parent.IsExpanded=true;window.UpdateLayout();
                var year=parent.Items.Cast<object>().Single(n=>n.GetType().GetProperty("Id")!.GetValue(n)?.ToString()=="competition:Worlds|2026");
                var row=(TreeViewItem)parent.ItemContainerGenerator.ContainerFromItem(year);
                Descendants(row).OfType<ToggleButton>().First(b=>b.Tag?.ToString()=="competition:Worlds|2026").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));window.UpdateLayout();
                Check(list.Items.Count==0,"missing annual sources remain empty without invented tracks");
                var empty=Descendants((DependencyObject)window.FindName("EmptyState")).OfType<Button>().Single();empty.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
                Check(list.Items.Count==controller.Settings.Tracks.Count(t=>t.Category=="draft"),"empty-state reset actually clears unavailable category and year filters");
                var atmosphere=(BitmapImage)ArtworkService.Atmosphere(true);
                Check(atmosphere.PixelWidth<=768 && Math.Abs(atmosphere.Width/atmosphere.Height-2)<.01,"large hero image is decoded to display size without cropping its ratio");
                var logo=(BitmapImage)ArtworkService.CompetitionLogo("FirstStand")!;
                Check(logo.PixelWidth<=192 && logo.PixelHeight<=192,"large competition icon does not retain a full poster-size bitmap");
                var folder=Path.Combine(data,"Artwork");Directory.CreateDirectory(folder);var coverPath=Path.Combine(folder,"release-cover.png");
                void WriteCover(int size,byte blue=0){var pixels=new byte[size*size*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=blue;pixels[i+2]=(byte)(255-blue);pixels[i+3]=255;}var image=BitmapSource.Create(size,size,96,96,PixelFormats.Bgra32,null,pixels,size*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(coverPath);encoder.Save(stream);}
                var custom=new Track{Id="custom-art-check",Category="custom",CoverFile="release-cover.png"};WriteCover(16);
                Check(ArtworkService.Get(custom) is BitmapImage initial && initial.PixelWidth==16,"small user cover is not unnecessarily upscaled");WriteCover(24);File.SetLastWriteTimeUtc(coverPath,DateTime.UtcNow.AddSeconds(1));
                Check(ArtworkService.Get(custom) is BitmapImage changed && changed.PixelWidth==24,"replacing a user cover is visible without restarting the app");
                WriteCover(24,255);File.SetLastWriteTimeUtc(coverPath,DateTime.UtcNow.AddSeconds(2));var color=(BitmapSource)ArtworkService.Get(custom);var sample=new byte[color.PixelWidth*color.PixelHeight*4];color.CopyPixels(sample,color.PixelWidth*4,0);
                Check(sample[0]==255 && sample[2]==0,"same-size cover replacement bypasses stale Windows image caching");
                var file=Path.Combine(report,"pause.wav");MakeWave(file);
                await controller.Audio.PlayAsync(new Track{Name="release check",File="pause.wav",Duration=4,End=4},file,false);controller.Audio.Volume=0;await Task.Delay(150);controller.Audio.PauseOrResume();
                int notifications=0;void Changed()=>notifications++;controller.Audio.Changed+=Changed;await Task.Delay(250);
                Check(notifications==0,"paused audio emits no continuous UI updates");
                controller.Audio.Seek(1);Check(notifications==1 && controller.Audio.PositionSeconds==1,"paused seeking still updates immediately");
                controller.Audio.Changed-=Changed;controller.Audio.PauseOrResume();await Task.Delay(180);Check(controller.Audio.PositionSeconds>1.08,"resume restores progress updates");controller.Audio.Stop();
                Capture(window,Path.Combine(report,"release-bp.png"));
            } catch(Exception e){failed++;Console.WriteLine(e);}
            finally{if(window is not null){window.AllowClose=true;window.Close();}app.Shutdown(failed==0?0:1);}
            Console.WriteLine($"Release checks: {passed} passed, {failed} failed");
        });return app.Run();
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var d in Descendants(child))yield return d;}}
    static void Capture(Window window,string path){var surface=(FrameworkElement)VisualTreeHelper.GetChild(window,0);var bitmap=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);png.Save(stream);}
    static void MakeWave(string path){const int rate=44100,samples=rate*4;using var file=File.Create(path);using var w=new BinaryWriter(file);w.Write("RIFF"u8);w.Write(36+samples*2);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write("data"u8);w.Write(samples*2);for(int i=0;i<samples;i++)w.Write((short)0);}
}
