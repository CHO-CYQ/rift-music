using System.IO;
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
        if(args.Length!=2) throw new ArgumentException("Pass library stage and a new report directory");
        var source=Path.GetFullPath(args[0]);var report=Path.GetFullPath(args[1]);Directory.CreateDirectory(report);
        var data=Path.Combine(report,"Data");Directory.CreateDirectory(Path.Combine(data,"Music"));
        File.Copy(Path.Combine(source,"Data/settings.json"),Path.Combine(data,"settings.json"),false);
        foreach(var file in Directory.GetFiles(Path.Combine(source,"Data/Music"))) File.Copy(file,Path.Combine(data,"Music",Path.GetFileName(file)),false);
        var artwork=Path.Combine(source,"Data/Artwork");
        if(Directory.Exists(artwork)) { Directory.CreateDirectory(Path.Combine(data,"Artwork")); foreach(var file in Directory.GetFiles(artwork))File.Copy(file,Path.Combine(data,"Artwork",Path.GetFileName(file)),false); }
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/RiftMusic;component/Styles.xaml")});
        var dispatcher=Dispatcher.CurrentDispatcher;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var exit=1;
        dispatcher.InvokeAsync(async()=>{
            MainWindow? window=null;using var controller=new AppController(data);
            try{
                controller.Settings.AutoPlay=false;controller.Settings.Mode=RiftMusic.Core.SelectionMode.Random;controller.Settings.RandomScope="worlds";
                window=new MainWindow(controller);window.Show();window.UpdateLayout();await dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                var navigation=(TreeView)window.FindName("CategoryTree");var list=(ListBox)window.FindName("TrackList");
                var search=(TextBox)window.FindName("SearchBox");
                TreeViewItem Node(string id) {
                    object Find(IEnumerable<object> values,string key)=>values.Single(x=>x.GetType().GetProperty("Id")!.GetValue(x)?.ToString()==key);
                    var parts=id.Split('|');var parent=Find(navigation.Items.Cast<object>(),parts[0]);
                    var container=(TreeViewItem)navigation.ItemContainerGenerator.ContainerFromItem(parent);
                    if(parts.Length==1)return container;
                    container.IsExpanded=true;window.UpdateLayout();
                    return (TreeViewItem)container.ItemContainerGenerator.ContainerFromItem(Find(container.Items.Cast<object>(),id));
                }
                void ClickNode(string id) {
                    if(id is "all" or "favorites")((Button)window.FindName(id=="all"?"AllLibraryButton":"FavoritesLibraryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    else {var node=Node(id);Descendants(node).OfType<ToggleButton>().First(t=>t.Tag?.ToString()==id).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));}
                    window.UpdateLayout();
                }
                void Choose(string id) {ClickNode("all");if(id!="all")ClickNode(id);}
                void ChooseYear(string category,int year) {Choose("all");ClickNode(category+"|"+year);}
                void Multiple(bool enabled) {var toggle=(ToggleButton)window.FindName("MultiSelectToggle");toggle.IsChecked=enabled;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));window.UpdateLayout();}

                void Workspace(bool bp)=>((Button)window.FindName(bp?"DraftWorkspaceButton":"OpeningWorkspaceButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(list.Items.Count==controller.Settings.Tracks.Count(t=>t.Category!="draft"),"opening workspace isolates its 63 tracks by default");
                Check(window.FindName("AllLibraryButton") is Button && window.FindName("FavoritesLibraryButton") is Button,"collections use distinct navigation buttons instead of filter checkboxes");
                Check(((ToggleButton)window.FindName("MultiSelectToggle")).IsChecked==false && !navigation.Items.Cast<object>().Any(n=>n.GetType().GetProperty("Id")?.GetValue(n)?.ToString() is "all" or "favorites"),"default sidebar separates collections from opt-in multiple filters");
                Check(Descendants(navigation).OfType<Border>().Where(b=>b.Name=="Mark").All(b=>b.Visibility==Visibility.Collapsed),"default category navigation has no checkbox clutter");
                ClickNode("worlds");ClickNode("skins");
                Check(list.Items.Count>0 && list.Items.Cast<Track>().All(t=>t.Category=="skins"),"normal category click replaces the previous category");
                ClickNode("worlds|2020");
                Check(list.Items.Count>0 && list.Items.Cast<Track>().All(t=>t.Category=="worlds" && t.Year==2020),"normal year click chooses one category-year pair");
                var allYearsAction=Descendants(Node("worlds")).OfType<Button>().Single(b=>b.Name=="AllYearsButton");allYearsAction.Focus();allYearsAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
                var allYearsKeepsFocus=Keyboard.FocusedElement is Button {Name:"AllYearsButton"};
                Check(list.Items.Count==controller.Settings.Tracks.Count(t=>t.Category=="worlds"),"all-years action restores its entire category");
                Multiple(true);Choose("all");ClickNode("worlds|2020");ClickNode("skins|2019");
                Check(list.Items.Cast<Track>().Any(t=>t.Category=="worlds") && list.Items.Cast<Track>().Any(t=>t.Category=="skins") && list.Items.Cast<Track>().All(t=>t.Category=="worlds"&&t.Year==2020 || t.Category=="skins"&&t.Year==2019),"explicit multiple mode preserves paired union filtering");
                Multiple(false);
                Check(list.Items.Count>0 && list.Items.Cast<Track>().All(t=>t.Category=="skins"&&t.Year==2019),"returning to single mode retains the latest selected pair");
                var resetAction=(Button)window.FindName("ResetBrowseButton");resetAction.Focus();resetAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
                var resetKeepsFocus=Keyboard.FocusedElement==window.FindName("MultiSelectToggle");Console.WriteLine($"SIDEBAR ACTION FOCUS: all-years={allYearsKeepsFocus}, reset={resetKeepsFocus}");
                Check(allYearsKeepsFocus && resetKeepsFocus,"all-years and reset keep keyboard focus on a visible sidebar control");
                Choose("all");
                Check(((FrameworkElement)window.FindName("SidebarSelectionHint")).Visibility==Visibility.Collapsed && ((FrameworkElement)window.FindName("HeroArtLabel")).Visibility==Visibility.Collapsed && ((FrameworkElement)window.FindName("ResultLabel")).Visibility==Visibility.Collapsed,"unnecessary sidebar, art and result footer instructions are removed");
                var heroArt=(Image)window.FindName("WorldsThemeImage");
                Check(heroArt.Stretch==Stretch.Uniform && heroArt.Source is BitmapImage openingArt && openingArt.UriSource.ToString().Contains("opening-atmosphere-v054"),"generic opening hero uses a full uncropped atmosphere illustration");
                Check(!Descendants((DependencyObject)window.FindName("SidebarScroll")).Contains((DependencyObject)window.FindName("ThemeFilter")),"theme filter is beside search instead of at the bottom of navigation");
                Multiple(true);Capture(window,Path.Combine(report,"sidebar-multiple.png"));
                var worldsNode=Node("worlds");
                Check(Descendants(worldsNode).OfType<Border>().Any(b=>b.Name=="CompetitionMark" && b.OpacityMask is ImageBrush),"sidebar uses the official logo alpha mask without an opaque backing tile");
                worldsNode.IsExpanded=true;window.UpdateLayout();await Task.Delay(350);window.UpdateLayout();
                var yearViewport=(Border)worldsNode.Template.FindName("YearViewport",worldsNode);
                Check(yearViewport.ActualHeight<=190 && yearViewport.ActualHeight>80,"long year groups have a bounded compact viewport");
                var positions=worldsNode.Items.Cast<object>().Take(6).Select(x=>((TreeViewItem)worldsNode.ItemContainerGenerator.ContainerFromItem(x)).TranslatePoint(new Point(),worldsNode).X).Distinct().Count();
                Check(positions>=3,"years use three columns instead of a tall single-column list");
                var skinsNode=Node("skins");skinsNode.IsExpanded=true;window.UpdateLayout();await Task.Delay(350);
                Check(!worldsNode.IsExpanded && skinsNode.IsExpanded,"opening another year group collapses the previous group without changing filters");
                worldsNode.IsExpanded=true;window.UpdateLayout();
                Check(yearViewport.HasAnimatedProperties,"year expansion starts a real layout reveal animation");
                await Task.Delay(350);window.UpdateLayout();Capture(window,Path.Combine(report,"compact-worlds-years.png"));
                Check(window.WindowStyle==WindowStyle.None && System.Windows.Shell.WindowChrome.GetWindowChrome(window)?.CaptionHeight>=40,"native caption is integrated with a generous drag area");
                Check(((Slider)window.FindName("PlaybackProgress")).ActualHeight>=28,"playback timeline has an accessible drag target");
                var play=(Button)window.FindName("PreviewButton");var playIcon=(System.Windows.Shapes.Path)window.FindName("PreviewIcon");
                list.SelectedIndex=0;play.Focus();window.UpdateLayout();
                Check(play.FocusVisualStyle is null && Descendants(play).OfType<System.Windows.Shapes.Path>().Count()==1,"focused play button has one glyph and no duplicate default focus overlay");
                Check(playIcon.TranslatePoint(new Point(),play).X>=0 && playIcon.ActualWidth<=play.ActualWidth,"play glyph fits inside its button without padding overflow");
                Capture(window,Path.Combine(report,"play-focused.png"));list.SelectedIndex=-1;
                var motion=(CheckBox)window.FindName("MotionToggle");motion.IsChecked=false;
                Check(!new AppearanceStore(data).Load().MotionEnabled,"appearance motion toggle persists independently of playback preferences");
                Workspace(true);Workspace(false);
                var content=(FrameworkElement)window.FindName("PageContent");
                Check(content.Opacity==1 && content.RenderTransform is TranslateTransform staticPage && !staticPage.HasAnimatedProperties,"workspace switch stays fully visible with motion disabled");
                Check(((TextBlock)window.FindName("FontStatus")).Text.Contains("替代") || ((TextBlock)window.FindName("FontStatus")).Text.Contains("Manuka"),"font settings report the actual loaded original or alternative");
                motion.IsChecked=true;
                var page=(ScrollViewer)window.FindName("PageScrollViewer");
                var hero=(FrameworkElement)window.FindName("HeroBanner");
                var before=hero.TranslatePoint(new Point(),page).Y;page.ScrollToVerticalOffset(350);window.UpdateLayout();
                Check(page.VerticalOffset>200 && hero.TranslatePoint(new Point(),page).Y<before-200,"hero and top content scroll with the music list");page.ScrollToTop();window.UpdateLayout();
                Capture(window,Path.Combine(report,"library-real.png"));
                Workspace(true);Check(list.Items.Count==controller.Settings.Tracks.Count(t=>t.Category=="draft"),"BP is a first-class workspace with all actual tracks");
                Check(((ToggleButton)window.FindName("MultiSelectToggle")).IsChecked==false && heroArt.Source is BitmapImage draftArt && draftArt.UriSource.ToString().Contains("draft-atmosphere-v054"),"BP restores its own single mode and uses meaningful generic art");Capture(window,Path.Combine(report,"bp-real.png"));Multiple(true);
                ChooseYear("competition:Worlds",2024);Capture(window,Path.Combine(report,"worlds2024-bp-real.png"));
                ChooseYear("competition:Worlds",2025);Capture(window,Path.Combine(report,"worlds2025-bp-real.png"));
                Choose("competition:FirstStand");Capture(window,Path.Combine(report,"firststand-real.png"));
                var focusedYear=Descendants(Node("competition:FirstStand|2026")).OfType<ToggleButton>().First();focusedYear.Focus();focusedYear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));window.UpdateLayout();
                Check(Keyboard.FocusedElement is ToggleButton {Tag:string focusedId} && focusedId=="competition:FirstStand|2026","sidebar year retains keyboard focus after rebuilding selection");
                ((ToggleButton)Keyboard.FocusedElement).RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),(int)Environment.TickCount64,Key.Right){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                Check(Keyboard.FocusedElement is ToggleButton {Tag:"competition:FirstStand|2025"},"arrow keys navigate the compact year grid without changing the selection");
                Workspace(false);Choose("worlds");
                Check(list.Items.Count>0 && list.Items.Cast<Track>().All(t=>t.Category=="worlds"),"Worlds category isolates Worlds tracks");
                ChooseYear("worlds",2020);
                Check(list.Items.Count>0 && list.Items.Cast<Track>().All(t=>t.Year==2020),"year filter combines with category");
                Choose("worlds");search.Text="this-track-does-not-exist";await Task.Delay(220);
                Check(list.Items.Count==0 && ((FrameworkElement)window.FindName("EmptyState")).Visibility==Visibility.Visible,"unmatched search has an honest empty state");
                search.Clear();Choose("skins");search.Text="Star Guardian";await Task.Delay(220);
                Check(list.Items.Count>=3 && list.Items.Cast<Track>().All(t=>t.Theme=="星之守护者"),"English alias search finds Chinese themes");
                Check(controller.Settings.RandomScope=="worlds","browsing does not change automatic random scope");
                var favorite=(Track)list.Items[0];list.SelectedItem=favorite;list.ScrollIntoView(favorite);window.UpdateLayout();
                var item=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(favorite);
                var heart=Descendants(item).OfType<CheckBox>().First(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="收藏音乐");
                heart.IsChecked=true;heart.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Check(favorite.Favorite && new SettingsStore(data).Load().Tracks.Single(t=>t.Id==favorite.Id).Favorite,"favorite toggle persists");
                search.Clear();Choose("favorites");
                Check(list.Items.Contains(favorite),"favorites navigation includes saved track");
                Workspace(true);Choose("favorites");Check(!list.Items.Contains(favorite),"BP favorites excludes opening favorites");Workspace(false);
                ((ComboBox)window.FindName("RandomScopeBox")).SelectedValue="favorites";
                Check(controller.Settings.RandomScope=="favorites","random scope can target favorites explicitly");
                favorite.Artist="Riot Games";favorite.CoverFile="custom-cover.png";
                favorite.DraftUses=[new DraftUse{Year=2022,Competition="Worlds",Stage="Champ Select",Evidence="https://example.com/verified-use"}];
                var editor=new TrackEditor(favorite){Owner=window};
                _ = dispatcher.BeginInvoke(()=>((Button)editor.FindName("SaveTrackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                Check(editor.ShowDialog()==true,"metadata editor saves a valid classified track");
                Check(editor.Result.Favorite && editor.Result.Category==favorite.Category && editor.Result.Theme==favorite.Theme && editor.Result.Quality==favorite.Quality && editor.Result.Source==favorite.Source,"editing preserves favorites, category and provenance");
                Check(editor.Result.Artist==favorite.Artist && editor.Result.CoverFile==favorite.CoverFile && editor.Result.DraftUses.Count==1 && editor.Result.DraftUses[0].Evidence==favorite.DraftUses[0].Evidence,"editing preserves artist, cover and verified draft uses");
                var draftFixture=new Track{Id="draft-ui-fixture",Name="BP UI fixture",File=favorite.File,Duration=favorite.Duration,End=favorite.End,Category="draft",Year=2024,
                    Favorite=true, DraftUses=[new DraftUse{Year=2024,Competition="LPL",Stage="BP",Evidence="fixture"},new DraftUse{Year=2025,Competition="Worlds",Stage="BP",Evidence="fixture"}]};
                controller.Settings.Tracks.Add(draftFixture);controller.Save();search.Text=" ";search.Clear();Workspace(true);Choose("all");
                controller.Settings.Tracks.Add(new Track{Id="early-draft-ui",Name="Early BP",File=favorite.File,Duration=favorite.Duration,End=favorite.End,Category="draft",Year=2018,DraftUses=[new DraftUse{Year=2018,Competition="Worlds",Stage="BP",Evidence="fixture"}]});
                search.Text=" ";search.Clear();await Task.Delay(220);
                Check(((ComboBox)window.FindName("DraftYearBox")).Items.Contains("2018"),"new early BP years appear immediately in automatic selector");
                search.Text="BP UI fixture";
                Choose("favorites");ClickNode("competition:LPL");
                Check(list.Items.Count==1 && list.Items[0]==draftFixture,"BP favorites combines with sidebar competition selection");
                ClickNode("competition:LPL");
                Check(list.Items.Count==0,"unchecking the final group shows no selected groups instead of silently broadening");
                Choose("favorites");search.Text="no-match-in-favorites";await Task.Delay(220);
                var empty=(FrameworkElement)window.FindName("EmptyState");Descendants(empty).OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(list.Items.Count==1 && list.Items[0]==draftFixture,"clearing an empty BP search keeps the favorites collection");Choose("all");search.Text="BP UI fixture";
                ChooseYear("competition:Worlds",2025);
                Check(list.Items.Count==1 && list.Items[0]==draftFixture,"sidebar year selects a matching verified competition use");
                ClickNode("competition:LPL|2024");
                Check(list.Items.Count==1,"sidebar union does not duplicate a track used in both selected pairs");
                ChooseYear("competition:LPL",2025);
                Check(list.Items.Count==0,"BP UI rejects a cross-year competition mismatch");
                Choose("all");list.SelectedItem=draftFixture;
                Check(window.FindName("YearFilter") is null && window.FindName("CompetitionFilter") is null,"duplicate right-hand year and competition dropdowns are removed");
                Check(Node("competition:FirstStand").Items.Count==2,"First Stand has only its 2025 and 2026 child years");
                var oldOpening=controller.Settings.FixedTrackId;
                var oldDraft=controller.Settings.FixedDraftTrackId;
                ((ComboBox)window.FindName("DraftModeBox")).SelectedIndex=1;
                Check(controller.Settings.FixedDraftTrackId==oldDraft && controller.Settings.FixedTrackId==oldOpening,"browsing and mode selection do not overwrite fixed tracks");
                ((Button)window.FindName("UseForAutomaticButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(controller.Settings.FixedDraftTrackId==draftFixture.Id && controller.Settings.FixedTrackId==oldOpening,"explicit fixed BP action leaves opening selection intact");
                ((ComboBox)window.FindName("DraftYearBox")).SelectedItem="2026";
                var automaticCompetition=(ComboBox)window.FindName("DraftCompetitionBox");automaticCompetition.SelectedItem=automaticCompetition.Items.Cast<object>().Single(row=>row.GetType().GetProperty("Id")!.GetValue(row)?.ToString()=="MSI");
                ChooseYear("competition:Worlds",2025);var invalidAutomaticCover=((Image)window.FindName("SelectedCover")).Source;
                ChooseYear("competition:LPL",2024);
                Check(ReferenceEquals(invalidAutomaticCover,((Image)window.FindName("SelectedCover")).Source),"out-of-range fixed BP automatic cover is independent of browsing");
                ((ComboBox)window.FindName("DraftYearBox")).SelectedItem="2024";automaticCompetition.SelectedItem=automaticCompetition.Items.Cast<object>().Single(row=>row.GetType().GetProperty("Id")!.GetValue(row)?.ToString()=="LPL");
                ChooseYear("competition:Worlds",2025);list.SelectedItem=draftFixture;
                controller.Audio.Volume=0;await controller.Audio.PlayAsync(draftFixture,controller.Store.ResolveMusic(draftFixture.File),true,true);
                var activeAutomaticCover=((Image)window.FindName("PlayerCover")).Source;
                Check(ReferenceEquals(activeAutomaticCover,ArtworkService.Get(draftFixture,new(){Competition="LPL",Year=2024})),"starting automatic playback of the selected same-id track refreshes its actual-use cover");
                ((ComboBox)window.FindName("DraftYearBox")).SelectedItem="2026";automaticCompetition.SelectedItem=automaticCompetition.Items.Cast<object>().Single(row=>row.GetType().GetProperty("Id")!.GetValue(row)?.ToString()=="MSI");
                ChooseYear("competition:Worlds",2025);
                Check(ReferenceEquals(activeAutomaticCover,((Image)window.FindName("PlayerCover")).Source),"active automatic BP keeps its actual start-use artwork when browsing or the next plan changes");controller.Audio.Stop();
                Choose("all");list.SelectedItem=draftFixture;
                var settingsButton=(Button)window.FindName("SettingsButton");settingsButton.Focus();settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
                var panel=(FrameworkElement)window.FindName("SettingsPanel");
                Check(Keyboard.FocusedElement==window.FindName("SettingsCloseButton"),"drawer takes initial keyboard focus");
                var focusContained=true;
                for(var step=0;step<20;step++) { (Keyboard.FocusedElement as FrameworkElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));focusContained &= Keyboard.FocusedElement is DependencyObject focused && Descendants(panel).Contains(focused); }
                Check(focusContained,"Tab stays inside the open settings drawer");
                Check(((FrameworkElement)window.FindName("DraftPanel")).Visibility==Visibility.Visible && ((FrameworkElement)window.FindName("OpeningSettingsPanel")).Visibility==Visibility.Collapsed,"automatic drawer follows BP workspace");
                Check(((TextBlock)window.FindName("SourceTrackTitle")).Text==draftFixture.Name,"source detail identifies the selected BP song");
                Capture(window,Path.Combine(report,"bp-settings.png"));
                window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                Check(((FrameworkElement)window.FindName("SettingsPanel")).Visibility==Visibility.Collapsed,"Escape dismisses settings");
                Check(Keyboard.FocusedElement==settingsButton,"closing drawer restores the invoking control focus");
                var importEditor=new TrackEditor(new Track{File=favorite.File,Duration=favorite.Duration,End=favorite.Duration,Category="draft",Name="local BP import"}){Owner=window};
                _=dispatcher.BeginInvoke(()=>{((TextBox)importEditor.FindName("YearBox")).Text="2026";((ComboBox)importEditor.FindName("CompetitionBox")).SelectedValue="MSI";((Button)importEditor.FindName("SaveTrackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));});
                Check(importEditor.ShowDialog()==true && importEditor.Result.DraftUses.Single().Year==2026 && importEditor.Result.DraftUses.Single().Competition=="MSI","local BP import records its selected year and competition");
                var distinctYearEditor=new TrackEditor(new Track{File=favorite.File,Duration=favorite.Duration,End=favorite.End,Category="draft",Name="Release differs from use",Year=2026,DraftUses=[new DraftUse{Year=2021,Competition="Worlds",Stage="BP",Evidence="verified 2021"}]}){Owner=window};
                _=dispatcher.BeginInvoke(()=>((Button)distinctYearEditor.FindName("SaveTrackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                Check(distinctYearEditor.ShowDialog()==true && distinctYearEditor.Result.DraftUses.Single().Year==2021,"saving a BP clip preserves verified use year independently of release label");
                Capture(window,Path.Combine(report,"bp.png"));
                Workspace(false);search.Clear();Choose("worlds");controller.Settings.RandomScope="worlds";controller.Save();
                Capture(window,Path.Combine(report,"worlds.png"));
                Choose("skins");Capture(window,Path.Combine(report,"skins.png"));
                window.Width=1080;window.Height=740;window.UpdateLayout();await dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Check(list.ActualWidth>650 && page.ActualHeight>400,"minimum window preserves usable library space");
                var sidebar=(ScrollViewer)window.FindName("SidebarScroll");sidebar.ScrollToBottom();window.UpdateLayout();
                var finalCategory=Node("custom");
                Check(finalCategory.TranslatePoint(new Point(0,finalCategory.ActualHeight),sidebar).Y<=sidebar.ActualHeight+1,"expanded sidebar can scroll to the final category at minimum size");
                Capture(window,Path.Combine(report,"minimum-window.png"));
                window.Width=1320;window.Height=850;Choose("all");Capture(window,Path.Combine(report,"library.png"));
                ((Button)window.FindName("MaximizeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(120);window.UpdateLayout();
                Check(window.WindowState==WindowState.Maximized,"integrated caption maximizes the native window");Capture(window,Path.Combine(report,"maximized.png"));
                ((Button)window.FindName("MaximizeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(120);
                Check(window.WindowState==WindowState.Normal,"integrated caption restores the native window");
                exit=0;Console.WriteLine("LIBRARY UI TESTS PASSED");
            }catch(Exception error){Console.WriteLine("FAIL "+error);}
            finally{if(window is not null){window.AllowClose=true;window.Close();}dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
        });
        Dispatcher.Run();return exit;
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent){for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var c=VisualTreeHelper.GetChild(parent,i);yield return c;foreach(var nested in Descendants(c))yield return nested;}}
    static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static void Capture(Window window,string path){var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(320)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);window.UpdateLayout();var surface=(FrameworkElement)VisualTreeHelper.GetChild(window,0);var bmp=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(surface);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(path);png.Save(file);}
}
