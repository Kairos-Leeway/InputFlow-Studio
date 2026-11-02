namespace InputFlowStudio;
public enum MouseButton { Left, Right }
public readonly record struct ScreenPoint(int X,int Y);
public interface IMouseOutput {
 ScreenPoint Position();
 void Move(int x,int y);
 void Button(MouseButton button,bool down);
 void Wheel(int delta);
 void ReleaseAll();
}
public sealed class NativeMouseOutput : IMouseOutput {
 readonly Action<uint> sendButton;
 readonly HashSet<MouseButton> held=new();
 public NativeMouseOutput(Action<uint>? sendButton=null){this.sendButton=sendButton??(flags=>Native.Send(flags));}
 public ScreenPoint Position(){if(!Native.GetCursorPos(out var p))throw new InvalidOperationException("无法读取鼠标坐标");return new(p.X,p.Y);}
 public void Move(int x,int y)=>Native.Move(x,y);
 public void Button(MouseButton b,bool down){sendButton(b==MouseButton.Left?(down?2u:4u):(down?8u:16u));if(down)held.Add(b);else held.Remove(b);}
 public void Wheel(int delta)=>Native.Send(0x800,data:delta);
 public void ReleaseAll(){Exception? failure=null;foreach(var button in held.ToArray()){try{sendButton(button==MouseButton.Left?4u:16u);held.Remove(button);}catch(Exception ex){failure=ex;}}if(failure!=null)throw failure;}
}
