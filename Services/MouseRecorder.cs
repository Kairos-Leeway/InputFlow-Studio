using System.Runtime.InteropServices;
namespace InputFlowStudio;
public interface IMouseRecorder {void Start(Action<Step> captured,IntPtr excludedWindow);void Stop();}
public sealed class MouseRecorder : IMouseRecorder {
 public bool RecordMovement {get;set;}=true;
 public bool RecordKeyboard {get;set;}=true;
 public Shortcut? EmergencyStop {get;set;}
 public bool IncludeControlHotkeys {get;set;}
 public IReadOnlyList<Shortcut> ControlHotkeys {get;set;}=Array.Empty<Shortcut>();
 IntPtr mouseHook,keyboardHook;Native.Hook? mouseCallback,keyboardCallback;RecordedInputBuffer? buffer;Action<Step>? publish;
 public void Start(Action<Step> captured,IntPtr excludedWindow){
  if(mouseHook!=IntPtr.Zero||keyboardHook!=IntPtr.Zero)throw new InvalidOperationException("正在录制");
  publish=captured;var held=new[]{160,161,162,163,164,165,91,92}.Where(key=>(Native.GetAsyncKeyState(key)&0x8000)!=0);
  buffer=new(unchecked((uint)Environment.TickCount),IncludeControlHotkeys,ControlHotkeys,RecordKeyboard?held:null,EmergencyStop);
  mouseCallback=(code,w,l)=>{try{if(code>=0){var m=Marshal.PtrToStructure<Native.MouseHook>(l);bool own=excludedWindow!=IntPtr.Zero&&Native.GetAncestor(Native.WindowFromPoint(m.P),2)==excludedWindow;if((m.Flags&3)==0&&!own){ActionKind? kind=w.ToInt32() switch{0x200=>ActionKind.Move,0x201=>ActionKind.Down,0x202=>ActionKind.Up,0x204=>ActionKind.RightDown,0x205=>ActionKind.RightUp,0x20A=>ActionKind.Wheel,_=>null};if(kind is {} k&&(RecordMovement||k!=ActionKind.Move))buffer?.Mouse(new Step{Kind=k,X=m.P.X,Y=m.P.Y,Duration=0,Wheel=(short)(m.Data>>16)},m.Time);}}}catch(Exception ex){System.Diagnostics.Trace.TraceError("Mouse capture: "+ex);}return Native.CallNextHookEx(mouseHook,code,w,l);};
  keyboardCallback=(code,w,l)=>{try{if(code>=0&&RecordKeyboard){var k=Marshal.PtrToStructure<Native.KeyboardHook>(l);int message=w.ToInt32();if((k.Flags&0x10)==0&&message is 0x100 or 0x101 or 0x104 or 0x105)buffer?.Keyboard((int)k.Key,(int)k.Scan,(k.Flags&1)!=0,message is 0x100 or 0x104,k.Time);}}catch(Exception ex){System.Diagnostics.Trace.TraceError("Keyboard capture: "+ex);}return Native.CallNextHookEx(keyboardHook,code,w,l);};
  mouseHook=Native.SetWindowsHookEx(14,mouseCallback,IntPtr.Zero,0);
  if(mouseHook!=IntPtr.Zero&&RecordKeyboard)keyboardHook=Native.SetWindowsHookEx(13,keyboardCallback,IntPtr.Zero,0);
  if(mouseHook==IntPtr.Zero||(RecordKeyboard&&keyboardHook==IntPtr.Zero)){Unhook();buffer=null;publish=null;throw new InvalidOperationException("无法安装键鼠录制钩子");}
 }
 void Unhook(){if(mouseHook!=IntPtr.Zero)Native.UnhookWindowsHookEx(mouseHook);if(keyboardHook!=IntPtr.Zero)Native.UnhookWindowsHookEx(keyboardHook);mouseHook=keyboardHook=IntPtr.Zero;mouseCallback=keyboardCallback=null;}
 public void Stop(){if(mouseHook==IntPtr.Zero&&keyboardHook==IntPtr.Zero)return;Unhook();var captured=buffer?.Finish(unchecked((uint)Environment.TickCount));buffer=null;var callback=publish;publish=null;if(captured!=null)foreach(var step in captured)callback?.Invoke(step);}
}
