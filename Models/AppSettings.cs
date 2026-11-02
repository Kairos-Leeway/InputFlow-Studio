using System.IO;
using System.Text.Json;
namespace InputFlowStudio;
public sealed class AppSettings {
 public string Theme {get;set;}="浅色";
 public bool ShowRecordingHints {get;set;}=true;
 public bool ShowMainAfterRecording {get;set;}=true;
 public bool HideWhenRunning {get;set;}=false;
 public bool HideMainWindow {get;set;}=true;
 public int CountdownSeconds {get;set;}=3;
 public double OverlayOpacity {get;set;}=.95;
 public bool RecordKeyboard {get;set;}=true;
 public bool IncludeControlHotkeys {get;set;}=false;
 public bool RecordMovement {get;set;}=true;
 public int DefaultLoops {get;set;}=1;
 public int DefaultLoopDelay {get;set;}=500;
 public double DefaultSpeed {get;set;}=1;
 public void Validate(){
  if(CountdownSeconds<0||CountdownSeconds>10)throw new ArgumentException("倒计时范围为 0–10 秒");
  if(!double.IsFinite(OverlayOpacity)||OverlayOpacity<.4||OverlayOpacity>1)throw new ArgumentException("浮层透明度范围为 0.4–1");
  if(DefaultLoops<0||DefaultLoopDelay<0||!double.IsFinite(DefaultSpeed)||DefaultSpeed<.1||DefaultSpeed>10)throw new ArgumentException("默认次数和间隔不能为负，默认倍速范围为 0.1–10");
  if(Theme is not ("浅色" or "深色"))throw new ArgumentException("请选择浅色或深色主题");
  if(OverlayCorner is not ("右上角" or "左上角" or "右下角" or "左下角"))throw new ArgumentException("请选择有效的浮层位置");
 }
 public Plan CreatePlan()=>new(){Loops=DefaultLoops,LoopDelay=DefaultLoopDelay,Speed=DefaultSpeed};
 public string Record {get;set;}="F6";
 public string Run {get;set;}="F7";
 public string Stop {get;set;}="F8";
 public string Pause {get;set;}="F9";
 public string Pick {get;set;}="F10";
 public string Add {get;set;}="Ctrl+Alt+A";
 public string OverlayCorner {get;set;}="右上角";
 public AppSettings Copy()=>JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;
 public IEnumerable<(int Id,string Name,string Gesture)> Entries(){yield return (1,"录制",Record);yield return (2,"运行",Run);yield return (3,"停止",Stop);yield return (4,"暂停",Pause);yield return (5,"拾取坐标",Pick);yield return (6,"添加步骤",Add);}
 public static string FilePath=>Path.Combine(MainViewModel.Folder,"settings.json");
 public static AppSettings Load(){try{if(File.Exists(FilePath)){var json=File.ReadAllText(FilePath);var result=JsonSerializer.Deserialize<AppSettings>(json)??new();using var doc=JsonDocument.Parse(json);if(!doc.RootElement.TryGetProperty(nameof(Theme),out _))result.Theme=ThemeService.Load()?"浅色":"深色";result.Validate();return result;}
 var legacy=Path.Combine(MainViewModel.Folder,"keys.txt");if(File.Exists(legacy)){var keys=File.ReadAllText(legacy).Split(',');if(keys.Length==5)return new(){Theme=ThemeService.Load()?"浅色":"深色",Record=keys[0],Run=keys[1],Stop=keys[2],Pause=keys[3],Pick=keys[4]};}}catch{}return new(){Theme=ThemeService.Load()?"浅色":"深色"};}
 public void Save(){Validate();Directory.CreateDirectory(MainViewModel.Folder);File.WriteAllText(FilePath+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));File.Move(FilePath+".tmp",FilePath,true);}
}
