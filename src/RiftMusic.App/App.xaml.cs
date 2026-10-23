using System.IO.Pipes;
using System.Windows;
using RiftMusic.App.Services;
using Forms = System.Windows.Forms;
namespace RiftMusic.App;

public partial class App : Application
{
    private Mutex? mutex;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayImage;
    private AppController? controller;
    private MainWindow? window;
    private readonly CancellationTokenSource lifetime = new();
    private bool ownsMutex;
    private string instanceName = "";
    private Task? listener;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            instanceName = InstanceScope.Name();
            mutex = new Mutex(true, "Local\\" + instanceName, out ownsMutex);
            if (!ownsMutex) ownsMutex = TryOwnMutex();
            if (!ownsMutex) { _ = ActivateExistingAsync(); return; }
            StartPrimary();
        }
        catch (Exception error)
        { LogStartup(error); MessageBox.Show("无法启动峡谷序曲。\n\n" + error.Message + "\n\n诊断记录：" + StartupLog, "峡谷序曲", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    private bool TryOwnMutex()
    {
        try { return mutex!.WaitOne(0); }
        catch (AbandonedMutexException) { return true; }
    }
    private void StartPrimary()
    {
        controller = new(Path.Combine(AppContext.BaseDirectory, "Data"));
        controller.Store.Save(controller.Settings);
        window = new MainWindow(controller); MainWindow = window;
        SetupTray(); window.Show(); controller.Start(); listener = ListenAsync();
    }
    private async Task ActivateExistingAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var pipe = new NamedPipeClientStream(".", instanceName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(3000, timeout.Token);
            await pipe.WriteAsync(new byte[]{1}, timeout.Token);
            var acknowledgement = new byte[1];
            if (await pipe.ReadAsync(acknowledgement, timeout.Token) != 1 || acknowledgement[0] != 1)
                throw new IOException("后台程序没有确认窗口已打开。");
            Shutdown();
        }
        catch (Exception error) when (error is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException)
        {
            LogStartup(error);
            // The original process may have exited while this launch waited.
            if (TryOwnMutex())
            {
                ownsMutex = true;
                try { StartPrimary(); return; }
                catch (Exception startupError) { LogStartup(startupError); }
            }
            MessageBox.Show("未能打开后台窗口。请从系统托盘退出峡谷序曲后重新打开。\n\n" + error.Message + "\n\n诊断记录：" + StartupLog, "峡谷序曲", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    private async Task ListenAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(instanceName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(lifetime.Token);
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                var request = new byte[1];
                if (await pipe.ReadAsync(request, requestTimeout.Token) != 1 || request[0] != 1) continue;
                ShowWindow();
                await pipe.WriteAsync(new byte[]{window?.IsVisible == true ? (byte)1 : (byte)0}, requestTimeout.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
            catch (OperationCanceledException) { /* A stalled caller must not stop the listener. */ }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                LogStartup(error);
                try { await Task.Delay(200, lifetime.Token); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
    private static string StartupLog => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiftMusic", "Logs", "startup.log");
    private static void LogStartup(Exception error)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(StartupLog)!); File.AppendAllText(StartupLog, $"{DateTimeOffset.Now:O} {error}\n"); }
        catch (Exception loggingError) when (loggingError is IOException or UnauthorizedAccessException) { }
    }
    private void SetupTray()
    {
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/RiftMusic;component/Assets/RiftMusic.ico")).Stream;
        using var image = new System.Drawing.Icon(iconStream);
        trayImage = (System.Drawing.Icon)image.Clone();
        tray = new Forms.NotifyIcon { Icon = trayImage, Text = "峡谷序曲 · 开场与 BP 音乐馆", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开峡谷序曲", null, (_, _) => ShowWindow());
        var auto = new Forms.ToolStripMenuItem("自动开场") { CheckOnClick = false };
        auto.Click += (_, _) => { try { controller!.SetEnabled(!controller.Settings.AutoPlay); } catch (Exception e) { MessageBox.Show(e.Message, "峡谷序曲"); } };
        menu.Items.Add(auto);
        var draft = new Forms.ToolStripMenuItem("自动 BP") { CheckOnClick = false };
        draft.Click += (_, _) => { try { controller!.SetDraftEnabled(!controller.Settings.AutoDraft); } catch (Exception e) { MessageBox.Show(e.Message, "峡谷序曲"); } };
        menu.Items.Add(draft); menu.Opening += (_, _) => { auto.Checked = controller!.Settings.AutoPlay; draft.Checked = controller.Settings.AutoDraft; };
        menu.Items.Add("立即停止音乐", null, (_, _) => controller!.StopPlayback()); menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Quit()); tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowWindow();
    }
    private void ShowWindow() { if (window is null) return; window.Show(); if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal; window.Activate(); }
    public void ShowTrayHint()
    {
        if (controller is null || controller.Settings.TrayHintShown) return;
        tray?.ShowBalloonTip(3000, "峡谷序曲仍在运行", "双击托盘图标打开；右键选择“退出”结束运行。", Forms.ToolTipIcon.Info);
        controller.Settings.TrayHintShown = true;
        try { controller.Save(); } catch (IOException) { }
    }
    public void Quit() { if (window is not null) window.AllowClose = true; Shutdown(); }
    protected override void OnExit(ExitEventArgs e)
    {
        lifetime.Cancel();
        if (controller is not null) { try { controller.Store.Save(controller.Settings); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { } controller.Dispose(); }
        if (tray is not null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        trayImage?.Dispose();
        if (ownsMutex) mutex?.ReleaseMutex(); mutex?.Dispose(); base.OnExit(e);
    }
}
