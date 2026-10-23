using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RiftMusic.Core;
public static class GameParser
{
    public static GameSnapshot? Parse(string stats, string events, string activeName, string players, DateTimeOffset observedAt)
    {
        try
        {
            using var s = JsonDocument.Parse(stats); using var e = JsonDocument.Parse(events);
            using var a = JsonDocument.Parse(activeName); using var p = JsonDocument.Parse(players);
            var time = s.RootElement.GetProperty("gameTime").GetDouble();
            var map = s.RootElement.GetProperty("mapNumber").GetInt32();
            if (!double.IsFinite(time) || time < 0 || time > 86400) return null;
            var active = a.RootElement.GetString(); var roster = new List<string>(); var found = false;
            foreach (var player in p.RootElement.EnumerateArray())
            {
                var id = Text(player, "riotId"); var old = Text(player, "summonerName");
                if (id.Length == 0) id = old;
                found |= !string.IsNullOrWhiteSpace(active) && (active == id || active == old);
                roster.Add(id + "\t" + Text(player, "team"));
            }
            roster.Sort(StringComparer.Ordinal);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(active + "\n" + string.Join("\n", roster))));
            var start = e.RootElement.GetProperty("Events").EnumerateArray().Any(ev => Text(ev, "EventName") == "GameStart");
            return new(time, observedAt, hash, map, start, found);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { return null; }
    }
    private static string Text(JsonElement element, string key) => element.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
