using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RiftMusic.App.Services;

/// <summary>Read-only League Client phase access. Credentials exist only for a local request.</summary>
public sealed class DraftClient : IDisposable
{
    private readonly HttpClient http;
    private readonly X509Certificate2 root;
    private readonly string? configuredDirectory;
    public string? LastError { get; private set; }
    public DraftClient(string? directory = null)
    {
        configuredDirectory = directory;
        using var resource = typeof(DraftClient).Assembly.GetManifestResourceStream("RiftMusic.riotgames.pem")
            ?? throw new InvalidDataException("程序缺少 Riot 接口证书，请重新安装完整版本。");
        using var reader = new StreamReader(resource);
        root = X509Certificate2.CreateFromPem(reader.ReadToEnd());
        var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
        handler.ServerCertificateCustomValidationCallback = Validate;
        http = new(handler) { Timeout = TimeSpan.FromSeconds(1) };
    }
    private bool Validate(HttpRequestMessage request, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (request.RequestUri is not { Scheme: "https", Host: "127.0.0.1" } uri || uri.Port is < 1 or > 65535
            || uri.AbsolutePath != "/lol-gameflow/v1/gameflow-phase" || certificate is null
            || errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable)) return false;
        // LCU certificates can name Riot's client rather than 127.0.0.1. Identity is
        // still pinned to Riot's embedded root and all requests are loopback-only.
        using var custom = new X509Chain();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        custom.ChainPolicy.CustomTrustStore.Add(root);
        custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        custom.ChainPolicy.DisableCertificateDownloads = true;
        custom.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        return custom.Build(certificate);
    }
    public async Task<string?> ReadAsync(CancellationToken cancellation)
    {
        try
        {
            var connection = ReadConnection();
            if (connection is null) { LastError = "未连接到英雄联盟客户端。请登录客户端并进入选人。"; return null; }
            using var request = new HttpRequestMessage(HttpMethod.Get, connection.LoopbackUri);
            connection.Authenticate(request);
            using var response = await http.SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode) { LastError = "客户端阶段接口暂时不可用。"; return null; }
            var phase = DraftDetector.ParsePhase(await response.Content.ReadAsStringAsync(cancellation));
            LastError = phase is null ? "客户端阶段数据暂时无法识别。" : null;
            return phase;
        }
        catch (HttpRequestException) { LastError = "LCU 本机 HTTPS 连接或 Riot 证书校验失败。"; return null; }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { LastError = "LCU 本机接口暂时无响应。"; return null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { LastError = "暂时无法读取本机客户端连接信息。"; return null; }
    }
    private LcuConnection? ReadConnection()
    {
        foreach (var path in Lockfiles())
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 4096) continue;
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                var connection = LcuConnection.Parse(reader.ReadToEnd());
                if (connection is not null && ClientIsRunning(connection.ProcessId)) return connection;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return null;
    }
    private IEnumerable<string> Lockfiles()
    {
        var directories = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredDirectory)) directories.Add(configuredDirectory);
        foreach (var name in new[] { "LeagueClient", "LeagueClientUx" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        if (process.MainModule?.FileName is { } executable && Path.GetDirectoryName(executable) is { } directory)
                            directories.Add(directory);
                    }
                    catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException) { }
                }
            }
        }
        directories.Add(@"D:\WeGameApps\英雄联盟");
        directories.Add(@"C:\Riot Games\League of Legends");
        return directories.SelectMany(directory => new[] { Path.Combine(directory, "lockfile"), Path.Combine(directory, "LeagueClient", "lockfile") })
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
    private static bool ClientIsRunning(int processId)
    {
        try { using var process = Process.GetProcessById(processId); return !process.HasExited && process.ProcessName is "LeagueClient" or "LeagueClientUx"; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception) { return false; }
    }
    public void Dispose() { http.Dispose(); root.Dispose(); }
}
