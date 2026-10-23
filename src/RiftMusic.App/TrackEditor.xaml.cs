using System.Globalization;
using System.Windows;
using System.Windows.Controls;
namespace RiftMusic.App;
public partial class TrackEditor : Window
{
    public Track Result { get; private set; }
    public TrackEditor(Track track)
    {
        InitializeComponent(); Result = track;
        NameBox.Text = track.Name; SeasonBox.Text = track.Season; SourceBox.Text = track.Source;
        ArtistBox.Text = track.Artist;
        CategoryBox.ItemsSource = MusicLibrary.Categories.Skip(2); CategoryBox.SelectedValue = track.Category;
        ThemeBox.Text = track.Theme; YearBox.Text = track.Year > 0 ? track.Year.ToString() : "";
        StartBox.Text = track.Start.ToString("0.###", CultureInfo.InvariantCulture); EndBox.Text = track.End.ToString("0.###", CultureInfo.InvariantCulture);
        FadeInBox.Text = track.FadeIn.ToString(CultureInfo.InvariantCulture); FadeOutBox.Text = track.FadeOut.ToString(CultureInfo.InvariantCulture);
        var kind = KindBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Content == track.Kind);
        if (kind is null) { kind = new ComboBoxItem { Content = track.Kind }; KindBox.Items.Add(kind); }
        KindBox.SelectedItem = kind;
        CompetitionBox.SelectedValue = track.DraftUses.FirstOrDefault()?.Competition ?? "Worlds";
        CompetitionBox.IsEnabled = track.DraftUses.Count == 0;
        DraftHint.Text = track.DraftUses.Count > 0 ? $"已核实使用：{string.Join("、",track.DraftUses.Select(u=>$"{u.Year} {u.Competition}"))}。编辑片段会保留这些记录。" : "填写年份与赛事，将音乐加入对应 BP 曲库。";
        DurationHint.Text = $"音频全长 {track.Duration:0.###} 秒 · 支持开场片段与 BP 整曲";
        UpdateDraftFields();
        Height=Math.Min(Height,SystemParameters.WorkArea.Height);MinHeight=Math.Min(MinHeight,SystemParameters.WorkArea.Height);
    }
    private void CloseEditorClick(object sender,RoutedEventArgs e)=>Close();
    private void CategoryChanged(object sender, SelectionChangedEventArgs e) { if(Result is not null) UpdateDraftFields(); }
    private void UpdateDraftFields() => DraftFields.Visibility = CategoryBox.SelectedValue as string == "draft" ? Visibility.Visible : Visibility.Collapsed;
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var numeric = new[] { StartBox.Text, EndBox.Text, FadeInBox.Text, FadeOutBox.Text }; var values = new double[4];
        for (var i = 0; i < 4; i++) if (!double.TryParse(numeric[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i]))
        { ErrorLabel.Text = "时间请填写有效数字，例如 12.5。"; return; }
        var year = 0;
        if (!string.IsNullOrWhiteSpace(YearBox.Text) && (!int.TryParse(YearBox.Text, out year) || year < 2009 || year > 2100))
        { ErrorLabel.Text = "年份请填写 2009 到 2100，未知年份可留空。"; return; }
        var category = CategoryBox.SelectedValue as string ?? "custom";
        if(category=="draft" && year==0 && Result.DraftUses.Count==0) { ErrorLabel.Text="BP 音乐需要填写年份。"; return; }
        var draftUses = Result.DraftUses.Select(u=>new DraftUse { Year=u.Year, Competition=u.Competition, Stage=u.Stage, Evidence=u.Evidence }).ToList();
        if(category=="draft" && draftUses.Count==0)
        {
            var use=draftUses.FirstOrDefault() ?? new DraftUse { Stage="用户导入 BP", Evidence=SourceBox.Text.Trim() };
            use.Year=year; use.Competition=CompetitionBox.SelectedValue as string ?? "Worlds";
            draftUses=[use];
        }
        var next = new Track { Id = Result.Id, File = Result.File, Duration = Result.Duration, Enabled = Result.Enabled,
            Artist=ArtistBox.Text.Trim(), CoverFile=Result.CoverFile, DraftUses=draftUses,
            Category = category, Theme = ThemeBox.Text.Trim(), Year = year,
            Favorite = Result.Favorite, Quality = Result.Quality, SearchTags = Result.SearchTags, PlaybackError = Result.PlaybackError,
            Name = NameBox.Text.Trim(), Season = SeasonBox.Text.Trim(), Source = SourceBox.Text.Trim(), Kind = (string)((ComboBoxItem)KindBox.SelectedItem).Content,
            Start = values[0], End = values[1], FadeIn = values[2], FadeOut = values[3] };
        if (!next.IsValid()) { ErrorLabel.Text = "请输入名称，并确保 0 ≤ 开始 < 结束 ≤ 音频时长，淡入淡出不小于 0。"; return; }
        Result = next; DialogResult = true;
    }
}
