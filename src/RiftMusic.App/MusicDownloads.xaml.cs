using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using RiftMusic.App.Services;

namespace RiftMusic.App;

public partial class MusicDownloads:Window
{
    private readonly AppController controller;
    private readonly List<MusicPack> packs;
    private List<PackRow> rows=[];
    private CancellationTokenSource? transfer;
    private bool busy;
    private sealed class PackRow
    {
        public required MusicPack Pack {get;init;}
        public bool Selected {get;set;}
        public bool Selectable {get;init;}
        public required string Detail {get;init;}
        public string Size=>$"{Pack.Bytes/1048576d:0.#} MiB";
        public string Label=>$"{Pack.Name}，{Detail}，{Size}";
    }
    public MusicDownloads(AppController controller)
    {
        this.controller=controller;
        using var stream=typeof(MusicDownloads).Assembly.GetManifestResourceStream("RiftMusic.music-packs.json")??throw new InvalidDataException("缺少曲包目录，请重新下载程序。");
        packs=JsonSerializer.Deserialize<List<MusicPack>>(stream)??throw new InvalidDataException("曲包目录无法读取。");
        foreach(var pack in packs)pack.Validate();
        InitializeComponent();RefreshRows();
        Height=Math.Min(Height,SystemParameters.WorkArea.Height);MinHeight=Math.Min(MinHeight,SystemParameters.WorkArea.Height);
        Closing+=WindowClosing;
    }
    private void RefreshRows()
    {
        rows=packs.Select(p=>{var count=p.InstalledCount(controller.Settings,controller.Store);return new PackRow{Pack=p,Selectable=count<p.Tracks.Count,Detail=count==p.Tracks.Count?$"{p.Tracks.Count} 首 · 已安装":$"{p.Tracks.Count} 首 · 已安装 {count} 首"};}).ToList();
        PackList.ItemsSource=rows;UpdateSelection();
    }
    private void UpdateSelection()
    {
        if(SelectionLabel is null)return;
        var selected=rows.Where(r=>r.Selected&&r.Selectable).ToList();
        SelectionLabel.Text=selected.Count==0?"请选择需要的曲包":$"已选 {selected.Count} 个曲包 · {selected.Sum(r=>r.Pack.Tracks.Count)} 首 · {selected.Sum(r=>r.Pack.Bytes)/1048576d:0.#} MiB";
        DownloadButton.IsEnabled=!busy&&selected.Count>0;
    }
    private void SelectionChanged(object sender,RoutedEventArgs e)=>Dispatcher.BeginInvoke(UpdateSelection);
    private void SetBusy(bool value){busy=value;PackList.IsEnabled=!value;ImportPackButton.IsEnabled=!value;CancelButton.Visibility=value?Visibility.Visible:Visibility.Collapsed;CancelButton.IsEnabled=true;UpdateSelection();}
    private async void DownloadClick(object sender,RoutedEventArgs e)
    {
        var selected=rows.Where(r=>r.Selected&&r.Selectable).Select(r=>r.Pack).ToList();if(busy||selected.Count==0)return;
        transfer=new();SetBusy(true);long completed=0,total=selected.Sum(p=>p.Bytes);TransferProgress.Value=0;
        using var client=new HttpClient{Timeout=TimeSpan.FromMinutes(15)};client.DefaultRequestHeaders.UserAgent.ParseAdd("RiftMusic/0.1");
        try
        {
            var directory=Path.Combine(controller.Store.DirectoryPath,"Downloads");Directory.CreateDirectory(directory);
            foreach(var pack in selected)
            {
                var path=Path.Combine(directory,Guid.NewGuid().ToString("N")+".part");
                try
                {
                    var start=completed;bool reporting=true;
                    var progress=new Progress<long>(bytes=>{if(!reporting)return;TransferProgress.Value=(start+bytes)/(double)total;StatusLabel.Text=$"正在下载 {pack.Name} · {bytes/1048576d:0.#} / {pack.Bytes/1048576d:0.#} MiB";});
                    await MusicPackDownloader.DownloadAsync(client,pack,path,progress,transfer.Token);
                    reporting=false;
                    StatusLabel.Text=$"正在校验并安装 {pack.Name}…";
                    await InstallAsync(path,pack,transfer.Token);
                    completed+=pack.Bytes;TransferProgress.Value=completed/(double)total;
                }
                finally{try{File.Delete(path);}catch(IOException){}}
            }
            StatusLabel.Text="下载完成，已加入曲库。关闭此窗口即可试听。";
        }
        catch(OperationCanceledException){StatusLabel.Text=transfer.IsCancellationRequested?"已取消；已经安装的曲包已保留。":"下载超时，请重试或从发布页手动下载曲包后导入。";}
        catch(Exception error){StatusLabel.Text=$"未完成：{error.Message} 可从发布页手动下载曲包后导入。";}
        finally{transfer.Dispose();transfer=null;SetBusy(false);RefreshRows();}
    }
    private async Task InstallAsync(string path,MusicPack pack,CancellationToken cancel)
    {
        using var prepared=await Task.Run(()=>MusicPackInstaller.PrepareAsync(path,pack,controller.Store,cancel),cancel);
        cancel.ThrowIfCancellationRequested();prepared.Install(controller.Store,controller.Settings);
    }
    private async void ImportClick(object sender,RoutedEventArgs e)
    {
        if(busy)return;var picker=new OpenFileDialog{Filter="峡谷序曲曲包 (*.zip)|*.zip",Title="选择从本项目 Release 下载的曲包"};if(picker.ShowDialog(this)!=true)return;
        var pack=packs.FirstOrDefault(p=>p.Asset.Equals(Path.GetFileName(picker.FileName),StringComparison.OrdinalIgnoreCase));
        if(pack is null){StatusLabel.Text="请选择本项目发布的分类曲包 ZIP；完整程序包请先解压运行。";return;}
        transfer=new();SetBusy(true);TransferProgress.Value=0;StatusLabel.Text="正在校验并导入曲包…";
        try{await InstallAsync(picker.FileName,pack,transfer.Token);TransferProgress.Value=1;StatusLabel.Text="导入完成，已有收藏和片段设置已保留。";}
        catch(OperationCanceledException){StatusLabel.Text="已取消导入。";}
        catch(Exception error){StatusLabel.Text="导入失败："+error.Message;}
        finally{transfer.Dispose();transfer=null;SetBusy(false);RefreshRows();}
    }
    private void CancelClick(object sender,RoutedEventArgs e){transfer?.Cancel();CancelButton.IsEnabled=false;StatusLabel.Text="正在取消…";}
    private void CloseClick(object sender,RoutedEventArgs e)=>Close();
    private void WindowClosing(object? sender,CancelEventArgs e){if(busy){e.Cancel=true;transfer?.Cancel();StatusLabel.Text="正在取消，请稍后关闭。";}}
    private void ReleaseClick(object sender,RoutedEventArgs e){try{Process.Start(new ProcessStartInfo("https://github.com/CHO-CYQ/rift-music/releases/tag/v0.1"){UseShellExecute=true});}catch(Exception error){StatusLabel.Text="无法打开浏览器："+error.Message;}}
}
