using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RiftMusic.App.Services;

namespace RiftMusic.App;

public partial class MainWindow
{
    private sealed record BrowseState(string Scope, string Query, string Theme, BrowseSelection Selection, double Offset);
    private readonly Dictionary<bool, BrowseState> browseStates = new();
    private IInputElement? settingsReturnFocus;
    private AppearanceStore appearanceStore = null!;
    private AppearanceOptions appearance = new();
    private string fontStatus="";
    private bool appearanceReady;
    private bool MotionAllowed => appearance.MotionEnabled && SystemParameters.ClientAreaAnimation;
    private void InitializeWorldsTypography()
    {
        appearanceStore=new(controller.Store.DirectoryPath);appearance=appearanceStore.Load();
        fontStatus=WorldsTypography.Configure(Application.Current.Resources,Path.Combine(controller.Store.DirectoryPath,"Fonts"));
    }
    private void InitializeAppearance()
    {
        if(FindName("MotionToggle") is CheckBox toggle) toggle.IsChecked=appearance.MotionEnabled;
        if(FindName("FontStatus") is TextBlock status)status.Text=fontStatus;
        if(FindName("WorldsThemeImage") is Image theme)theme.Source=ArtworkService.Worlds22Theme();
        appearanceReady=true;
    }
    private void AppearanceChanged(object sender,RoutedEventArgs e)
    {
        if(!appearanceReady || sender is not CheckBox toggle)return;
        var previous=appearance.MotionEnabled;appearance.MotionEnabled=toggle.IsChecked==true;
        try {appearanceStore.Save(appearance);AnimateWorkspace();foreach(var node in CategoryTree.Items)if(CategoryTree.ItemContainerGenerator.ContainerFromItem(node) is TreeViewItem item)UpdateYearReveal(item,false);if(SettingsPanel.Visibility==Visibility.Visible)WorldsMotion.Reconfigure(SettingsPanel,MotionAllowed,24,0);}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) { appearance.MotionEnabled=previous;appearanceReady=false;toggle.IsChecked=previous;appearanceReady=true;ShowError(error); }
    }
    private void OpenFontsClick(object sender,RoutedEventArgs e)
    {
        try { var folder=Path.Combine(controller.Store.DirectoryPath,"Fonts");Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{FileName="explorer.exe",Arguments="\""+folder+"\"",UseShellExecute=true}); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) {ShowError(error);}
    }
    private void AnimateWorkspace()
    {
        WorldsMotion.Reconfigure(TrackList,false,0,0);
        if(FindName("PageContent") is FrameworkElement page)WorldsMotion.Reconfigure(page,MotionAllowed,0,10);
        if(FindName("WorldsDisplayTitle") is FrameworkElement title)WorldsMotion.Reveal(title,MotionAllowed,-18);
        if(FindName("WorldsThemeImage") is FrameworkElement theme)WorldsMotion.Reveal(theme,MotionAllowed,24);
    }
    private void InitializeWindowExperience()
    {
        WindowWorkArea.Attach(this);
        var area = SystemParameters.WorkArea;
        MinHeight = Math.Min(MinHeight, Math.Max(560, area.Height));
        MinWidth = Math.Min(MinWidth, Math.Max(900, area.Width));
        Height = Math.Min(Height, area.Height);
        Width = Math.Min(Width, area.Width);
        StateChanged += (_, _) => { MaximizeLabel.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922"; };
        PreviewKeyDown += (_, e) => {
            if (e.Key == Key.Escape && SettingsOverlay.Visibility == Visibility.Visible) { CloseSettings(); e.Handled=true; }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { CloseSettings(); SearchBox.Focus(); SearchBox.SelectAll(); e.Handled=true; }
        };
        // Let the one page own vertical wheel input, including when the pointer is on a song.
        PageScrollViewer.PreviewMouseWheel += (_, e) => {
            PageScrollViewer.ScrollToVerticalOffset(Math.Max(0,PageScrollViewer.VerticalOffset - e.Delta)); e.Handled=true;
        };
    }
    private void WorkspaceClick(object sender, RoutedEventArgs e)
    {
        if (!ready || sender is not Button { Tag:string mode }) return;
        var next = mode == "draft";
        if (next == draftWorkspace) return;
        browseStates[draftWorkspace] = new(scope, SearchBox.Text, ThemeFilter.SelectedItem?.ToString()??"全部主题", browseSelection.Clone(), PageScrollViewer.VerticalOffset);
        draftWorkspace=next;
        var saved=browseStates.GetValueOrDefault(next)??new BrowseState("all","","全部主题",new(),0);
        scope=saved.Scope; browseSelection=saved.Selection.Clone();
        updating=true; SearchBox.Text=saved.Query; ThemeFilter.SelectedIndex=0; updating=false;
        RefreshLibrary(restoreTheme:saved.Theme);PageScrollViewer.UpdateLayout();PageScrollViewer.ScrollToVerticalOffset(saved.Offset);AnimateWorkspace();
    }
    private void RefreshWorkspacePresentation()
    {
        OpeningWorkspaceButton.Background = Brush(draftWorkspace ? "#F2F2F2" : "#321BDD");
        DraftWorkspaceButton.Background = Brush(draftWorkspace ? "#321BDD" : "#F2F2F2");
        OpeningWorkspaceButton.Foreground = Brush(draftWorkspace ? "#181818" : "#FFFFFF");
        DraftWorkspaceButton.Foreground = Brush(draftWorkspace ? "#FFFFFF" : "#181818");
        if(FindName("WorldsDisplayTitle") is TextBlock title)title.Text=draftWorkspace?"DRAFT / SESSION":"RIFT / OVERTURE";
        WorkspaceHint.Text = draftWorkspace ? "选英雄时的赛场配乐" : "进入峡谷时的第一段旋律";
        NavSectionLabel.Text = draftWorkspace ? "赛事" : "分类";
        FeaturedPanel.Visibility = Visibility.Collapsed;
        OpeningSettingsPanel.Visibility=draftWorkspace?Visibility.Collapsed:Visibility.Visible;
        DraftPanel.Visibility=draftWorkspace?Visibility.Visible:Visibility.Collapsed;
        SettingsDrawerHeading.Text=draftWorkspace?"赛事 BP 设置":"游戏开场设置";
        AutomationSettingsTitle.Text=draftWorkspace?"选人时，自动循环播放":"进入峡谷时，自动播放";
        UseForAutomaticButton.Content=draftWorkspace?"设为固定 BP 音乐":"设为固定开场音乐";
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private void RefreshAutomaticSummary()
    {
        var settings=controller.Settings;
        var random=(draftWorkspace?settings.DraftMode:settings.Mode)==Core.SelectionMode.Random;
        var next=controller.Settings.Tracks.FirstOrDefault(t=>t.Id==(draftWorkspace?settings.FixedDraftTrackId:settings.FixedTrackId));
        var candidates=draftWorkspace ? MusicLibrary.Filter(settings.Tracks,"draft",year:settings.DraftYear,competition:settings.DraftCompetition)
            : MusicLibrary.OpeningScope(settings.Tracks,settings.RandomScope);
        var range=draftWorkspace ? $"{(settings.DraftYear>0?settings.DraftYear.ToString():"全部年份")} · {CompetitionRows().FirstOrDefault(c=>c.Id==settings.DraftCompetition)?.Name??"全部赛事"}"
            : MusicLibrary.Category(settings.RandomScope).Name;
        var enabled=draftWorkspace?settings.AutoDraft:settings.AutoPlay;
        var fixedInScope=next is not null && (next.Category=="draft")==draftWorkspace && (!draftWorkspace || candidates.Any(t=>t.Id==next.Id));
        var count=candidates.Count(controller.CanPlay);
        var status=random? $"随机 · {range} · {count} 首可用" : next is null ? "固定 · 尚未设置曲目" : $"固定 · {next.Name}";
        AutomationSummaryLabel.Text=$"自动{(draftWorkspace?" BP":"开场")} {(enabled?"已开启":"已关闭")}  /  {status}";
        SelectionCategory.Text=draftWorkspace?"当前自动 BP 配置":"当前自动开场配置";
        SelectionTitle.Text=random?range+" · 随机":next?.Name??"尚未设置固定音乐";
        SelectionDetail.Text=random?$"{count} 首可用 · 自动避开上一首":next is null?"选择曲目后点击“设为固定音乐”。":!fixedInScope?"这首音乐不在当前自动年份／赛事范围内，请调整范围。":!controller.CanPlay(next)?"这首音乐未勾选、文件缺失或无法播放。":"已设为固定音乐 · "+next.DurationLabel;
        SelectedCover.Source=draftWorkspace && (random || !fixedInScope)
            ? ArtworkService.DraftArtwork(new(){Competition=settings.DraftCompetition,Year=settings.DraftYear})
            : random ? ArtworkService.Category(settings.RandomScope) : ArtworkService.Get(next,draftWorkspace?AutomaticUse(next):null);
        ModeHint.Text=random?"自动范围在这里设置；曲库里的搜索和筛选只影响浏览。":"选中曲目后点击“设为固定音乐”。仅查看或试听不会改变配置。";
    }
    private void UseForAutomaticClick(object sender, RoutedEventArgs e)
    {
        if(Selected is not { } selected || !controller.CanPlay(selected))return;
        if(selected.Category=="draft") {
            controller.Settings.DraftMode=Core.SelectionMode.Fixed;
            controller.Settings.FixedDraftTrackId=selected.Id;
            // A deliberate fixed selection must be eligible within its automatic use filter.
            if(!selected.DraftUses.Any(u=>(controller.Settings.DraftYear==0||u.Year==controller.Settings.DraftYear) &&
                (controller.Settings.DraftCompetition=="all"||u.Competition==controller.Settings.DraftCompetition))) {
                var use=selected.DraftUses.FirstOrDefault();
                controller.Settings.DraftYear=use?.Year??0;controller.Settings.DraftCompetition=use?.Competition??"all";
            }
        } else { controller.Settings.Mode=Core.SelectionMode.Fixed; controller.Settings.FixedTrackId=selected.Id; }
        try { controller.Save(); RefreshState(); }catch(Exception error){ShowError(error);}
    }
    private void SettingsClick(object sender,RoutedEventArgs e)
    {
        settingsReturnFocus=Keyboard.FocusedElement??SettingsButton;
        SettingsOverlay.Visibility=SettingsBackdrop.Visibility=SettingsPanel.Visibility=Visibility.Visible;
        RefreshWorkspacePresentation(); RefreshState();
        WorldsMotion.Reconfigure(SettingsPanel,MotionAllowed,24,0);
        SettingsScrollViewer.UpdateLayout();
        if(sender is Button {Tag:"details"}) { SettingsDrawerHeading.Text="曲目详情";SourceSection.BringIntoView(); }
        else SettingsScrollViewer.ScrollToTop();
        SettingsCloseButton.Focus();
    }
    private void SettingsCloseClick(object sender,RoutedEventArgs e)=>CloseSettings();
    private void CloseSettings() { SettingsOverlay.Visibility=SettingsPanel.Visibility=SettingsBackdrop.Visibility=Visibility.Collapsed; if(settingsReturnFocus is not null)Keyboard.Focus(settingsReturnFocus); settingsReturnFocus=null; }
    private void MinimizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void MaximizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void CloseWindowClick(object sender,RoutedEventArgs e)=>Close();
}
