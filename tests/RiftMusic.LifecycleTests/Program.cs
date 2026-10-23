using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RiftMusic.App;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        if(args.Length<1){Console.WriteLine("Pass published RiftMusic.exe and optional screenshot path");return 1;}
        var app=new RiftMusic.App.App();app.InitializeComponent();var result=1;
        app.Dispatcher.InvokeAsync(async ()=>{
            try
            {
                await Task.Delay(600);
                var window=app.MainWindow as MainWindow ?? throw new Exception("Main window missing");
                Check(window.IsVisible,"real app startup displays main window");
                var pipeName=(string)typeof(RiftMusic.App.App).GetField("instanceName",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(app)!;
                window.Close();
                using(var signal=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly))
                {
                    await signal.ConnectAsync(2000);
                    await signal.WriteAsync(new byte[]{1});
                    var acknowledgement=new byte[1];
                    Check(await signal.ReadAsync(acknowledgement).AsTask().WaitAsync(TimeSpan.FromSeconds(5))==1 && acknowledgement[0]==1 && window.IsVisible,"activation acknowledges an actually visible window");
                }
                // A disconnected or stalled caller must not disable all later launches.
                using(var disconnected=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly))await disconnected.ConnectAsync(2000);
                await Task.Delay(100);
                using(var stalled=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly))
                { await stalled.ConnectAsync(2000);await Task.Delay(2300); }
                if(args.Length>1)
                {
                    window.UpdateLayout();var surface=(FrameworkElement)VisualTreeHelper.GetChild(window,0);
                    var bitmap=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    var screenshot=Path.GetFullPath(args[1]);Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);using var image=File.Create(screenshot);encoder.Save(image);
                }
                window.Close();Check(!window.IsVisible,"close hides to tray without exiting");
                var launch=new ProcessStartInfo(Path.GetFullPath(args[0])){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
                var hostTrace=Path.Combine(AppContext.BaseDirectory,"host-trace.txt");if(File.Exists(hostTrace))File.Delete(hostTrace);
                launch.Environment["DOTNET_HOST_TRACE"]="1";launch.Environment["DOTNET_HOST_TRACEFILE"]=hostTrace;
                using var second=Process.Start(launch) ?? throw new Exception("Second instance did not start");
                try { await second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                finally { if(!second.HasExited){second.Kill();await second.WaitForExitAsync();} }
                Check(second.ExitCode==0,"second instance exits successfully");
                var trace=File.ReadAllText(hostTrace);var localHost=Path.Combine(Path.GetDirectoryName(launch.FileName)!,"hostfxr.dll");
                Check((trace.Contains("Loaded library from "+localHost,StringComparison.OrdinalIgnoreCase) || (trace.Contains("Detected Single-File app bundle") && trace.Contains("Using internal fxr") && trace.Contains("Executing as a self-contained app"))),"portable exe loads bundled runtime host");
                var activationDeadline=DateTime.UtcNow.AddSeconds(5);
                while(!window.IsVisible && DateTime.UtcNow<activationDeadline)await Task.Delay(50);
                Check(window.IsVisible,"second instance activates original window");
                for(var cycle=0;cycle<3;cycle++)
                {
                    if(cycle==1)window.WindowState=WindowState.Minimized;else window.Close();
                    using var next=Process.Start(launch) ?? throw new Exception("Repeated launch missing");
                    await next.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    Check(next.ExitCode==0 && window.IsVisible && window.WindowState!=WindowState.Minimized,$"reopen cycle {cycle+1} restores the original window");
                }
                result=0;
            }
            catch(Exception error){Console.WriteLine("FAIL "+error);}
            finally{
                if(typeof(RiftMusic.App.App).GetField("controller",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(app) is RiftMusic.App.Services.AppController owned)owned.Settings.Volume=.573;
                app.Quit();
            }
        });
        app.Run();
        using(var saved=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data","settings.json"))))Check(saved.RootElement.GetProperty("Volume").GetDouble()==.573,"explicit exit persists the last changed setting");
        if(result==0)Console.WriteLine("LIFECYCLE TESTS PASSED");
        return result;
    }
    private static void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
}
