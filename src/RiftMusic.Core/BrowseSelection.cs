namespace RiftMusic.Core;

public sealed record BrowseRule(string Scope, int Year = 0)
{
    public bool Covers(BrowseRule other) => Scope == other.Scope && (Year == 0 || Year == other.Year);
    public bool Matches(Track track) => track.Category == "draft"
        ? Scope.StartsWith("competition:", StringComparison.Ordinal) && track.DraftUses.Any(u =>
            u.Competition == Scope[12..] && (Year == 0 || u.Year == Year))
        : track.Category == Scope && (Year == 0 || track.Year == Year || Year == -1 && track.Year == 0);
}

/// <summary>Sidebar choices are a union of category/year pairs, never a Cartesian product.</summary>
public sealed class BrowseSelection
{
    private readonly HashSet<BrowseRule> rules = [];
    public IReadOnlyCollection<BrowseRule> Rules => rules;
    public bool ShowAll { get; private set; } = true;
    public bool FavoritesOnly { get; set; }
    public void Clear() { rules.Clear(); ShowAll = true; FavoritesOnly = false; }
    public BrowseSelection Clone()
    { var copy=new BrowseSelection {ShowAll=ShowAll,FavoritesOnly=FavoritesOnly};copy.rules.UnionWith(rules);return copy; }
    public void Toggle(BrowseRule rule, bool selected, IEnumerable<BrowseRule> leaves)
    {
        ShowAll=false;
        if(selected) {
            if(rules.Any(r=>r.Covers(rule)))return;
            rules.RemoveWhere(rule.Covers);rules.Add(rule);
        } else {
            // Split a selected parent when a single year is removed.
            foreach(var ancestor in rules.Where(r=>r.Covers(rule)).ToList()) {
                rules.Remove(ancestor);
                foreach(var leaf in leaves.Where(l=>ancestor.Covers(l)&&!rule.Covers(l)))rules.Add(leaf);
            }
            rules.RemoveWhere(rule.Covers);
        }
    }
    public bool? State(BrowseRule rule,IEnumerable<BrowseRule> leaves)
    {
        if(ShowAll)return false;
        if(rules.Any(r=>r.Covers(rule)))return true;
        if(rule.Year!=0)return false;
        var children=leaves.Where(rule.Covers).ToList();
        var selected=children.Count(l=>rules.Any(r=>r.Covers(l)));
        return selected==0?false:selected==children.Count?true:null;
    }
    public IEnumerable<Track> Filter(IEnumerable<Track> tracks,bool draft,string? query=null,string? theme=null)
    {
        var purpose=tracks.Where(t=>(t.Category=="draft")==draft);
        if(FavoritesOnly)purpose=purpose.Where(t=>t.Favorite);
        if(!ShowAll)purpose=purpose.Where(t=>rules.Any(r=>r.Matches(t)));
        return MusicLibrary.Filter(purpose,query:query,theme:theme);
    }
    public DraftUse? RepresentativeUse(Track track) => track.DraftUses
        .Where(u=>ShowAll || rules.Any(r=>r.Scope=="competition:"+u.Competition&&(r.Year==0||r.Year==u.Year)))
        .OrderByDescending(u=>u.Year).ThenBy(u=>u.Competition=="Worlds"?0:1).ThenBy(u=>u.Competition).FirstOrDefault();
}
