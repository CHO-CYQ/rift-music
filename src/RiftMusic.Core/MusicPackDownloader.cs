namespace RiftMusic.Core;

public static class MusicPackDownloader
{
    public static async Task DownloadAsync(HttpClient client,MusicPack pack,string target,IProgress<long>? progress,CancellationToken cancel,TimeSpan? timeout=null)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(timeout??TimeSpan.FromMinutes(15));cancel=deadline.Token;
        pack.Validate();bool created=false;
        try
        {
            using var response=await client.GetAsync(pack.Url,HttpCompletionOption.ResponseHeadersRead,cancel);
            response.EnsureSuccessStatusCode();
            if(response.RequestMessage?.RequestUri?.Scheme!="https")throw new InvalidDataException("下载地址必须使用 HTTPS。");
            if(response.Content.Headers.ContentLength is long size && size!=pack.Bytes)throw new InvalidDataException("服务器返回的曲包大小不符。");
            await using var input=await response.Content.ReadAsStreamAsync(cancel);
            await using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);created=true;
            var buffer=new byte[81920];long total=0;int n;var last=System.Diagnostics.Stopwatch.StartNew();
            while((n=await input.ReadAsync(buffer,cancel))>0)
            {
                total+=n;if(total>pack.Bytes)throw new InvalidDataException("下载内容大于预期大小。");
                await output.WriteAsync(buffer.AsMemory(0,n),cancel);
                if(last.ElapsedMilliseconds>=100){progress?.Report(total);last.Restart();}
            }
            if(total!=pack.Bytes)throw new InvalidDataException("下载未完成，请重试或手动导入曲包。");
            progress?.Report(total);
        }
        catch{if(created)try{File.Delete(target);}catch(IOException){}throw;}
    }
}
