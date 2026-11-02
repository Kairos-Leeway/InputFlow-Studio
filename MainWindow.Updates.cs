using System.Windows;
using System.Windows.Threading;
namespace InputFlowStudio;
public partial class MainWindow
{
    readonly UpdateService updates = new();
    readonly CancellationTokenSource updateLifetime = new();
    readonly DispatcherTimer pendingUpdateTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    ReleaseUpdate? pendingRelease;
    VerifiedInstaller? installerAfterClose;
    bool startupUpdateRequested, checkingUpdates, windowClosed, pendingIsAutomatic, lastCloseFailed;

    void InitializeUpdates()
    {
        pendingUpdateTimer.Tick += (_, _) => TryShowUpdate();
        Loaded += async (_, _) =>
        {
            if (!showRecordingUi || startupUpdateRequested) return;
            startupUpdateRequested = true;
            try { await Task.Delay(1500, updateLifetime.Token); await CheckForUpdatesAsync(false); }
            catch (OperationCanceledException) { }
        };
        Closed += (_, _) =>
        {
            windowClosed = true; updateLifetime.Cancel(); pendingUpdateTimer.Stop();
            try { if (installerAfterClose != null) updates.LaunchInstaller(installerAfterClose); }
            catch (Exception ex) { MessageBox.Show("安装程序未能启动：" + ex.Message + "\n已下载文件：" + installerAfterClose?.Path, "InputFlow Studio 更新"); }
            finally { updates.Dispose(); }
        };
    }


    async Task<string> CheckForUpdatesAsync(bool manual, bool deferPrompt = false)
    {
        if (checkingUpdates) return "正在检查更新，请稍候。";
        if (!manual)
        {
            if (!vm.Settings.CheckUpdatesOnStartup) return "启动检查已关闭。";
            if (vm.Settings.LastUpdateCheckUtc is {} last)
            {
                var elapsed = DateTimeOffset.UtcNow - last;
                if (elapsed >= TimeSpan.Zero && elapsed < TimeSpan.FromHours(24)) return "今天已经检查过更新。";
            }
        }
        checkingUpdates = true;
        try
        {
            vm.Settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            SaveUpdatePreferences();
            if (manual && !deferPrompt) ShowNotice("正在检查 GitHub Releases…");
            var result = await updates.CheckAsync(token: updateLifetime.Token);
            if (windowClosed || closing) return "检查已取消。";
            vm.Settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            SaveUpdatePreferences();
            if (result.Status != UpdateStatus.Available || result.Release == null)
            {
                string message = result.Status == UpdateStatus.NoRelease ? "仓库尚未发布正式版本。" : $"当前版本 {UpdateService.CurrentVersionText} 无需更新。";
                if (manual && !deferPrompt) ShowNotice(message);
                return message;
            }
            if (!manual && string.Equals(vm.Settings.SkippedUpdateVersion, result.Release.Version.ToString(), StringComparison.Ordinal)) return "已跳过该版本。";
            pendingRelease = result.Release; pendingIsAutomatic = !manual;
            pendingUpdateTimer.Start();
            if (!deferPrompt) TryShowUpdate();
            return $"发现新版本 {result.Release.Version.ToString(3)}，关闭设置或等待当前任务结束后可查看下载选项。";
        }
        catch (OperationCanceledException)
        {
            string message = "检查更新已取消或连接超时。";
            if (manual && !windowClosed && !deferPrompt) ShowNotice(message, true);
            return message;
        }
        catch (Exception ex)
        {
            string message = "检查更新失败：" + ex.Message;
            if (manual && !windowClosed && !deferPrompt) ShowNotice(message, true);
            else System.Diagnostics.Trace.TraceInformation(message);
            return message;
        }
        finally { checkingUpdates = false; }
    }

    void SaveUpdatePreferences()
    {
        try { vm.Settings.Save(); }
        catch (Exception ex) { System.Diagnostics.Trace.TraceInformation("更新偏好未能保存：" + ex.Message); }
    }

    void TryShowUpdate()
    {
        if (pendingRelease == null || windowClosed || closing) { pendingUpdateTimer.Stop(); return; }
        if (pendingIsAutomatic && !vm.Settings.CheckUpdatesOnStartup) { pendingRelease = null; pendingUpdateTimer.Stop(); return; }
        if (!vm.CanEdit || modalOpen) return;
        var release = pendingRelease; pendingRelease = null; pendingUpdateTimer.Stop();
        var dialog = new UpdateWindow(updates, release, vm.Settings.Theme == "浅色");
        Ask(() => dialog.ShowDialog());
        if (dialog.Skipped) { vm.Settings.SkippedUpdateVersion = release.Version.ToString(); SaveUpdatePreferences(); }
        if (dialog.Installer == null) return;
        if (!UpdateService.IsVerified(dialog.Installer)) { ShowNotice("安装包校验失败，请重新下载。", true); return; }
        installerAfterClose = dialog.Installer;
        Close();
        if (!windowClosed) { installerAfterClose = null; if(!lastCloseFailed) ShowNotice("已取消安装，下载文件已保留。保存修改后可再次检查更新。"); }
    }
}
