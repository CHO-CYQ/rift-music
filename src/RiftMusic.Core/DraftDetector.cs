using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RiftMusic.Core;

public enum DraftDecision { Waiting, Play, Handled, Stop }

/// <summary>A missing sample cannot prove that a selection session ended.</summary>
public sealed class DraftDetector
{
    private static readonly HashSet<string> Phases = new(StringComparer.Ordinal)
    {
        "None", "Lobby", "Matchmaking", "CheckedIntoTournament", "ReadyCheck", "ChampSelect",
        "GameStart", "InProgress", "Reconnect", "WaitingForStats", "PreEndOfGame", "EndOfGame",
        "TerminatedInError", "WatchInProgress"
    };
    public bool IsChampSelect { get; private set; }
    public DraftDecision Observe(string? phase)
    {
        if (phase is null || !Phases.Contains(phase)) return DraftDecision.Waiting;
        if (phase == "ChampSelect")
        {
            if (IsChampSelect) return DraftDecision.Handled;
            IsChampSelect = true; return DraftDecision.Play;
        }
        if (!IsChampSelect) return DraftDecision.Waiting;
        IsChampSelect = false; return DraftDecision.Stop;
    }
    public static string? ParsePhase(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.String) return null;
            var phase = document.RootElement.GetString();
            return phase is not null && Phases.Contains(phase) ? phase : null;
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>The authentication secret is held in memory and never formatted or serialized.</summary>
public sealed class LcuConnection
{
    private readonly string password;
    public int ProcessId { get; }
    public int Port { get; }
    public Uri LoopbackUri => new($"https://127.0.0.1:{Port}/lol-gameflow/v1/gameflow-phase");
    private LcuConnection(int processId, int port, string password) { ProcessId = processId; Port = port; this.password = password; }
    public static LcuConnection? Parse(string content)
    {
        var fields = content.Trim().Split(':');
        if (fields.Length != 5 || fields[0] is not ("LeagueClient" or "LeagueClientUx")
            || !int.TryParse(fields[1], out var processId) || processId <= 0
            || !int.TryParse(fields[2], out var port) || port is < 1 or > 65535
            || string.IsNullOrWhiteSpace(fields[3]) || fields[3].Any(char.IsControl) || fields[4] != "https") return null;
        return new(processId, port, fields[3]);
    }
    public void Authenticate(HttpRequestMessage request)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri != LoopbackUri) throw new ArgumentException("LCU 认证仅用于本机只读阶段接口。", nameof(request));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("riot:" + password)));
    }
    public override string ToString() => $"LCU 127.0.0.1:{Port} · process {ProcessId}";
}
