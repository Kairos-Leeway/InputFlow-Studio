using System.Windows;
namespace InputFlowStudio;
public partial class UpdateWindow : Window
{
    readonly UpdateService service;
    readonly ReleaseUpdate release;
    readonly CancellationTokenSource lifetime = new();
    CancellationTokenSource? operation;
    VerifiedInstaller? downloaded;
    bool downloading;
    public VerifiedInstaller? Installer { get; private set; }
    public bool Skipped { get; private set; }

    public UpdateWindow(UpdateService service, ReleaseUpdate release, bool light)
    {
        InitializeComponent();
        ThemeService.Apply(Resources, light);
        this.service = service;
        this.release = release;
        Heading.Text = "发现新版本 " + release.Version.ToString(3);
        Versions.Text = $"当前版本 {UpdateService.CurrentVersionText}    →    {release.Version.ToString(3)}";
        Notes.Text = string.IsNullOrWhiteSpace(release.Notes) ? "本版本暂未提供更新说明。" : release.Notes;
        DownloadButton.IsEnabled = release.Package != null;
        StatusText.Text = release.Package == null ? "该版本尚无可验证的 x64 安装包，请到发布页查看。" : "下载完成并校验后，由你确认退出程序并开始安装。方案数据会保留。";
        Closing += (_, _) => { lifetime.Cancel(); operation?.Cancel(); };
        Loaded += (_, _) => Activate();
    }

    async void Download(object sender, RoutedEventArgs args)
    {
        if (downloading) return;
        if (downloaded != null) { Installer = downloaded; DialogResult = true; return; }
        downloading = true;
        DownloadButton.IsEnabled = false;
        SkipButton.IsEnabled = false;
        LaterButton.Content = "取消下载";
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try
        {
            StatusText.Text = "正在下载…";
            downloaded = await service.DownloadAsync(release, new Progress<double>(value => { if(downloading&&downloaded==null&&!lifetime.IsCancellationRequested){Progress.Value = value; StatusText.Text = $"正在下载并校验… {value:P0}";} }), operation.Token);
            if (lifetime.IsCancellationRequested) return;
            Progress.Value=1;
            StatusText.Text = "下载和 SHA-256 校验完成。安装前会处理未保存修改，然后退出本程序。";
            DownloadButton.Content = "安装更新并退出";
        }
        catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) StatusText.Text = "下载已取消或超时，可以重试。"; }
        catch (Exception ex) { if (!lifetime.IsCancellationRequested) StatusText.Text = "下载失败：" + ex.Message; }
        finally
        {
            operation.Dispose(); operation = null; downloading = false;
            if (!lifetime.IsCancellationRequested) { DownloadButton.IsEnabled = release.Package != null; SkipButton.IsEnabled = true; LaterButton.Content = "稍后"; }
        }
    }
    void Later(object sender, RoutedEventArgs args) { if (downloading) operation?.Cancel(); else DialogResult = false; }
    void Skip(object sender, RoutedEventArgs args) { Skipped = true; DialogResult = false; }
    void OpenPage(object sender, RoutedEventArgs args) { try { UpdateService.OpenReleasePage(); } catch (Exception ex) { StatusText.Text = ex.Message; } }
}
