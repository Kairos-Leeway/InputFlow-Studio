using System.Runtime.InteropServices;
namespace InputFlowStudio;
internal static class Native {
 [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr window,uint flags);
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] public struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(Point p,uint flags);
 [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
 public const uint OwnInputTag=0x49465354;
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint key,uint map);
 [StructLayout(LayoutKind.Sequential)] public struct KeyboardHook {public uint Key,Scan,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Sequential)] public struct Keyboard {public ushort Key,Scan;public uint Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Explicit)] public struct Payload {[FieldOffset(0)] public Mouse Mouse;[FieldOffset(0)] public Keyboard Keyboard;}
 public delegate IntPtr Hook(int code,IntPtr wp,IntPtr lp);
 [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id,Hook cb,IntPtr module,uint thread);
 [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h,int c,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
 [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint n,Input[] inputs,int size);
 [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] public struct MouseHook {public Point P;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Sequential)] public struct Input {public uint Type;public Payload Data;public Mouse M {get=>Data.Mouse;set=>Data.Mouse=value;}public Keyboard K {get=>Data.Keyboard;set=>Data.Keyboard=value;}}
 [StructLayout(LayoutKind.Sequential)] public struct Mouse {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 public static void Send(uint flags,int x=0,int y=0,int data=0){var a=new[]{new Input{M=new Mouse{X=x,Y=y,Data=unchecked((uint)data),Flags=flags}}};if(SendInput(1,a,Marshal.SizeOf<Input>())!=1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
 public static void SendKey(int key,int scan,bool extended,bool down){uint mapped=key==19?0u:scan>0?(uint)scan:MapVirtualKey((uint)key,4);bool ext=extended||(mapped&0xFF00)==0xE000;ushort scanCode=(ushort)(mapped&0xFF);uint flags=(down?0u:2u)|(ext?1u:0u)|(scanCode>0?8u:0u);var inputs=new[]{new Input{Type=1,K=new Keyboard{Key=scanCode>0?(ushort)0:(ushort)key,Scan=scanCode,Flags=flags,Extra=new UIntPtr(OwnInputTag)}}};if(SendInput(1,inputs,Marshal.SizeOf<Input>())!=1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
 public static void Move(int x,int y){int l=GetSystemMetrics(76),t=GetSystemMetrics(77),w=GetSystemMetrics(78),h=GetSystemMetrics(79);Send(0xC001,(int)((x-l)*65535.0/Math.Max(1,w-1)),(int)((y-t)*65535.0/Math.Max(1,h-1)));}
}
