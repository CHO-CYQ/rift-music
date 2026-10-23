using System.IO;
using System.Reflection;
using System.Text;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Threading;
using RiftMusic.App.Services;
using RiftMusic.Core;

internal static class Program
{
    [STAThread] static int Main()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var result = 1;
        dispatcher.InvokeAsync(async () => { try { result = await RunAsync(); } finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); } });
        Dispatcher.Run(); return result;
    }
    static async Task<int> RunAsync()
    {
        var failures = 0;
        Test("opening toggle refuses a fixed BP track", controller => {
            controller.Settings.Mode = SelectionMode.Fixed; controller.Settings.FixedTrackId = "draft";
            Reject(() => controller.SetEnabled(true)); Equal(false, controller.Settings.AutoPlay);
        });
        Test("draft toggle refuses a fixed opening track and never falls back", controller => {
            controller.Settings.DraftMode = SelectionMode.Fixed; controller.Settings.FixedDraftTrackId = "opening";
            Reject(() => controller.SetDraftEnabled(true)); Equal(false, controller.Settings.AutoDraft);
        });
        Test("draft toggle persists independently and preserves opening preferences", controller => {
            controller.Settings.Mode = SelectionMode.Fixed; controller.Settings.FixedTrackId = "opening"; controller.Settings.LastTrackId = "opening";
            controller.Settings.DraftMode = SelectionMode.Random; controller.Settings.DraftYear = 2021; controller.Settings.DraftCompetition = "Worlds";
            controller.SetDraftEnabled(true); var saved = new SettingsStore(controller.Store.DirectoryPath).Load();
            Equal(true, saved.AutoDraft); Equal(SelectionMode.Fixed, saved.Mode); Equal("opening", saved.FixedTrackId); Equal("opening", saved.LastTrackId);
            controller.SetDraftEnabled(false); Equal(false, new SettingsStore(controller.Store.DirectoryPath).Load().AutoDraft);
        });
        Test("draft enable refuses a year competition pair from different uses", controller => {
            controller.Settings.DraftYear = 2021; controller.Settings.DraftCompetition = "LCK";
            Reject(() => controller.SetDraftEnabled(true)); Equal(false, controller.Settings.AutoDraft);
        });
        Test("malformed local lockfile produces safe diagnostics without credentials", controller => {
            File.WriteAllText(Path.Combine(controller.Store.DirectoryPath, "lockfile"), "LeagueClient:1234:54321:literal-secret:http");
            using var client = new DraftClient(controller.Store.DirectoryPath);
            Equal<string?>(null, client.ReadAsync(CancellationToken.None).GetAwaiter().GetResult());
            Equal(false, client.LastError!.Contains("literal-secret"));
        });
        await TestAsync("BP loops once through repeated and missing phase samples then stops on exit", async controller => {
            controller.Settings.LastTrackId = "opening"; controller.SetDraftEnabled(true);
            await Phase(controller, "ChampSelect"); Equal(true, controller.Audio.IsLooping); Equal("draft", controller.Audio.CurrentId);
            Equal("draft", new SettingsStore(controller.Store.DirectoryPath).Load().LastDraftTrackId); Equal("opening", controller.Settings.LastTrackId);
            controller.Audio.Seek(1.5); controller.Audio.PauseOrResume(); Equal(true, controller.Audio.IsPaused);
            await Phase(controller, "ChampSelect"); await Phase(controller, null); await Phase(controller, "ChampSelect");
            Equal(true, controller.Audio.IsPaused); Equal(true, controller.Audio.PositionSeconds >= 1.49);
            await Phase(controller, "Lobby"); Equal(false, controller.Audio.IsBusy);
            await Phase(controller, "ChampSelect"); Equal(true, controller.Audio.IsLooping);
        });
        await TestAsync("turning off BP stops its music and leaves later manual preview playable", async controller => {
            controller.SetDraftEnabled(true); await Phase(controller, "ChampSelect"); Equal(true, controller.Audio.IsBusy);
            controller.SetDraftEnabled(false); Equal(false, controller.Audio.IsBusy);
            await controller.PreviewAsync(controller.Settings.Tracks.Single(t => t.Id == "opening"));
            controller.SetDraftEnabled(false); Equal("opening", controller.Audio.CurrentId); Equal(false, controller.Audio.IsAutomatic);
        });
        await TestAsync("live game stops BP and owns the shared automatic player", async controller => {
            controller.SetDraftEnabled(true); await Phase(controller, "ChampSelect");
            await Phase(controller, "ChampSelect", true); Equal(false, controller.Audio.IsBusy);
            var opening = controller.Settings.Tracks.Single(t => t.Id == "opening");
            await controller.Audio.PlayAsync(opening, controller.Store.ResolveMusic(opening.File), true);
            await Phase(controller, "ChampSelect", true); await Phase(controller, null);
            Equal("opening", controller.Audio.CurrentId); Equal(false, controller.Audio.IsLooping);
        });
        await TestAsync("a decoder failure releases BP ownership before a later manual preview", async controller => {
            controller.SetDraftEnabled(true); await Phase(controller, "ChampSelect");
            controller.Audio.Stop();
            var failed = (Action<string?, string>?)typeof(AudioPlayer).GetField("Failed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller.Audio);
            failed?.Invoke("draft", "decoder failure fixture");
            await controller.PreviewAsync(controller.Settings.Tracks.Single(t => t.Id == "opening"));
            controller.SetDraftEnabled(false); Equal("opening", controller.Audio.CurrentId);
        });
        await TestAsync("prolonged LCU loss stops BP without replaying after reconnect", async controller => {
            controller.SetDraftEnabled(true); await Phase(controller, "ChampSelect");
            typeof(AppController).GetField("lastDraftValid", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(controller, DateTimeOffset.UtcNow.AddSeconds(-6));
            await Phase(controller, null); Equal(false, controller.Audio.IsBusy);
            await Phase(controller, "ChampSelect"); Equal(false, controller.Audio.IsBusy);
            await Phase(controller, "Lobby"); await Phase(controller, "ChampSelect"); Equal(true, controller.Audio.IsLooping);
        });
        Test("LCU certificate validation rejects unrelated trust even when system reports no TLS error", controller => {
            using var client = new DraftClient();
            using var key = RSA.Create(2048);
            var certificateRequest = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://127.0.0.1:54321/lol-gameflow/v1/gameflow-phase");
            var validate = typeof(DraftClient).GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Equal(false, (bool)validate.Invoke(client, [request, certificate, null, SslPolicyErrors.None])!);
            using var remote = new HttpRequestMessage(HttpMethod.Get, "https://example.com/lol-gameflow/v1/gameflow-phase");
            Equal(false, (bool)validate.Invoke(client, [remote, certificate, null, SslPolicyErrors.None])!);
        });
        Console.WriteLine($"{11 - failures}/11 passed"); return failures == 0 ? 0 : 1;

        void Test(string name, Action<AppController> body)
        {
            var directory = Path.Combine(Path.GetTempPath(), "rift-controller-draft-" + Guid.NewGuid()); Directory.CreateDirectory(Path.Combine(directory, "Music"));
            try {
                Seed(directory);
                using var controller = new AppController(directory); body(controller); Console.WriteLine("PASS " + name);
            } catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error); }
            finally { Directory.Delete(directory, true); }
        }
        async Task TestAsync(string name, Func<AppController, Task> body)
        {
            var directory = Path.Combine(Path.GetTempPath(), "rift-audio-draft-" + Guid.NewGuid()); Directory.CreateDirectory(Path.Combine(directory, "Music"));
            try { Seed(directory); using var controller = new AppController(directory); await body(controller); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error); }
            finally { Directory.Delete(directory, true); }
        }
    }
    static Task Phase(AppController controller, string? phase, bool liveGame = false) => (Task)typeof(AppController).GetMethod("ProcessDraftAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, [phase, liveGame])!;
    static void Seed(string directory)
    {
        var opening = new Track { Id = "opening", Name = "Opening", File = "opening.wav", Duration = 4, End = 4 };
        var draft = new Track { Id = "draft", Name = "Draft", File = "draft.wav", Category = "draft", Duration = 4, End = 4, DraftUses = [new() { Year = 2021, Competition = "Worlds" }, new() { Year = 2022, Competition = "LCK" }] };
        Wave(Path.Combine(directory, "Music", "opening.wav")); Wave(Path.Combine(directory, "Music", "draft.wav"));
        new SettingsStore(directory).Save(new() { Tracks = [opening, draft] });
    }
    static void Wave(string path)
    {
        const int rate = 22050, samples = rate * 4;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
        for (var sample = 0; sample < samples; sample++) writer.Write((short)0);
    }
    static void Reject(Action action) { try { action(); throw new Exception("invalid selection was accepted"); } catch (InvalidOperationException) { } }
    static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}"); }
}
