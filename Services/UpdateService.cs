using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO;
namespace InputFlowStudio;

public sealed record UpdatePackage(string Name, Uri DownloadUrl, long Size, string Sha256);
public sealed record ReleaseUpdate(Version Version, string Tag, string Notes, Uri Page, UpdatePackage? Package);
public enum UpdateStatus { Current, Available, NoRelease }
public sealed record UpdateCheck(UpdateStatus Status, ReleaseUpdate? Release = null);
public sealed record VerifiedInstaller(string Path, string Sha256, long Size, Version Version);

/// <summary>Checks public stable releases and verifies pinned-repository installer downloads.</summary>
public sealed class UpdateService : IDisposable
{
    public const string Repository = "Kairos-Leeway/InputFlow-Studio";
    public const string ReleasesPage = "https://github.com/" + Repository + "/releases";
    public static string CurrentVersionText => CurrentVersion.ToString(3);
    public static Version CurrentVersion => Normalize(typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0));
    static readonly Uri Latest = new("https://api.github.com/repos/" + Repository + "/releases/latest");
    const long MaxSize = 512L * 1024 * 1024;
    readonly HttpClient client;
    readonly bool ownsClient;
    readonly string cache;
    readonly Action<string> launch;
    readonly Func<string, string?> installerVersion;

    public UpdateService(HttpClient? client = null, string? cache = null, Action<string>? launch = null, Func<string, string?>? installerVersion = null)
    {
        ownsClient = client == null;
        this.client = client ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        this.cache = Path.GetFullPath(cache ?? Path.Combine(MainViewModel.Folder, "updates"));
        this.launch = launch ?? (path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }));
        this.installerVersion = installerVersion ?? (path => FileVersionInfo.GetVersionInfo(path).ProductVersion);
    }

    static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    public static bool TryVersion(string? tag, out Version version)
    {
        version = new(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var match = Regex.Match(tag.Trim(), @"^[vV]?(\d+\.\d+(?:\.\d+){0,2})(?:\+[0-9A-Za-z.-]+)?$");
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var parsed)) return false;
        version = Normalize(parsed);
        return true;
    }

    static bool ValidUri(Uri uri, string suffix) => uri.Scheme == "https" && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        && uri.Port == 443 && uri.UserInfo.Length == 0 && uri.AbsolutePath.StartsWith("/" + Repository + suffix, StringComparison.OrdinalIgnoreCase);

    public static ReleaseUpdate? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean() || root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!TryVersion(tag, out var version)) return null;
        if (!Uri.TryCreate(root.GetProperty("html_url").GetString(), UriKind.Absolute, out var page) || !ValidUri(page, "/releases/tag/"))
            throw new InvalidDataException("发布页地址与本软件仓库不一致");
        var packages = new List<UpdatePackage>();
        if (root.TryGetProperty("assets", out var assets)) foreach (var asset in assets.EnumerateArray())
        {
            string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            bool named = name.Equals("Setup.exe", StringComparison.OrdinalIgnoreCase) || (name.StartsWith("InputFlowStudio", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Setup", StringComparison.OrdinalIgnoreCase));
            if (!named || Regex.IsMatch(name, @"arm64|x86|ia32", RegexOptions.IgnoreCase)) continue;
            if (!asset.TryGetProperty("state", out var state) || state.GetString() != "uploaded") continue;
            if (!asset.TryGetProperty("size", out var size) || !size.TryGetInt64(out long length) || length < 2 || length > MaxSize) continue;
            string digest = asset.TryGetProperty("digest", out var hash) ? hash.GetString() ?? "" : "";
            if (!Regex.IsMatch(digest, @"^sha256:[0-9a-fA-F]{64}$")) continue;
            if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var uri) || !ValidUri(uri, "/releases/download/")) continue;
            packages.Add(new(name, uri, length, digest[7..].ToLowerInvariant()));
        }
        var package = packages.OrderByDescending(p => p.Name.Contains("x64", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p.Name.StartsWith("InputFlowStudio", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        return new(version, tag, root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "", page, package);
    }

    static HttpRequestMessage Request(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("InputFlowStudio/" + CurrentVersionText);
        request.Headers.Accept.ParseAdd(uri.Host == "api.github.com" ? "application/vnd.github+json" : "application/octet-stream");
        return request;
    }

    public async Task<UpdateCheck> CheckAsync(Version? current = null, CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = Request(Latest);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(UpdateStatus.NoRelease);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) throw new HttpRequestException("GitHub 请求受限，请稍后重试");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 2 * 1024 * 1024) throw new InvalidDataException("版本信息大小异常");
        string json = await response.Content.ReadAsStringAsync(timeout.Token);
        if (json.Length > 2 * 1024 * 1024) throw new InvalidDataException("版本信息大小异常");
        var release = Parse(json);
        if (release == null) return new(UpdateStatus.NoRelease);
        return new(release.Version > Normalize(current ?? CurrentVersion) ? UpdateStatus.Available : UpdateStatus.Current, release);
    }

    public async Task<VerifiedInstaller> DownloadAsync(ReleaseUpdate release, IProgress<double>? progress = null, CancellationToken token = default)
    {
        var package = release.Package ?? throw new InvalidOperationException("该版本没有可验证的 Windows x64 安装包，请前往发布页查看");
        if (!ValidUri(package.DownloadUrl, "/releases/download/") || !Regex.IsMatch(package.Sha256, @"^[0-9a-fA-F]{64}$") || package.Size < 2 || package.Size > MaxSize)
            throw new InvalidDataException("安装包元数据无效");
        Directory.CreateDirectory(cache);
        string destination = Path.Combine(cache, $"InputFlowStudio-{release.Version.ToString(3)}-Setup.exe");
        var downloaded = new VerifiedInstaller(destination, package.Sha256, package.Size, release.Version);
        if (File.Exists(destination) && IsVerified(downloaded) && MatchesInstallerVersion(destination, release.Version)) { progress?.Report(1); return downloaded; }
        string partial = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            using var request = Request(package.DownloadUrl);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != package.Size) throw new InvalidDataException("安装包大小与发布信息不一致");
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                byte[] buffer = new byte[65536];
                int count;
                while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    total += count;
                    if (total > package.Size || total > MaxSize) throw new InvalidDataException("安装包大小超过发布信息");
                    digest.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
                    progress?.Report((double)total / package.Size);
                }
            }
            if (total != package.Size || !Convert.ToHexString(digest.GetHashAndReset()).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装包校验失败，请重新下载");
            if (!HasExeHeader(partial)) throw new InvalidDataException("下载内容不是 Windows 安装程序");
            if (!MatchesInstallerVersion(partial, release.Version)) throw new InvalidDataException("安装包版本与 Release 标签不一致，请到发布页反馈");
            File.Move(partial, destination, true);
            progress?.Report(1);
            return downloaded;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    static bool HasExeHeader(string file) { using var stream = File.OpenRead(file); return stream.ReadByte() == 0x4D && stream.ReadByte() == 0x5A; }
    public static bool IsVerified(VerifiedInstaller file)
    {
        try
        {
            if (!File.Exists(file.Path) || new FileInfo(file.Path).Length != file.Size || !HasExeHeader(file.Path)) return false;
            using var stream = File.OpenRead(file.Path);
            return Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
    }

    public void LaunchInstaller(VerifiedInstaller file)
    {
        string path = Path.GetFullPath(file.Path);
        if (!path.StartsWith(cache.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !IsVerified(file) || !MatchesInstallerVersion(path, file.Version))
            throw new InvalidDataException("安装文件校验失败，未启动安装");
        launch(path);
    }
    bool MatchesInstallerVersion(string path, Version expected) => TryVersion(installerVersion(path), out var actual) && actual == Normalize(expected);
    public static void OpenReleasePage() => Process.Start(new ProcessStartInfo(ReleasesPage) { UseShellExecute = true });
    public void Dispose() { if (ownsClient) client.Dispose(); }
}
