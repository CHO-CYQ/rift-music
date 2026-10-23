using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
namespace RiftMusic.App.Services;

public sealed class LiveGameClient : IDisposable
{
    private readonly HttpClient http;
    private readonly X509Certificate2 root;
    public string? LastError { get; private set; }
    public LiveGameClient()
    {
        using var resource = typeof(LiveGameClient).Assembly.GetManifestResourceStream("RiftMusic.riotgames.pem")
            ?? throw new InvalidDataException("程序缺少 Riot 接口证书，请重新安装完整版本。");
        using var reader = new StreamReader(resource);
        root = X509Certificate2.CreateFromPem(reader.ReadToEnd());
        var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
        handler.ServerCertificateCustomValidationCallback = Validate;
        http = new(handler) { BaseAddress = new Uri("https://127.0.0.1:2999/liveclientdata/"), Timeout = TimeSpan.FromSeconds(1) };
    }
    private bool Validate(HttpRequestMessage request, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (request.RequestUri?.Host != "127.0.0.1" || request.RequestUri.Port != 2999 || certificate is null) return false;
        if (errors == SslPolicyErrors.None) return true;
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) || errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable)) return false;
        using var custom = new X509Chain();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        custom.ChainPolicy.CustomTrustStore.Add(root); custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return custom.Build(certificate);
    }
    public async Task<GameSnapshot?> ReadAsync(CancellationToken cancellation)
    {
        try
        {
            var stats = await http.GetStringAsync("gamestats", cancellation);
            var observed = DateTimeOffset.UtcNow;
            var events = await http.GetStringAsync("eventdata", cancellation);
            var active = await http.GetStringAsync("activeplayername", cancellation);
            var players = await http.GetStringAsync("playerlist", cancellation);
            var parsed = GameParser.Parse(stats, events, active, players, observed);
            LastError = parsed is null ? "游戏接口返回的数据暂时无法识别。" : null;
            return parsed;
        }
        catch (HttpRequestException) { LastError = "尚未连接到对局。请进入召唤师峡谷；若已在局内，请检查本机 2999 接口和证书。"; return null; }
        catch (TaskCanceledException) when (!cancellation.IsCancellationRequested) { LastError = "游戏接口暂时无响应。"; return null; }
    }
    public void Dispose() { http.Dispose(); root.Dispose(); }
}
