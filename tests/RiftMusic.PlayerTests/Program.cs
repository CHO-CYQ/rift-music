using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RiftMusic.App;
using RiftMusic.App.Services;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public NativeRect Monitor,Work; public uint Flags; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out NativeRect bounds);
    [STAThread] static int Main(string[] args)
    {
        var report=Path.GetFullPath(args.Length>0?args[0]:"player-report");Directory.CreateDirectory(report);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/RiftMusic;component/Styles.xaml")});
        var failures=new List<string>();var passed=0;
        void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(ok)passed++;else failures.Add(name);}
        using var controller=new AppController(Path.Combine(report,"Data"));controller.Settings.AutoPlay=false;
        var window=new MainWindow(controller);window.Show();window.UpdateLayout();
        try {
            var cover=(Image)window.FindName("PlayerCover");var title=(TextBlock)window.FindName("NowPlaying");var sub=(TextBlock)window.FindName("PlayerSubtitle");
            var progress=(Slider)window.FindName("PlaybackProgress");var volume=(Slider)window.FindName("VolumeSlider");
            var bar=Parents(cover).OfType<Border>().First(b=>Grid.GetRow(b)==2);
            var info=Parents(cover).OfType<Grid>().First(g=>g.Children.OfType<Grid>().Any());
            Check(cover.Width>=88 && cover.Height>=72,"player artwork has a readable complete album slot");
            Check(title.FontSize>=17 && sub.FontSize>=14,"player track title and state remain readable");
            Check(TextOptions.GetTextFormattingMode(window)==TextFormattingMode.Display,"small text uses pixel-aware display formatting");
            var body=new Typeface((FontFamily)app.Resources["MusicFont"],FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
            var heading=new Typeface((FontFamily)app.Resources["ChineseDisplayFont"],FontStyles.Normal,FontWeights.Bold,FontStretches.Normal);
            var found=heading.TryGetGlyphTypeface(out var glyph);Console.WriteLine("Chinese display font: "+glyph?.FontUri);
            Check(found && glyph is not null && glyph.FontUri.ToString().Contains("AlimamaShuHeiTi-Bold.ttf",StringComparison.OrdinalIgnoreCase),"Chinese display text resolves to the new angular title face");
            Check(glyph is not null && "游戏开场赛事音乐全球总决赛听见下一局".All(c=>glyph.CharacterToGlyphMap.ContainsKey(c)),"Chinese title font contains the actual interface characters");
            var buttons=new[]{"PreviousButton","PreviewButton","NextButton","StopButton"}.Select(n=>(Button)window.FindName(n)).ToArray();
            Check(buttons.All(b=>b.Width>=48 && b.Height>=48),"all transport buttons have generous consistent hit targets");
            var paths=buttons.SelectMany(b=>Descendants(b).OfType<System.Windows.Shapes.Path>()).ToArray();
            Check(paths.Length==4 && paths.All(p=>p.Stroke is not null && Math.Abs(p.StrokeThickness-1.8)<.001),"transport icons share one vector stroke weight");
            progress.Maximum=732;progress.Value=1;volume.Value=59;
            ((TextBlock)window.FindName("PositionLabel")).Text="0:01";((TextBlock)window.FindName("LengthLabel")).Text="12:12";
            title.Text="全球总决赛 · 选人阶段配乐 Distraction";sub.Text="已暂停";
            var widths=new List<double>();
            foreach(var size in new[]{(1080d,740d),(1320d,850d),(1800d,950d)}) {
                window.Width=size.Item1;window.Height=size.Item2;window.UpdateLayout();
                var slot=(FrameworkElement)info.Children[0];widths.Add(slot.ActualWidth);
                var controls=buttons.Select(b=>Bounds(b,bar)).ToArray();var timeline=Bounds(progress,bar);
                Check(controls.All(r=>r.Top>=0 && r.Bottom<=timeline.Top+1),$"transport and timeline do not collide at width {size.Item1}");
                Check(!Bounds(slot,bar).IntersectsWith(timeline) && !Bounds(volume,bar).IntersectsWith(timeline),$"three player regions do not overlap at width {size.Item1}");
                Check(Bounds(title,slot).Right<=slot.ActualWidth+1 && Bounds(cover,slot).Left>=0,$"track information stays inside its region at width {size.Item1}");
                foreach(var density in new[]{1d,1.25,1.5,2d}) Capture(bar,Path.Combine(report,$"player-{size.Item1}-{density*100:0}.png"),density);
            }
            Check(widths.Last()>widths.First()+80,"track information grows when the window widens");
            foreach(var value in new[]{0d,1d,366d,732d}) {
                progress.Value=value;window.UpdateLayout();progress.ApplyTemplate();
                var track=(System.Windows.Controls.Primitives.Track)progress.Template.FindName("PART_Track",progress);
                if(value==1) {
                    var fill=Descendants(track.DecreaseRepeatButton).OfType<Border>().First();
                    Check(fill.TranslatePoint(new Point(),progress).X>=track.Thumb.TranslatePoint(new Point(),progress).X+5,"tiny progress fill stays beneath the knob without a disconnected blue fragment");
                }
                Check(Descendants(track.Thumb).All(x=>x is not UIElement e || e.Effect is null),"timeline thumb remains a sharp vector without blurred halo at "+value);
                var thumb=Bounds(track.Thumb,progress);Check(thumb.Left>=-1 && thumb.Right<=progress.ActualWidth+1,"timeline endpoints keep the complete thumb inside at "+value);
            }
            window.WindowState=WindowState.Maximized;window.UpdateLayout();
            window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var handle=new WindowInteropHelper(window).Handle;var monitor=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};GetMonitorInfo(MonitorFromWindow(handle,2),ref monitor);
            var bottom=bar.PointToScreen(new Point(0,bar.ActualHeight)).Y;
            Console.WriteLine($"Native DPI {GetDpiForWindow(handle)}; player bottom {bottom}; monitor work bottom {monitor.Work.Bottom}");
            Check(bottom<=monitor.Work.Bottom+1,"maximized player stays above the current monitor taskbar");
            GetWindowRect(handle,out var bounds);
            Check(Math.Abs(bounds.Left-monitor.Work.Left)<=1 && Math.Abs(bounds.Top-monitor.Work.Top)<=1 && Math.Abs(bounds.Right-monitor.Work.Right)<=1 && Math.Abs(bounds.Bottom-monitor.Work.Bottom)<=1,"maximized native frame fills the complete monitor work area");
            Capture(window,Path.Combine(report,"maximized.png"),1);
            window.WindowState=WindowState.Normal;window.UpdateLayout();Check(window.WindowState==WindowState.Normal,"restore keeps the native normal window");
        } finally {window.AllowClose=true;window.Close();app.Shutdown();}
        Console.WriteLine($"PLAYER TESTS: {passed} passed, {failures.Count} failed");return failures.Count==0?0:1;
    }
    static Rect Bounds(FrameworkElement element,Visual relative)=>new(element.TranslatePoint(new Point(),(UIElement)relative),element.RenderSize);
    static IEnumerable<DependencyObject> Parents(DependencyObject item){while((item=VisualTreeHelper.GetParent(item)) is not null)yield return item;}
    static IEnumerable<DependencyObject> Descendants(DependencyObject item){for(var i=0;i<VisualTreeHelper.GetChildrenCount(item);i++){var child=VisualTreeHelper.GetChild(item,i);yield return child;foreach(var x in Descendants(child))yield return x;}}
    static void Capture(Visual visual,string file,double density){var element=(FrameworkElement)visual;var window=Window.GetWindow(element);var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*density),(int)Math.Ceiling(window.ActualHeight*density),96*density,96*density,PixelFormats.Pbgra32);bitmap.Render(window);BitmapSource result=bitmap;if(element!=window){var offset=element.TranslatePoint(new Point(),window);result=new CroppedBitmap(bitmap,new Int32Rect((int)Math.Round(offset.X*density),(int)Math.Round(offset.Y*density),Math.Min((int)Math.Round(element.ActualWidth*density),bitmap.PixelWidth-(int)Math.Round(offset.X*density)),Math.Min((int)Math.Round(element.ActualHeight*density),bitmap.PixelHeight-(int)Math.Round(offset.Y*density))));}var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(result));using var stream=File.Create(file);encoder.Save(stream);}
}
