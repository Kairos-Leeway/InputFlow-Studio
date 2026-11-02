using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using InputFlowStudio;

static class UpdateChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static readonly byte[] Payload=Encoding.UTF8.GetBytes("MZ-test-installer-payload");
    static string Digest(byte[] value)=>Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    static string Release(string tag="v2.2.0",bool pre=false,bool draft=false,string? url=null,string? digest=null,string name="InputFlowStudio-2.2.0-x64-Setup.exe",long? size=null)=>JsonSerializer.Serialize(new{tag_name=tag,draft,prerelease=pre,html_url="https://github.com/"+UpdateService.Repository+"/releases/tag/"+tag,body="更新说明",assets=new[]{new{name,state="uploaded",size=size??Payload.Length,digest=digest??"sha256:"+Digest(Payload),browser_download_url=url??"https://github.com/"+UpdateService.Repository+"/releases/download/"+tag+"/"+name}}});
    static async Task Throws<T>(Func<Task> action,string message) where T:Exception {try{await action();throw new Exception(message);}catch(T){}}
    static UpdateService Service(HttpClient client,string folder,Action<string>? launch=null,string version="2.2.0")=>new(client,folder,launch,_=>version);
    public static async Task Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"InputFlowUpdateTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            Check(UpdateService.TryVersion("v2.10.0",out var big)&&UpdateService.TryVersion("2.9",out var small)&&big>small,"Version comparison is lexical");
            foreach(string invalid in new[]{"v2.2.0-beta","v2.2.0-rc1","v../2.2.0","latest","99999999999999.0.0"})Check(!UpdateService.TryVersion(invalid,out _),"Invalid version accepted");
            Check(UpdateService.Parse(Release(pre:true))==null&&UpdateService.Parse(Release(draft:true))==null,"Preview/draft release accepted");
            Check(UpdateService.Parse(Release(name:"InputFlowStudio-arm64-Setup.exe"))!.Package==null,"Wrong architecture accepted");
            Check(UpdateService.Parse(Release(digest:""))!.Package==null,"Missing digest accepted");
            Check(UpdateService.Parse(Release(url:"https://evil.example/Setup.exe"))!.Package==null,"External download host accepted");
            Check(UpdateService.Parse(Release(url:"http://github.com/"+UpdateService.Repository+"/releases/download/v2.2.0/Setup.exe"))!.Package==null,"HTTP download accepted");
            using(var client=new HttpClient(new StubHandler((request,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Release("v2.10.0"))}))))using(var service=Service(client,folder))
            {Check((await service.CheckAsync(new Version(2,9,0))).Status==UpdateStatus.Available,"New version not detected");Check((await service.CheckAsync(new Version(2,10,0))).Status==UpdateStatus.Current,"Equal version updated");Check((await service.CheckAsync(new Version(3,0,0))).Status==UpdateStatus.Current,"Downgrade offered");}
            using(var client=new HttpClient(new StubHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))))using(var service=Service(client,folder))Check((await service.CheckAsync()).Status==UpdateStatus.NoRelease,"404 not handled");
            using(var client=new HttpClient(new StubHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)))))using(var service=Service(client,folder))await Throws<HttpRequestException>(()=>service.CheckAsync(),"Rate limit ignored");
            int downloads=0,launches=0;using var transport=new HttpClient(new StubHandler((request,_)=>{Check(request.Headers.UserAgent.ToString().Contains("InputFlowStudio"),"Missing GitHub user agent");downloads++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Payload)});}));
            var release=UpdateService.Parse(Release())!;using var updater=Service(transport,folder,_=>launches++);
            var file=await updater.DownloadAsync(release);Check(UpdateService.IsVerified(file)&&downloads==1,"Verified download failed");await updater.DownloadAsync(release);Check(downloads==1,"Verified cache not reused");updater.LaunchInstaller(file);Check(launches==1,"Verified launch not delegated");File.WriteAllText(file.Path,"MZ-tampered");await Throws<InvalidDataException>(()=>{updater.LaunchInstaller(file);return Task.CompletedTask;},"Modified installer launched");Check(launches==1,"Tampered file was executed");
            using(var client=new HttpClient(new StubHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Payload.Select((b,i)=>(byte)(i==4?b+1:b)).ToArray())}))))using(var service=Service(client,Path.Combine(folder,"hash")))await Throws<InvalidDataException>(()=>service.DownloadAsync(release),"Hash mismatch accepted");
            using(var client=new HttpClient(new StubHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[]{77,90})}))))using(var service=Service(client,Path.Combine(folder,"size")))await Throws<InvalidDataException>(()=>service.DownloadAsync(release),"Size mismatch accepted");
            using(var service=Service(transport,Path.Combine(folder,"wrong-version"),version:"2.1.0"))await Throws<InvalidDataException>(()=>service.DownloadAsync(release),"Installer version mismatch accepted");
            using(var client=new HttpClient(new StubHandler(async (_,token)=>{await Task.Delay(Timeout.Infinite,token);return new HttpResponseMessage(HttpStatusCode.OK);})))using(var service=Service(client,Path.Combine(folder,"cancel")))using(var cancel=new CancellationTokenSource())
            {var task=service.DownloadAsync(release,token:cancel.Token);cancel.Cancel();await Throws<OperationCanceledException>(()=>task,"Cancellation ignored");}
            Check(!Directory.GetFiles(folder,"*.part",SearchOption.AllDirectories).Any(),"Partial download files leaked");
            var prefs=new AppSettings{CheckUpdatesOnStartup=false,SkippedUpdateVersion="2.2.0.0",LastUpdateCheckUtc=DateTimeOffset.UtcNow};Check(!prefs.Copy().CheckUpdatesOnStartup&&prefs.Copy().SkippedUpdateVersion=="2.2.0.0","Update preferences not preserved");
            using(var service=Service(transport,Path.Combine(folder,"ui")))
            {var window=new UpdateWindow(service,release,true);((Button)window.FindName("DownloadButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));for(int i=0;i<100&&((Button)window.FindName("DownloadButton")).Content.ToString()!="安装更新并退出";i++)await Task.Delay(10);Check(((Button)window.FindName("DownloadButton")).Content.ToString()=="安装更新并退出"&&window.Installer==null,"Update UI bypassed explicit installation confirmation");}
            Console.WriteLine("UPDATE CHECKS PASSED: stable versions, API errors, pinned URLs, size/hash/version validation, cancellation, cache, tamper rejection, preferences, explicit install confirmation (fake installer only)");
        }
        finally
        {
            string full=Path.GetFullPath(folder);string prefix=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar+"InputFlowUpdateTests-";
            if(full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))Directory.Delete(full,true);
        }
    }
    public static async Task Live(bool download)
    {
        string cache=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,"..","update-live-download"));using var service=new UpdateService(cache:cache,launch:_=>throw new Exception("Live test must not execute installer"));
        var result=await service.CheckAsync(new Version(0,0,0));Console.WriteLine($"LIVE GitHub check: {result.Status}, {result.Release?.Tag}, asset={result.Release?.Package?.Name}, digest={result.Release?.Package?.Sha256}");
        if(download&&result.Release?.Package!=null){int last=-1;var file=await service.DownloadAsync(result.Release,new Progress<double>(p=>{int block=(int)(p*10);if(block!=last){last=block;Console.WriteLine($"Download {block*10}%");}}));Check(UpdateService.IsVerified(file),"Live installer verification failed");Console.WriteLine("LIVE download verified; installer was NOT executed: "+file.Path);}
    }
}
sealed class StubHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> response):HttpMessageHandler
{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>response(request,cancellationToken);}

