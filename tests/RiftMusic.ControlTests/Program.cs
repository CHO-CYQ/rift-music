using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using RiftMusic.App;
using RiftMusic.App.Services;
using Path = System.Windows.Shapes.Path;

internal static class Program
{
    public sealed record YearNode(bool IsYear,bool IsMultiple,string Count);
    [STAThread] static int Main(string[] args)
    {
        var report=System.IO.Path.GetFullPath(args.Length>0?args[0]:"control-report");Directory.CreateDirectory(report);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/RiftMusic;component/Styles.xaml")});
        var failures=new List<string>();var passed=0;
        void Check(bool result,string message){Console.WriteLine((result?"PASS ":"FAIL ")+message);if(result)passed++;else failures.Add(message);}
        var gallery=new UniformGrid{Columns=3,Margin=new Thickness(16)};
        var controls=new List<(string Name,Control Control)>();
        void Add(string name,Control control){controls.Add((name,control));control.HorizontalAlignment=HorizontalAlignment.Left;var cell=new StackPanel{Margin=new Thickness(12,6,12,10)};cell.Children.Add(new TextBlock{Text=name,FontSize=13,Margin=new Thickness(0,0,0,8)});cell.Children.Add(control);gallery.Children.Add(cell);}
        Style Style(string key)=>(Style)app.FindResource(key);
        Path Icon(string key)=>new(){Data=(Geometry)app.FindResource(key),Width=19,Height=19,Stretch=Stretch.Uniform,Fill=Brushes.Black};
        Add("switch",new CheckBox{Style=Style("Switch"),IsChecked=true});
        Add("checkbox",new CheckBox{Content="参与自动播放",IsChecked=true});
        Add("favorite",new CheckBox{Style=Style("FavoriteCheck"),IsChecked=true});
        Add("small-icon-button",new Button{Style=Style("PlayerButton"),Content=Icon("FluentSettings")});
        Add("caption",new Button{Style=Style("CaptionButton"),Content="\uE8BB"});
        Add("button",new Button{Content="导入音乐"});
        Add("radio",new RadioButton{Content="每局随机",IsChecked=true});
        Add("multi-select",new ToggleButton{Style=Style("SidebarModeToggle"),Content="多选",IsChecked=true});
        Add("year",new ToggleButton{Style=Style("SidebarCheck"),Content=new TextBlock{Text="2025",FontSize=14},Width=60,DataContext=new YearNode(true,false,"1"),IsChecked=true});
        Add("collection",new Button{Style=Style("CollectionButton"),Content="全部音乐",Width=200});
        Add("combo",new ComboBox{ItemsSource=new[]{"全部主题","全球总决赛"},SelectedIndex=0,Width=200});
        Add("text-box",new TextBox{Text="搜索音乐",Width=200});
        Add("slider",new Slider{Minimum=0,Maximum=100,Value=59,Width=200});
        Add("expander",new Expander{Header="连接诊断",Content=new TextBlock{Text="本机客户端"}});
        Add("sidebar-category",new ToggleButton{Style=Style("SidebarCheck"),Content="全球总决赛",DataContext=new YearNode(false,true,"15"),IsChecked=true});
        Add("transport",new Button{Style=Style("PlayerTransportButton"),Content=new Path{Style=Style("PlayerTransportGlyph"),Data=(Geometry)app.FindResource("PlayerNext")}});
        Add("play",new Button{Style=Style("PlayerPlayButton"),Width=56,Height=56,Content=new Path{Style=Style("PlayerTransportGlyph"),Data=(Geometry)app.FindResource("PlayerPlay"),Stroke=Brushes.White}});
        Add("list-row",new ListBoxItem{Content=Icon("FluentSettings")});
        var host=new Window{Content=gallery,Width=960,Height=810,ShowInTaskbar=false,Style=(Style)app.FindResource(typeof(Window))};host.Show();host.UpdateLayout();
        try {
            foreach(var entry in controls) {
                var control=entry.Control;control.ApplyTemplate();host.UpdateLayout();
                var tracked=Descendants(control).OfType<FrameworkElement>().Where(e=>(e is Shape || e is ContentPresenter) && e.IsVisible).ToArray();
                var normal=tracked.ToDictionary(e=>e,e=>Bounds(e,control));
                var baseline=control.RenderSize;
                Check(control.FocusVisualStyle is null,"one integrated focus cue without dotted overlay: "+entry.Name);
                var focusTarget=control is Expander ? Descendants(control).OfType<ToggleButton>().First() : control;focusTarget.Focus();Keyboard.Focus(focusTarget);host.UpdateLayout();
                Check(baseline==control.RenderSize && tracked.All(e=>Close(normal[e],Bounds(e,control))),"focus does not squeeze or move icon/content: "+entry.Name);
                Check(Descendants(control).OfType<Shape>().Where(s=>s.IsVisible).All(s=>Fits(s)),"focused vector fits its own slot and ancestor borders: "+entry.Name);
                var focusBorders=Descendants(control).OfType<Border>().Where(b=>b.Name.Contains("Focus") && b.Visibility==Visibility.Visible).ToArray();
                Check(focusBorders.Length>0 || Descendants(control).OfType<Border>().Any(b=>b.BorderBrush is SolidColorBrush brush && brush.Color is {R:50,G:27,B:221} || b.BorderBrush is SolidColorBrush black && black.Color.R==24 && black.Color.G==24 && black.Color.B==24),"keyboard focus remains visibly indicated: "+entry.Name);
                foreach(var state in new[]{false,true}) {
                    if(control is ToggleButton toggle)toggle.IsChecked=state;
                    if(control is Expander expander)expander.IsExpanded=state;
                    if(control is ListBoxItem row)row.IsSelected=state;
                    SetReadOnly(focusTarget,typeof(UIElement),"IsMouseOverPropertyKey",true);
                    if(focusTarget is ButtonBase)SetReadOnly(focusTarget,typeof(ButtonBase),"IsPressedPropertyKey",true);
                    host.UpdateLayout();Check(Descendants(control).OfType<Shape>().Where(s=>s.IsVisible).All(s=>Fits(s)),"hover/pressed vector is complete "+state+": "+entry.Name);
                    if(control is Button or ListBoxItem)Check(tracked.All(e=>Close(normal[e],Bounds(e,control))),"press/selection preserves content position "+state+": "+entry.Name);
                    if(focusTarget is ButtonBase)SetReadOnly(focusTarget,typeof(ButtonBase),"IsPressedPropertyKey",false);
                    SetReadOnly(focusTarget,typeof(UIElement),"IsMouseOverPropertyKey",false);
                }
                control.IsEnabled=false;host.UpdateLayout();Check(Descendants(control).OfType<Shape>().Where(s=>s.IsVisible).All(s=>Fits(s)),"disabled vector is complete: "+entry.Name);control.IsEnabled=true;
                if(control is CheckBox {Style:var style} && ReferenceEquals(style,Style("Switch"))) {
                    var knob=(Ellipse)control.Template.FindName("Knob",control);var rail=(FrameworkElement)control.Template.FindName("Rail",control);
                    var inside=Bounds(knob,rail);Check(inside.Top>=2 && inside.Bottom<=rail.ActualHeight-2 && Math.Abs(knob.ActualWidth-knob.Width)<.01 && Math.Abs(knob.ActualHeight-knob.Height)<.01,"focused switch keeps the complete white circle with breathing room");
                    Capture(host,System.IO.Path.Combine(report,"focused-switch-gallery.png"));
                }
                Keyboard.ClearFocus();host.Focus();host.UpdateLayout();
            }
            Capture(host,System.IO.Path.Combine(report,"control-gallery.png"));
            using var controller=new AppController(System.IO.Path.Combine(report,"Data"));controller.Settings.AutoPlay=false;
            var main=new MainWindow(controller);main.Show();main.UpdateLayout();
            var tree=(TreeView)main.FindName("CategoryTree");
            var logos=Descendants(tree).OfType<Border>().Where(b=>b.Name=="CompetitionMark" && b.Visibility==Visibility.Visible).ToArray();
            Check(logos.Length>0 && logos.All(b=>VisualTreeHelper.GetParent(b) is Grid g && g.Children.OfType<TextBlock>().All(t=>t.Visibility==Visibility.Collapsed)),"official sidebar logos do not overlap a second font glyph");
            ((Button)main.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));main.UpdateLayout();
            var opening=(CheckBox)main.FindName("AutoToggle");opening.Focus();main.UpdateLayout();
            Check(Descendants(opening).OfType<Shape>().All(s=>Fits(s)),"actual opening settings switch is complete when focused");
            Capture(main,System.IO.Path.Combine(report,"opening-settings-focused.png"));main.AllowClose=true;main.Close();
        } finally {host.Close();app.Shutdown();}
        Console.WriteLine($"CONTROL TESTS: {passed} passed, {failures.Count} failed");return failures.Count==0?0:1;
    }
    static void SetReadOnly(DependencyObject item,Type owner,string name,object value){var field=owner.GetField(name,BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.FlattenHierarchy)??throw new Exception("Missing WPF state key "+name);item.SetValue((DependencyPropertyKey)field.GetValue(null)!,value);}
    static Rect Bounds(FrameworkElement element,Visual relative)=>new(element.TranslatePoint(new Point(),(UIElement)relative),element.RenderSize);
    static bool Close(Rect a,Rect b)=>Math.Abs(a.X-b.X)<.05 && Math.Abs(a.Y-b.Y)<.05 && Math.Abs(a.Width-b.Width)<.05 && Math.Abs(a.Height-b.Height)<.05;
    static bool Fits(Shape shape){var ink=VisualTreeHelper.GetDescendantBounds(shape);if(ink.IsEmpty)return true;if(ink.Left<-.1 || ink.Top<-.1 || ink.Right>shape.ActualWidth+.1 || ink.Bottom>shape.ActualHeight+.1){Console.WriteLine($"INK {shape.Name}: {ink} SLOT {shape.RenderSize}");return false;}for(DependencyObject ancestor=VisualTreeHelper.GetParent(shape);ancestor is FrameworkElement box;ancestor=VisualTreeHelper.GetParent(box)){if(box is Control)break;if(box is Border or ContentPresenter){var bounds=shape.TransformToAncestor(box).TransformBounds(ink);if(bounds.Left<-.1 || bounds.Top<-.1 || bounds.Right>box.ActualWidth+.1 || bounds.Bottom>box.ActualHeight+.1){Console.WriteLine($"ANCESTOR {shape.Name}: {bounds} IN {box.Name} {box.RenderSize}");return false;}}}return true;}
    static IEnumerable<DependencyObject> Descendants(DependencyObject item){for(var i=0;i<VisualTreeHelper.GetChildrenCount(item);i++){var child=VisualTreeHelper.GetChild(item,i);yield return child;foreach(var x in Descendants(child))yield return x;}}
    static void Capture(Window window,string file){var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(file);encoder.Save(stream);}
}
