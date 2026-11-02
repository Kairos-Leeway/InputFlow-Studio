using System.Diagnostics;
using System.Runtime.InteropServices;
namespace InputFlowStudio;
/// <summary>Distinguishes this app's injected control chords from actual emergency hotkeys.</summary>
public sealed class HotkeyOriginGate : IDisposable {
 readonly Func<IReadOnlyList<HotkeyBinding>> bindings;
 readonly Action? emergencyStop;
 readonly Dictionary<int,(int Count,long Time)> pending=new();
 readonly HashSet<int> physicalPressed=new(),injectedPressed=new();readonly HashSet<int> active=new();
 IntPtr hook;Native.Hook? callback;
 public HotkeyOriginGate(Func<IReadOnlyList<HotkeyBinding>> bindings,bool install=true,Action? emergencyStop=null){this.bindings=bindings;this.emergencyStop=emergencyStop;if(!install)return;foreach(int key in new[]{160,161,162,163,164,165,91,92})if((Native.GetAsyncKeyState(key)&0x8000)!=0)physicalPressed.Add(key);
  callback=(code,w,l)=>{try{if(code>=0){var k=Marshal.PtrToStructure<Native.KeyboardHook>(l);int message=w.ToInt32();if(message is 0x100 or 0x101 or 0x104 or 0x105)Observe((int)k.Key,message is 0x100 or 0x104,(k.Flags&0x10)!=0&&k.Extra==new UIntPtr(Native.OwnInputTag));}}catch(Exception ex){Trace.TraceError("Hotkey origin: "+ex);}return Native.CallNextHookEx(hook,code,w,l);};
  hook=Native.SetWindowsHookEx(13,callback,IntPtr.Zero,0);if(hook==IntPtr.Zero)throw new InvalidOperationException("无法安装回放快捷键保护钩子");
 }
 public void Observe(int key,bool down,bool ownInjected){var source=ownInjected?injectedPressed:physicalPressed;if(down)source.Add(key);else source.Remove(key);var pressed=physicalPressed.Union(injectedPressed).ToHashSet();var now=Stopwatch.GetTimestamp();var next=new HashSet<int>();foreach(var binding in bindings()){uint mods=KeyCodes.Modifiers(pressed);if(!ownInjected&&down&&binding.Id==3&&key==binding.Shortcut.Key&&(mods&binding.Shortcut.Modifiers)==binding.Shortcut.Modifiers&&mods!=binding.Shortcut.Modifiers){pending.Remove(binding.Id);emergencyStop?.Invoke();}if(!KeyCodes.Matches(binding.Shortcut,pressed))continue;next.Add(binding.Id);if(!ownInjected&&down){pending.Remove(binding.Id);continue;}if(ownInjected&&!active.Contains(binding.Id)){var old=pending.GetValueOrDefault(binding.Id);pending[binding.Id]=(old.Count+1,now);}}active.Clear();active.UnionWith(next);}
 public bool ConsumeInjected(int id){if(!pending.TryGetValue(id,out var value))return false;if(Stopwatch.GetElapsedTime(value.Time).TotalSeconds>2){pending.Remove(id);return false;}if(value.Count<=1)pending.Remove(id);else pending[id]=(value.Count-1,value.Time);return true;}
 public void Dispose(){if(hook!=IntPtr.Zero)Native.UnhookWindowsHookEx(hook);hook=IntPtr.Zero;callback=null;pending.Clear();}
}
