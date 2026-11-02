using System.Windows.Input;
namespace InputFlowStudio;
public readonly record struct Shortcut(uint Modifiers,uint Key) {
 public static Shortcut Parse(string text){
  if(string.IsNullOrWhiteSpace(text))throw new ArgumentException("快捷键不能为空");
  var parts=text.Split('+',StringSplitOptions.TrimEntries);uint mods=0;Key key=System.Windows.Input.Key.None;
  foreach(var part in parts){uint modifier=part.ToLowerInvariant() switch{"ctrl" or "control"=>2u,"alt"=>1u,"shift"=>4u,"win" or "windows"=>8u,_=>0u};
   if(modifier!=0){if((mods&modifier)!=0)throw new ArgumentException("修饰键重复："+text);mods|=modifier;continue;}
   if(key!=System.Windows.Input.Key.None||!Enum.TryParse(part,true,out key)||!Enum.IsDefined(key)||key is System.Windows.Input.Key.None or System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin or System.Windows.Input.Key.System)throw new ArgumentException("无效快捷键："+text);
  }
  int vk=KeyInterop.VirtualKeyFromKey(key);if(vk==0)throw new ArgumentException("请指定一个普通按键："+text);return new(mods,(uint)vk);
 }
 public static string Capture(Key key,ModifierKeys mods){var parts=new List<string>();if(mods.HasFlag(ModifierKeys.Control))parts.Add("Ctrl");if(mods.HasFlag(ModifierKeys.Alt))parts.Add("Alt");if(mods.HasFlag(ModifierKeys.Shift))parts.Add("Shift");if(mods.HasFlag(ModifierKeys.Windows))parts.Add("Win");parts.Add(key.ToString());return string.Join('+',parts);}
}
public interface IHotkeyRegistry {bool Register(int id,Shortcut shortcut);void Unregister(int id);}
public sealed class NativeHotkeyRegistry(IntPtr window) : IHotkeyRegistry {
 public bool Register(int id,Shortcut s)=>Native.RegisterHotKey(window,id,s.Modifiers|0x4000,s.Key);
 public void Unregister(int id)=>Native.UnregisterHotKey(window,id);
}
public sealed record HotkeyBinding(int Id,string Name,Shortcut Shortcut,Plan? Plan=null);
public sealed class HotkeyManager(IHotkeyRegistry registry) : IDisposable {
 List<HotkeyBinding> current=new();bool suspended;
 public IReadOnlyList<HotkeyBinding> Current=>current;
 public IReadOnlyList<HotkeyBinding> Active=>suspended?Array.Empty<HotkeyBinding>():current;
 public static List<HotkeyBinding> Build(AppSettings settings,IEnumerable<Plan> plans){
  settings.Validate();var result=settings.Entries().Select(e=>new HotkeyBinding(e.Id,e.Name,Shortcut.Parse(e.Gesture))).ToList();int id=100;
  foreach(var plan in plans){Shortcut? shortcut=plan.Shortcut is null?(plan.HotKey>0?new Shortcut(0,(uint)plan.HotKey):null):(string.IsNullOrWhiteSpace(plan.Shortcut)?null:Shortcut.Parse(plan.Shortcut));if(shortcut is {} s)result.Add(new(id++,"方案："+plan.Name,s,plan));}
  var duplicates=result.GroupBy(b=>b.Shortcut).FirstOrDefault(g=>g.Count()>1);if(duplicates!=null)throw new ArgumentException("快捷键重复："+string.Join("、",duplicates.Select(b=>b.Name)));return result;
 }
 void Unregister(IEnumerable<HotkeyBinding> bindings){foreach(var b in bindings)registry.Unregister(b.Id);}
 void Register(IEnumerable<HotkeyBinding> bindings){foreach(var b in bindings)if(!registry.Register(b.Id,b.Shortcut))throw new InvalidOperationException("快捷键被其他程序占用或不受支持："+b.Name);}
 public void Apply(AppSettings settings,IEnumerable<Plan> plans){var next=Build(settings,plans);var old=current;bool wasSuspended=suspended;Unregister(old);
  try{Register(next);current=next;suspended=false;}
  catch(Exception original){Unregister(next);if(!wasSuspended){try{Register(old);}catch(Exception rollback){Unregister(old);suspended=true;throw new AggregateException("快捷键更新及恢复失败，请关闭占用快捷键的程序后重新设置",original,rollback);}}throw;}
 }
 public void Suspend(){Unregister(current);suspended=true;}
 public void Resume(){if(!suspended)return;try{Register(current);suspended=false;}catch{Unregister(current);throw;}}
 public Plan? FindPlan(int id)=>current.FirstOrDefault(b=>b.Id==id)?.Plan;
 public void Dispose(){Unregister(current);current.Clear();}
}
