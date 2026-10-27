using System.Windows;
namespace RiftMusic.App;
public partial class MainWindow
{
    private void DownloadMusicClick(object sender,RoutedEventArgs e)
    {
        try{new MusicDownloads(controller){Owner=this}.ShowDialog();}
        catch(Exception error){ShowError(error);}
        finally{RefreshLibrary();RefreshState();RefreshAudio();}
    }
}
