using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
namespace InputFlowStudio;
public enum ActionKind { Move, LeftClick, RightClick, Down, Up, DoubleClick, Wheel, Wait, RightDown, RightUp, KeyPress, KeyDown, KeyUp }
public abstract class ObservableModel : INotifyPropertyChanged {
 public event PropertyChangedEventHandler? PropertyChanged;
 protected void Set<T>(ref T field,T value,[CallerMemberName]string? name=null){if(EqualityComparer<T>.Default.Equals(field,value))return;field=value;PropertyChanged?.Invoke(this,new(name));OnChanged(name);}
 protected virtual void OnChanged(string? name){}
 protected void Notify(string name)=>PropertyChanged?.Invoke(this,new(name));
}
public class Step : ObservableModel {
 Step? saved;
 public Guid Id {get;set;}=Guid.NewGuid();
 ActionKind kind;int x,y,duration=100,interval=100,wheel=120,number,keyCode=65,scanCode,keyModifiers;bool extended;
 public ActionKind Kind {get=>kind;set=>Set(ref kind,value);}
 public int X {get=>x;set=>Set(ref x,value);}public int Y {get=>y;set=>Set(ref y,value);}
 public int Duration {get=>duration;set=>Set(ref duration,value);}public int Interval {get=>interval;set=>Set(ref interval,value);}
 public int Wheel {get=>wheel;set=>Set(ref wheel,value);}
 public int KeyCode {get=>keyCode;set=>Set(ref keyCode,value);}public int ScanCode {get=>scanCode;set=>Set(ref scanCode,value);}public bool Extended {get=>extended;set=>Set(ref extended,value);}public int KeyModifiers {get=>keyModifiers;set=>Set(ref keyModifiers,value);}
 [JsonIgnore] public bool IsKeyboard=>Kind is ActionKind.KeyPress or ActionKind.KeyDown or ActionKind.KeyUp;
 [JsonIgnore] public bool UsesDuration=>Kind is ActionKind.Move or ActionKind.LeftClick or ActionKind.RightClick or ActionKind.DoubleClick or ActionKind.Wait or ActionKind.KeyPress;
 [JsonIgnore] public string KeyDisplay=>Shortcut.Capture(System.Windows.Input.KeyInterop.KeyFromVirtualKey(KeyCode),(System.Windows.Input.ModifierKeys)KeyModifiers);
 [JsonIgnore] public int Number {get=>number;set=>Set(ref number,value);}
 internal void SetSaved(Step? value){if(ReferenceEquals(saved,value))return;saved=value;DirtyChanged();}
 protected override void OnChanged(string? name){if(name!=nameof(Number)){DirtyChanged();Notify(nameof(KeyDisplay));Notify(nameof(IsKeyboard));Notify(nameof(UsesDuration));}}
 void DirtyChanged(){foreach(var name in new[]{nameof(XChanged),nameof(YChanged),nameof(DurationChanged),nameof(IntervalChanged),nameof(WheelChanged),nameof(KindChanged),nameof(KeyChanged),nameof(IsDirty),nameof(IsNew)})Notify(name);}
 [JsonIgnore] public bool IsNew=>saved==null;
 [JsonIgnore] public bool XChanged=>saved==null||X!=saved.X;
 [JsonIgnore] public bool YChanged=>saved==null||Y!=saved.Y;
 [JsonIgnore] public bool DurationChanged=>saved==null||Duration!=saved.Duration;
 [JsonIgnore] public bool IntervalChanged=>saved==null||Interval!=saved.Interval;
 [JsonIgnore] public bool WheelChanged=>saved==null||Wheel!=saved.Wheel;
 [JsonIgnore] public bool KindChanged=>saved==null||Kind!=saved.Kind;
 [JsonIgnore] public bool KeyChanged=>saved==null||KeyCode!=saved.KeyCode||ScanCode!=saved.ScanCode||Extended!=saved.Extended||KeyModifiers!=saved.KeyModifiers;
 [JsonIgnore] public bool IsDirty=>KeyChanged||XChanged||YChanged||DurationChanged||IntervalChanged||WheelChanged||KindChanged;
 public override string ToString()=> $"{Kind} ({X}, {Y}) {Duration}ms";
}
public class Plan : ObservableModel {
 Plan? saved;bool dirty;
 string name="新方案";int hotKey,loops=1,loopDelay=500;string? shortcut;double speed=1;
 public string Name {get=>name;set=>Set(ref name,value);}
 public int HotKey {get=>hotKey;set=>Set(ref hotKey,value);}
 public string? Shortcut {get=>shortcut;set=>Set(ref shortcut,value);}
 public int Loops {get=>loops;set=>Set(ref loops,value);}public int LoopDelay {get=>loopDelay;set=>Set(ref loopDelay,value);}public double Speed {get=>speed;set=>Set(ref speed,value);}
 public ObservableCollection<Step> Steps {get;set;}=new();
 internal void SetSaved(Plan baseline){saved=baseline;UpdateDirty();}
 internal void UpdateDirty(){if(saved==null)return;dirty=NameChanged||LoopsChanged||LoopDelayChanged||SpeedChanged||Shortcut!=saved.Shortcut||HotKey!=saved.HotKey||!Steps.Select(s=>s.Id).SequenceEqual(saved.Steps.Select(s=>s.Id))||Steps.Any(s=>s.IsDirty);foreach(var key in new[]{nameof(NameChanged),nameof(LoopsChanged),nameof(LoopDelayChanged),nameof(SpeedChanged),nameof(IsDirty)})Notify(key);}
 [JsonIgnore] public bool IsDirty=>dirty;
 [JsonIgnore] public bool NameChanged=>saved!=null&&Name!=saved.Name;
 [JsonIgnore] public bool LoopsChanged=>saved!=null&&Loops!=saved.Loops;
 [JsonIgnore] public bool LoopDelayChanged=>saved!=null&&LoopDelay!=saved.LoopDelay;
 [JsonIgnore] public bool SpeedChanged=>saved!=null&&Speed!=saved.Speed;
 public override string ToString()=>Name;
}
