using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RiftMusic.App.Services;
using SelectionMode = RiftMusic.Core.SelectionMode;
namespace RiftMusic.App;

public partial class MainWindow : Window
{
    private readonly AppController controller;
    private readonly CancellationTokenSource closing = new();
    private bool ready, updating, importing, seeking, progressUpdating;
    private int libraryCount=-1;
    private readonly System.Windows.Threading.DispatcherTimer searchTimer=new(){Interval=TimeSpan.FromMilliseconds(150)};
    private string scope = "all";
    private bool draftWorkspace;
    private string errors = "";
    private sealed record CompetitionRow(string Id, string Name) { public override string ToString() => Name; }
    private string? lastCoverId;
    private bool playerCoverDirty=true;
    private string? automaticCoverTrackId;
    private DraftUse? automaticCoverUse;
    public bool AllowClose { get; set; }
    public MainWindow(AppController controller)
    {
        this.controller = controller; ArtworkService.Configure(controller.Store.DirectoryPath);
        InitializeWorldsTypography(); InitializeComponent(); InitializeAppearance();
        RandomScopeBox.ItemsSource = MusicLibrary.Categories.Where(c=>c.Id!="draft");
        ThemeFilter.Items.Add("全部主题"); ThemeFilter.SelectedIndex = 0;
        DraftCompetitionBox.ItemsSource = CompetitionRows(); DraftCompetitionBox.SelectedIndex = 0;
        DraftModeBox.ItemsSource = new[] { "每次随机", "固定一首" };
        ready = true; controller.Changed += RefreshState; controller.Audio.Changed += RefreshAudio;
        searchTimer.Tick+=(_,_)=>{searchTimer.Stop();RefreshLibrary(filtersOnly:true);};
        IsVisibleChanged+=(_,_)=>{if(IsVisible && ready){RefreshState();RefreshAudio();}};
        VolumeSlider.Value = controller.Settings.Volume * 100;
        RefreshLibrary(); RefreshState(); RefreshAudio();
        InitializeWindowExperience();
        Loaded += (_,_) => AnimateWorkspace();
        Closing += (_, e) => { if (!AllowClose) { try { controller.Save(); } catch(IOException) { } e.Cancel = true; Hide(); ((App)Application.Current).ShowTrayHint(); } };
        Closed += (_, _) => { searchTimer.Stop();closing.Cancel(); closing.Dispose(); controller.Changed -= RefreshState; controller.Audio.Changed -= RefreshAudio; };
    }
    private Track? Selected => TrackList.SelectedItem as Track;
    private static IEnumerable<CompetitionRow> CompetitionRows() => new[] { new CompetitionRow("all","全部赛事"),new("Worlds","S 赛"),new("MSI","MSI"),new("FirstStand","全球先锋赛"),new("LCK","LCK"),new("LPL","LPL") };
    private void RefreshLibrary(string? select = null, bool filtersOnly = false, string? restoreTheme = null)
    {
        var id = select ?? Selected?.Id;
        searchTimer.Stop();
        filtersOnly=filtersOnly && libraryCount==controller.Settings.Tracks.Count;
        updating = true;
        if(!filtersOnly) {
        DraftYearBox.ItemsSource = new[] { "全部年份" }.Concat(MusicLibrary.Years(controller.Settings.Tracks,"draft")
            .Concat(Enumerable.Range(2021,6)).Concat(controller.Settings.DraftYear>0?[controller.Settings.DraftYear]:Array.Empty<int>())
            .Distinct().OrderDescending().Select(y=>y.ToString())).ToList();
        RefreshNavigation();
        }
        var theme = restoreTheme ?? ThemeFilter.SelectedItem as string ?? "全部主题";
        var inScope = browseSelection.Filter(controller.Settings.Tracks, draftWorkspace).ToList();
        if(!filtersOnly) {
        ThemeFilter.ItemsSource = null; ThemeFilter.Items.Clear();
        ThemeFilter.ItemsSource = new[] { "全部主题" }.Concat(inScope.Select(t => t.ThemeLabel).Distinct().Order()).ToList();
        ThemeFilter.SelectedItem = ThemeFilter.Items.Contains(theme) ? theme : "全部主题";
        ArtworkService.SetBrowsing(browseSelection);
        lastCoverId = null;
        playerCoverDirty=true;
        }
        var tracks = browseSelection.Filter(controller.Settings.Tracks, draftWorkspace, SearchBox.Text,
            ThemeFilter.SelectedIndex > 0 ? ThemeFilter.SelectedItem?.ToString() : null).ToList();
        // Equivalent queries (spaces, capitalization) should not recreate all row visuals.
        if(!filtersOnly || !TrackList.Items.Cast<Track>().SequenceEqual(tracks,ReferenceEqualityComparer.Instance))TrackList.ItemsSource = tracks;
        TrackList.SelectedItem = tracks.FirstOrDefault(t => t.Id == id);
        updating = false;
        libraryCount=controller.Settings.Tracks.Count;
        var category = WorkspaceLibrary.Navigation(draftWorkspace).FirstOrDefault(c => c.Id == scope) ?? WorkspaceLibrary.Navigation(draftWorkspace)[0];
        PageTitle.Text = scope=="all" ? draftWorkspace ? "赛事 BP" : "游戏开场" : category.Name;
        if(!browseSelection.ShowAll && browseSelection.Rules.Select(r=>r.Scope).Distinct().Count()>1)PageTitle.Text="多选音乐集";
        PageCaption.Text = draftWorkspace ? "CHAMP SELECT / MUSIC LIBRARY" : "GAME START / MUSIC LIBRARY";
        SidebarSelectionHint.Text = browseSelection.ShowAll ? browseSelection.FavoritesOnly ? "收藏 · 全部分类" : "显示全部 · 可多选" : $"已选 {browseSelection.Rules.Count} 组 · 点击显示全部可重置";
        ThemeFilter.Visibility=!draftWorkspace && inScope.Select(t=>t.ThemeLabel).Distinct().Count()>1?Visibility.Visible:Visibility.Collapsed;
        HeroEyebrow.Text=draftWorkspace ? "选人配乐 / 赛事与年份" : scope=="all" ? "游戏开场 / 音乐馆" : scope=="worlds" ? "全球总决赛 / 赛事开场" : category.Name+" / 开场音乐";
        HeroTitle.Text=draftWorkspace ? "选人时刻，听见赛场。" : scope=="worlds" ? "那些年的全球总决赛。" : scope=="all" ? "把赛场的旋律，带进下一局。" : "下一局，从喜欢的旋律开始。";
        HeroDescription.Text=draftWorkspace ? "按赛事与年份，找到熟悉的 BP 配乐。" : scope=="worlds" ? "从 S1 到 S15，收藏属于世界赛的开场记忆。" : "世界赛、英雄与峡谷的声音，为你的开场而来。";
        RefreshWorkspacePresentation();
        RefreshHeroArtwork(tracks);
        EmptyState.Visibility = tracks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var hasSearch=!string.IsNullOrWhiteSpace(SearchBox.Text) || ThemeFilter.SelectedIndex>0;
        EmptyTitle.Text = hasSearch ? "没有找到匹配的音乐" : scope == "favorites" ? "把喜欢的音乐留在这里" : "这里暂时没有可用音乐";
        EmptyHint.Text = hasSearch ? "试试其他关键词，或清除搜索与主题筛选。" : scope == "favorites" ? "点击曲目旁的爱心，即可加入当前用途的收藏。" : draftWorkspace ? "当前年份或赛事还没有合格音源。可重置分类筛选，或导入本地 BP 音乐。" : "可重置分类筛选，或导入本地音乐。";
        EmptyResetButton.Content=hasSearch?"清除搜索与主题":"重置分类筛选";
        EmptyResetButton.Visibility=hasSearch || !browseSelection.ShowAll?Visibility.Visible:Visibility.Collapsed;
        SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        SearchClearButton.Visibility=SearchBox.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
        SearchResultCount.Text=$"{tracks.Count} 首";
        ResultLabel.Text = $"{tracks.Count} 首  ·  双击试听  ·  筛选仅影响浏览，勾选参与自动播放";
        RefreshState(); RefreshAudio();
        if(!filtersOnly && IsLoaded)WorldsMotion.Reconfigure(TrackList,MotionAllowed,0,5);
    }
    private void FeaturedClick(object sender,RoutedEventArgs e)
    { if(sender is Button {Tag:string category}) { draftWorkspace=false; SelectSidebarScope(category); updating=true; SearchBox.Clear(); ThemeFilter.SelectedIndex=0; updating=false; RefreshLibrary(); PageScrollViewer.ScrollToTop(); } }
    private void FilterChanged(object sender, TextChangedEventArgs e)
    {
        if(!ready || updating)return;
        SearchHint.Visibility=SearchBox.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
        SearchClearButton.Visibility=SearchBox.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
        searchTimer.Stop();searchTimer.Start();
    }
    private void FilterSelectionChanged(object sender, SelectionChangedEventArgs e) { if (ready && !updating) RefreshLibrary(filtersOnly:true); }
    private void ClearSearch(object sender,RoutedEventArgs e) { updating=true;SearchBox.Clear();updating=false;RefreshLibrary(filtersOnly:true);SearchBox.Focus(); }
    private void ClearFilters(object sender, RoutedEventArgs e)
    {
        var hasSearch=!string.IsNullOrWhiteSpace(SearchBox.Text) || ThemeFilter.SelectedIndex>0;
        updating=true;SearchBox.Clear();ThemeFilter.SelectedIndex=0;updating=false;
        if(!hasSearch) { var favorites=browseSelection.FavoritesOnly;browseSelection.Clear();browseSelection.FavoritesOnly=favorites;UpdateBrowseScope(); }
        RefreshLibrary(filtersOnly:hasSearch);SearchBox.Focus();
    }
    private void FavoriteChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: Track track } box) return;
        track.Favorite = box.IsChecked == true;
        try { controller.Save(); } catch (Exception error) { ShowError(error); }
        RefreshLibrary(track.Id);
    }
    private void RandomScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || updating || RandomScopeBox.SelectedValue is not string selected) return;
        controller.Settings.RandomScope = selected;
        try { controller.Save(); } catch (Exception error) { ShowError(error); }
    }
    private void TrackDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if(e.OriginalSource is not DependencyObject source)return;
        for(var current=source;current is not null && current!=TrackList;current=current is Visual?VisualTreeHelper.GetParent(current):null)
            if(current is System.Windows.Controls.Primitives.ButtonBase)return;
        if(ItemsControl.ContainerFromElement(TrackList,source) is ListBoxItem && Selected is { } t && !controller.Audio.IsAutomatic)_=PreviewTrackAsync(t);
    }
    private void RefreshState()
    {
        if (!ready || IsLoaded && !IsVisible) return;
        updating = true;
        AutoToggle.IsChecked = controller.Settings.AutoPlay;
        FixedMode.IsChecked = controller.Settings.Mode == SelectionMode.Fixed;
        RandomMode.IsChecked = controller.Settings.Mode == SelectionMode.Random;
        RandomScopeBox.SelectedValue = controller.Settings.RandomScope;
        RandomScopeBox.IsEnabled = controller.Settings.Mode == SelectionMode.Random;
        DraftToggle.IsChecked=controller.Settings.AutoDraft;
        DraftModeBox.SelectedIndex=controller.Settings.DraftMode==SelectionMode.Random?0:1;
        DraftYearBox.SelectedItem=controller.Settings.DraftYear>0?controller.Settings.DraftYear.ToString():"全部年份";
        DraftCompetitionBox.SelectedItem=DraftCompetitionBox.Items.Cast<CompetitionRow>().FirstOrDefault(c=>c.Id==controller.Settings.DraftCompetition);
        DraftStatusLabel.Text=controller.DraftStatus; DraftDiagnosticLabel.Text=controller.DraftDiagnostics;
        updating = false;
        StatusLabel.Text = controller.Status; DetailLabel.Text = controller.Detail; DiagnosticLabel.Text = controller.Diagnostics;
        var currentErrors = string.Join('|', controller.Settings.Tracks.Select(t => t.PlaybackError));
        if (currentErrors != errors) { errors = currentErrors; TrackList.Items.Refresh(); }
        VolumeLabel.Text = $"{controller.Settings.Volume * 100:0}%";
        CountLabel.Text = $"{browseSelection.Filter(controller.Settings.Tracks,draftWorkspace).Count()} 首{(draftWorkspace?" BP 音乐":"开场音乐")}";
        RefreshAutomaticSummary();
        EditButton.IsEnabled = RemoveButton.IsEnabled = Selected is not null && !importing;
        PreviewButton.IsEnabled = (controller.Audio.IsBusy || Selected is { } t && t.IsValid() && controller.Exists(t.File)) && !importing;
        ImportButton.IsEnabled = !importing;
        UseForAutomaticButton.IsEnabled = Selected is { } chosen && controller.CanPlay(chosen) && !importing;
        SelectionActions.Visibility=Selected is null?Visibility.Collapsed:Visibility.Visible;
        SelectedActionTitle.Text=Selected?.Name??"";
        SetFixedInlineButton.Content=draftWorkspace?"设为固定 BP":"设为固定开场";
        SetFixedInlineButton.IsEnabled=UseForAutomaticButton.IsEnabled;
    }
    private void RefreshAudio()
    {
        if (!ready || IsLoaded && !IsVisible) return;
        var audio = controller.Audio;
        NowPlaying.Text = audio.IsBusy ? audio.CurrentName : Selected?.Name ?? "选择音乐";
        PlayerSubtitle.Text = audio.IsBusy ? (audio.IsPaused?"已暂停":audio.IsLooping?"BP 音乐 · 循环播放":audio.IsAutomatic ? "正在播放本局开场" : "正在试听") : Selected is { } t ? $"{t.ThemeLabel} · {t.Kind}" : "待播放";
        SourceQuality.Text = Selected?.Quality is { Length: > 0 } q ? q : "用户导入 / 未标注";
        SourceText.Text = Selected?.Source ?? "选择一首音乐，查看来源。";
        SourceTrackTitle.Text = Selected?.Name ?? "尚未选择曲目";
        SourceArtist.Text = Selected?.Artist is {Length:>0} artist ? artist : Selected?.CategoryName ?? "在曲库中单击选择";
        PlayerGlyph.Text = (audio.IsBusy ? controller.Settings.Tracks.FirstOrDefault(t => t.Id == audio.CurrentId) : Selected)?.Glyph ?? "\uE8D6";
        PositionLabel.Text = TimeSpan.FromSeconds(Math.Max(0, audio.PositionSeconds)).ToString(@"m\:ss");
        LengthLabel.Text = audio.IsBusy ? TimeSpan.FromSeconds(audio.ClipDuration).ToString(@"m\:ss") : Selected?.DurationLabel ?? "0:00";
        if(!seeking) { progressUpdating=true; PlaybackProgress.Value=audio.Progress; progressUpdating=false; }
        PlaybackProgress.IsEnabled=audio.IsBusy;
        PreviewIcon.Data=(Geometry)FindResource(audio.IsBusy&&!audio.IsPaused?"PlayerPause":"PlayerPlay");
        var playing=audio.IsBusy?controller.Settings.Tracks.FirstOrDefault(t=>t.Id==audio.CurrentId):Selected;
        if(audio.IsAutomatic && playing?.Category=="draft") {
            if(automaticCoverTrackId!=playing.Id) {automaticCoverTrackId=playing.Id;automaticCoverUse=AutomaticUse(playing)??playing.DraftUses.OrderByDescending(u=>u.Year).FirstOrDefault();playerCoverDirty=true;}
        } else {if(automaticCoverTrackId is not null)playerCoverDirty=true;automaticCoverTrackId=null;automaticCoverUse=null;}
        if(playerCoverDirty || lastCoverId!=playing?.Id) { playerCoverDirty=false;lastCoverId=playing?.Id; PlayerCover.Source=ArtworkService.Get(playing,audio.IsAutomatic&&playing?.Category=="draft"?automaticCoverUse:null); WorldsMotion.Reconfigure(PlayerCover,MotionAllowed,0,0); }
        StopButton.IsEnabled = audio.IsBusy;
        PreviousButton.IsEnabled=NextButton.IsEnabled=!audio.IsAutomatic && TrackList.Items.Count>0;
        PreviewButton.IsEnabled = (audio.IsBusy || Selected is { } selected && selected.IsValid() && controller.Exists(selected.File)) && !importing;
    }
    private void DraftChanged(object sender,RoutedEventArgs e)
    { if(!ready||updating)return; try { controller.SetDraftEnabled(DraftToggle.IsChecked==true); } catch(Exception error) { ShowError(error); RefreshState(); } }
    private void DraftSettingChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ready||updating)return;
        controller.Settings.DraftMode=DraftModeBox.SelectedIndex==1?SelectionMode.Fixed:SelectionMode.Random;
        controller.Settings.DraftYear=int.TryParse(DraftYearBox.SelectedItem?.ToString(),out var year)?year:0;
        controller.Settings.DraftCompetition=(DraftCompetitionBox.SelectedItem as CompetitionRow)?.Id??"all";
        try { controller.Save(); RefreshState(); } catch(Exception error) { ShowError(error); }
    }
    private void SeekStarted(object sender,MouseButtonEventArgs e)=>seeking=true;
    private void SeekChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    { if(ready&&!progressUpdating&&controller.Audio.IsBusy) controller.Audio.Seek(e.NewValue*controller.Audio.ClipDuration); }
    private void SeekEnded(object sender,MouseButtonEventArgs e)=>seeking=false;
    private void SeekCaptureLost(object sender,MouseEventArgs e)=>seeking=false;
    private async void PreviousClick(object sender,RoutedEventArgs e)=>await StepTrackAsync(-1);
    private async void NextClick(object sender,RoutedEventArgs e)=>await StepTrackAsync(1);
    private async Task StepTrackAsync(int direction)
    {
        if(controller.Audio.IsAutomatic) return;
        var tracks=TrackList.Items.Cast<Track>().Where(t=>t.IsValid()&&controller.Exists(t.File)).ToList(); if(tracks.Count==0)return;
        var current=tracks.FindIndex(t=>t.Id==(controller.Audio.CurrentId??Selected?.Id));
        if(current<0) current=direction>0?-1:0;
        var next=tracks[(current+direction+tracks.Count)%tracks.Count]; TrackList.SelectedItem=next; TrackList.ScrollIntoView(next); await PreviewTrackAsync(next);
    }
    private void ShowError(Exception e) => MessageBox.Show(this, e.Message, "峡谷序曲", MessageBoxButton.OK, MessageBoxImage.Information);
    private void AutoChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || updating) return;
        try { controller.SetEnabled(AutoToggle.IsChecked == true); } catch (Exception error) { ShowError(error); RefreshState(); }
    }
    private void ModeChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || updating) return;
        controller.Settings.Mode = RandomMode.IsChecked == true ? SelectionMode.Random : SelectionMode.Fixed;
        try { controller.Save(); RefreshState(); } catch (Exception error) { ShowError(error); }
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || updating) return;
        RefreshState(); RefreshAudio();
    }
    private void TrackEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: Track track } box)
        {
            track.Enabled = box.IsChecked == true;
            try { controller.Save(); } catch (Exception error) { ShowError(error); }
            RefreshLibrary(track.Id);
        }
    }
    private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        controller.Settings.Volume = VolumeSlider.Value / 100; controller.Audio.Volume = controller.Settings.Volume;
        VolumeLabel.Text = $"{VolumeSlider.Value:0}%";
    }
    private async void ImportClick(object sender, RoutedEventArgs e)
    {
        if (importing) return;
        var importAsDraft = draftWorkspace;
        var dialog = new OpenFileDialog { Title = importAsDraft ? "导入 BP 音乐" : "导入开场音乐", Filter = "音乐文件 (*.mp3;*.wav)|*.mp3;*.wav", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        importing = true; RefreshState();
        try
        {
            var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            if (extension is not (".mp3" or ".wav")) throw new InvalidDataException("请选择 MP3 或 WAV 文件。");
            var duration = await AudioPlayer.ProbeAsync(dialog.FileName, closing.Token);
            if (closing.IsCancellationRequested) return;
            var track = new Track { Name = Path.GetFileNameWithoutExtension(dialog.FileName), Duration = duration, End = importAsDraft?duration:Math.Min(60, duration), Category=importAsDraft?"draft":"custom" };
            track.File = track.Id + extension;
            var editor = new TrackEditor(track) { Owner = this };
            if (editor.ShowDialog() != true) return;
            var target = controller.Store.ResolveMusic(editor.Result.File); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(dialog.FileName, target, false);
            controller.Settings.Tracks.Add(editor.Result);
            if(editor.Result.Category=="draft")controller.Settings.FixedDraftTrackId ??= editor.Result.Id;
            else controller.Settings.FixedTrackId ??= editor.Result.Id;
            try { controller.Save(); }
            catch { controller.Settings.Tracks.Remove(editor.Result); throw; }
            draftWorkspace = editor.Result.Category=="draft"; SelectSidebarScope(draftWorkspace ? "all" : editor.Result.Category); updating = true; SearchBox.Clear(); ThemeFilter.SelectedIndex = 0; updating = false;
            RefreshLibrary(editor.Result.Id);
            controller.SetStatus("音乐已添加", "先试听，再打开自动播放设置选择播放方式。");
        }
        catch (OperationCanceledException) { if (!closing.IsCancellationRequested) ShowError(new InvalidDataException("读取音频超时，请尝试 PCM WAV 格式。")); }
        catch (Exception error) { if (!closing.IsCancellationRequested) ShowError(error); }
        finally { importing = false; if (!closing.IsCancellationRequested) { RefreshState(); RefreshAudio(); } }
    }
    private void EditClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } track) return;
        var editor = new TrackEditor(track) { Owner = this };
        if (editor.ShowDialog() != true) return;
        controller.Settings.Tracks[controller.Settings.Tracks.IndexOf(track)] = editor.Result;
        try { controller.Save(); RefreshLibrary(track.Id); } catch (Exception error) { ShowError(error); }
    }
    private void RemoveClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } track) return;
        controller.Settings.Tracks.Remove(track);
        if (controller.Settings.FixedTrackId == track.Id) controller.Settings.FixedTrackId = null;
        if (controller.Settings.FixedDraftTrackId == track.Id) controller.Settings.FixedDraftTrackId = null;
        try { controller.Save(); RefreshLibrary(); } catch (Exception error) { ShowError(error); }
    }
    private async void PreviewClick(object sender, RoutedEventArgs e)
    {
        if(controller.Audio.IsBusy) { controller.Audio.PauseOrResume(); return; }
        if (Selected is not { } t) return;
        await PreviewTrackAsync(t);
    }
    private async Task PreviewTrackAsync(Track track)
    { try { await controller.PreviewAsync(track); } catch(Exception error) { if(!closing.IsCancellationRequested) ShowError(error); } }
    private void StopClick(object sender, RoutedEventArgs e) => controller.StopPlayback();
}
