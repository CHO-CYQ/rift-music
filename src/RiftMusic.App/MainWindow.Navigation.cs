using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using RiftMusic.App.Services;

namespace RiftMusic.App;

public partial class MainWindow
{
    private BrowseSelection browseSelection = new();
    private List<BrowseRule> sidebarLeaves = [];
    private readonly Dictionary<bool,HashSet<string>> expandedGroups = new();
    private bool restoringNavigation;
    private readonly Dictionary<bool,bool> multipleFilters = new();
    private readonly Dictionary<bool,BrowseRule> lastSidebarChoice = new();
    private bool MultipleFilters => multipleFilters.GetValueOrDefault(draftWorkspace);
    private sealed record SidebarNode(string Id,string Name,string Count,string Glyph,ImageSource? Logo,
        BrowseRule? Rule,bool? Checked,List<SidebarNode> Children,bool IsMultiple) {
        public bool IsYear=>Rule?.Year!=0&&Rule is not null;
        public string Hint=>IsYear?$"{Name} · {Count} 首":$"{Name} · {Count} 首";
    }

    private void RefreshNavigation()
    {
        var expanded=expandedGroups.GetValueOrDefault(draftWorkspace)??[];
        var nodes=new List<SidebarNode>(); sidebarLeaves=[];
        AllLibraryCount.Text=WorkspaceLibrary.InScope(controller.Settings.Tracks,draftWorkspace).Count().ToString();
        FavoritesLibraryCount.Text=WorkspaceLibrary.InScope(controller.Settings.Tracks,draftWorkspace,"favorites").Count().ToString();
        AllLibraryButton.Background=Brush(!browseSelection.FavoritesOnly?"#ECE9FC":"#F2F2F2");
        AllLibraryButton.Foreground=Brush(!browseSelection.FavoritesOnly?"#321BDD":"#626262");
        FavoritesLibraryButton.Background=Brush(browseSelection.FavoritesOnly?"#ECE9FC":"#F2F2F2");
        FavoritesLibraryButton.Foreground=Brush(browseSelection.FavoritesOnly?"#321BDD":"#626262");
        MultiSelectToggle.IsChecked=MultipleFilters;
        ResetBrowseButton.Visibility=browseSelection.ShowAll?Visibility.Collapsed:Visibility.Visible;
        foreach(var category in WorkspaceLibrary.Navigation(draftWorkspace).Where(c=>c.Id is not ("all" or "favorites"))) {
            var parentRule=new BrowseRule(category.Id);
            var records=WorkspaceLibrary.InScope(controller.Settings.Tracks,draftWorkspace,category.Id).ToList();
            var years=MusicLibrary.Years(records).ToList();
            if(draftWorkspace) years=records.SelectMany(t=>t.DraftUses.Where(u=>"competition:"+u.Competition==category.Id).Select(u=>u.Year)).Where(y=>y>0).Distinct().OrderDescending().ToList();
            if(draftWorkspace)years=years.Concat(category.Id=="competition:FirstStand"?[2025,2026]:Enumerable.Range(2021,6)).Distinct().OrderDescending().ToList();
            if(!draftWorkspace && records.Any(t=>t.Year==0))years.Add(-1);
            var leaves=years.Select(y=>new BrowseRule(category.Id,y)).ToList();sidebarLeaves.AddRange(leaves);
            var children=leaves.Select(rule=>new SidebarNode(category.Id+"|"+rule.Year,rule.Year<0?"未标注":rule.Year.ToString(),records.Count(rule.Matches).ToString(),"",null,rule,MultipleFilters?browseSelection.State(rule,leaves):browseSelection.Rules.Contains(rule),[],MultipleFilters)).ToList();
            var logo=category.Id=="worlds"?ArtworkService.CompetitionLogo("Worlds"):category.Id.StartsWith("competition:")?ArtworkService.CompetitionLogo(category.Id[12..]):null;
            nodes.Add(new(category.Id,category.Name,records.Count.ToString(),logo is null?category.Glyph:"",logo,parentRule,browseSelection.State(parentRule,leaves),children,MultipleFilters));
        }
        restoringNavigation=true;
        try {
            CategoryTree.ItemsSource=nodes;
            CategoryTree.UpdateLayout();
            foreach(var node in nodes)if(CategoryTree.ItemContainerGenerator.ContainerFromItem(node) is TreeViewItem item) {
                item.IsExpanded=expanded.Contains(node.Id);UpdateYearReveal(item,false);
            }
        } finally {restoringNavigation=false;}
    }
    private void SidebarClick(object sender,RoutedEventArgs e)
    {
        if(!ready||updating||sender is not ToggleButton {DataContext:SidebarNode {Rule:{} rule} node})return;
        var restoreFocus=Keyboard.FocusedElement==sender;
        if(MultipleFilters)browseSelection.Toggle(rule,node.Checked!=true,sidebarLeaves);
        else SelectSingleRule(rule);
        if(!MultipleFilters || node.Checked!=true)lastSidebarChoice[draftWorkspace]=rule;
        if(!expandedGroups.TryGetValue(draftWorkspace,out var groups))expandedGroups[draftWorkspace]=groups=[];
        groups.Clear();groups.Add(rule.Scope);
        UpdateBrowseScope();
        RefreshLibrary();PageScrollViewer.ScrollToTop();
        var root=CategoryTree.Items.Cast<SidebarNode>().FirstOrDefault(n=>n.Id==node.Id.Split('|')[0]);
        if(restoreFocus && root is not null && CategoryTree.ItemContainerGenerator.ContainerFromItem(root) is TreeViewItem parent) {
            TreeViewItem target=parent;
            if(node.IsYear) {parent.IsExpanded=true;CategoryTree.UpdateLayout();var child=root.Children.FirstOrDefault(n=>n.Id==node.Id);if(child is not null && parent.ItemContainerGenerator.ContainerFromItem(child) is TreeViewItem leaf)target=leaf;}
            target.UpdateLayout();FindSidebarChoice(target,node.Id)?.Focus();
        }
    }
    private void SelectSingleRule(BrowseRule rule)
    {
        var favorites=browseSelection.FavoritesOnly;
        browseSelection.Clear();browseSelection.FavoritesOnly=favorites;
        browseSelection.Toggle(rule,true,sidebarLeaves);
    }
    private void UpdateBrowseScope() => scope=browseSelection.FavoritesOnly?"favorites":browseSelection.Rules.Select(r=>r.Scope).Distinct().Count()==1?browseSelection.Rules.First().Scope:"all";
    private void SidebarCollectionClick(object sender,RoutedEventArgs e)
    {
        if(!ready||updating||sender is not Button {Tag:string collection})return;
        SelectSidebarScope(collection);lastSidebarChoice.Remove(draftWorkspace);
        expandedGroups.GetValueOrDefault(draftWorkspace)?.Clear();RefreshLibrary();PageScrollViewer.ScrollToTop();
    }
    private void ResetBrowseClick(object sender,RoutedEventArgs e)
    {
        var restoreFocus=Keyboard.FocusedElement==sender;
        var favorites=browseSelection.FavoritesOnly;browseSelection.Clear();browseSelection.FavoritesOnly=favorites;
        lastSidebarChoice.Remove(draftWorkspace);UpdateBrowseScope();RefreshLibrary();PageScrollViewer.ScrollToTop();
        if(restoreFocus)MultiSelectToggle.Focus();
    }
    private void SidebarMultiSelectChanged(object sender,RoutedEventArgs e)
    {
        if(!ready||updating||restoringNavigation)return;
        multipleFilters[draftWorkspace]=MultiSelectToggle.IsChecked==true;
        if(!MultipleFilters && browseSelection.Rules.Count>1) {
            var last=lastSidebarChoice.GetValueOrDefault(draftWorkspace);
            var rule=browseSelection.Rules.FirstOrDefault(r=>last is not null && (r.Covers(last)||last.Covers(r)))??browseSelection.Rules.OrderBy(r=>r.Scope).ThenByDescending(r=>r.Year).First();
            SelectSingleRule(rule);lastSidebarChoice[draftWorkspace]=rule;
        }
        UpdateBrowseScope();RefreshLibrary();MultiSelectToggle.Focus();
    }
    private void SidebarAllYearsClick(object sender,RoutedEventArgs e)
    {
        if(!ready||updating||e.OriginalSource is not Button {Name:"AllYearsButton",Tag:string id})return;
        e.Handled=true;
        var restoreFocus=Keyboard.FocusedElement==e.OriginalSource;
        var rule=new BrowseRule(id);
        if(MultipleFilters)browseSelection.Toggle(rule,true,sidebarLeaves);else SelectSingleRule(rule);
        lastSidebarChoice[draftWorkspace]=rule;UpdateBrowseScope();RefreshLibrary();
        if(restoreFocus && CategoryTree.Items.Cast<SidebarNode>().FirstOrDefault(n=>n.Id==id) is {} node && CategoryTree.ItemContainerGenerator.ContainerFromItem(node) is TreeViewItem item) {
            item.ApplyTemplate();(item.Template.FindName("AllYearsButton",item) as Button)?.Focus();
        }
    }
    private static ToggleButton? FindSidebarChoice(DependencyObject root,string id)
    {
        if(root is ToggleButton {Tag:string tag} box && tag==id)return box;
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++)if(FindSidebarChoice(VisualTreeHelper.GetChild(root,index),id) is {} result)return result;
        return null;
    }
    private void SidebarYearKeyDown(object sender,KeyEventArgs e)
    {
        if(sender is not ToggleButton {DataContext:SidebarNode {IsYear:true} node} || e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End))return;
        var root=CategoryTree.Items.Cast<SidebarNode>().First(n=>n.Children.Contains(node));
        if(CategoryTree.ItemContainerGenerator.ContainerFromItem(root) is not TreeViewItem parent)return;
        var index=root.Children.IndexOf(node);
        var next=e.Key switch{Key.Left=>index-1,Key.Right=>index+1,Key.Up=>index-3,Key.Down=>index+3,Key.Home=>0,_=>root.Children.Count-1};
        next=Math.Clamp(next,0,root.Children.Count-1);
        if(parent.ItemContainerGenerator.ContainerFromItem(root.Children[next]) is TreeViewItem item)FindSidebarChoice(item,root.Children[next].Id)?.Focus();
        e.Handled=true;
    }
    private void RefreshHeroArtwork(List<Track> tracks)
    {
        var rule=browseSelection.Rules.Count==1?browseSelection.Rules.First():null;
        var generic=ArtworkService.Atmosphere(draftWorkspace||rule?.Scope=="worlds");
        ImageSource source=generic;
        var representative=rule?.Year>0?tracks.FirstOrDefault():null;
        if(representative is not null && ArtworkService.Get(representative) is System.Windows.Media.Imaging.BitmapImage verified)source=verified;
        WorldsThemeImage.Source=source;HeroImage.Source=source;
        HeroArtLabel.Text=representative is null||ReferenceEquals(source,generic)?"原创AI音乐馆氛围图；非官方赛事、年份或皮肤配图":ArtworkService.Description(representative);
        WorldsThemeImage.Stretch=Stretch.Uniform;
    }
    private DraftUse? AutomaticUse(Track? track) => track?.DraftUses.FirstOrDefault(u=>
        (controller.Settings.DraftYear==0||u.Year==controller.Settings.DraftYear)&&
        (controller.Settings.DraftCompetition=="all"||u.Competition==controller.Settings.DraftCompetition));
    private void SidebarExpanded(object sender,RoutedEventArgs e)
    {
        if(e.OriginalSource is not TreeViewItem {DataContext:SidebarNode node} item || node.IsYear)return;
        if(restoringNavigation){UpdateYearReveal(item,false);return;}
        if(!expandedGroups.TryGetValue(draftWorkspace,out var groups))expandedGroups[draftWorkspace]=groups=[];
        if(item.IsExpanded) {
            groups.Clear();groups.Add(node.Id);
            foreach(var other in CategoryTree.Items.Cast<SidebarNode>())if(other.Id!=node.Id && CategoryTree.ItemContainerGenerator.ContainerFromItem(other) is TreeViewItem container)container.IsExpanded=false;
        } else groups.Remove(node.Id);
        UpdateYearReveal(item,MotionAllowed);
    }
    private void UpdateYearReveal(TreeViewItem item,bool animate)
    {
        item.ApplyTemplate();
        if(item.Template.FindName("YearViewport",item) is not Border viewport)return;
        if(item.Template.FindName("ExpandToggle",item) is System.Windows.Controls.Primitives.ToggleButton toggle) {
            toggle.ApplyTemplate();
            if(toggle.Template.FindName("ExpandArrow",toggle) is TextBlock {RenderTransform:RotateTransform arrow} arrowLabel) {
                if(arrow.IsFrozen){arrow=arrow.Clone();arrowLabel.RenderTransform=arrow;}
                var previous=arrow.Angle;arrow.BeginAnimation(RotateTransform.AngleProperty,null);arrow.Angle=item.IsExpanded?90:0;
                if(animate){var rotation=new System.Windows.Media.Animation.DoubleAnimation(previous,arrow.Angle,TimeSpan.FromMilliseconds(180)){FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop};rotation.Completed+=(_,_)=>arrow.BeginAnimation(RotateTransform.AngleProperty,null);arrow.BeginAnimation(RotateTransform.AngleProperty,rotation);}
            }
        }
        WorldsMotion.Expand(viewport,item.IsExpanded,animate);
        if(item.IsExpanded && animate) {
            item.UpdateLayout();var index=0;
            foreach(var child in item.Items)if(item.ItemContainerGenerator.ContainerFromItem(child) is TreeViewItem chip)WorldsMotion.Reconfigure(chip,true,-6-(index++%3)*4,0);
        }
    }
    private void SelectSidebarScope(string next)
    {
        browseSelection.Clear();
        if(next=="favorites")browseSelection.FavoritesOnly=true;
        else if(next!="all")browseSelection.Toggle(new(next),true,sidebarLeaves);
        scope=next;
    }
}
