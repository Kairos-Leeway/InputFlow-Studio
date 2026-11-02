namespace InputFlowStudio;
public interface IKeyboardOutput {void Key(int key,int scan,bool extended,bool down);void ReleaseAll();}
public sealed class NativeKeyboardOutput : IKeyboardOutput {
 readonly Action<int,int,bool,bool> send;
 readonly Dictionary<(int Code,bool Extended,bool Scan),(int Key,int Scan,bool Extended)> held=new();
 public NativeKeyboardOutput(Action<int,int,bool,bool>? send=null){this.send=send??Native.SendKey;}
 public void Key(int key,int scan,bool extended,bool down){uint mapped=key==19?0u:scan>0?(uint)scan:Native.MapVirtualKey((uint)key,4);int actualScan=(int)(mapped&0xFF);bool actualExtended=extended||(mapped&0xFF00)==0xE000;var identity=(actualScan>0?actualScan:key,actualExtended,actualScan>0);if(down)held[identity]=(key,actualScan,actualExtended);send(key,actualScan,actualExtended,down);if(!down)held.Remove(identity);}
 public void ReleaseAll(){Exception? failure=null;foreach(var pair in held.ToArray()){var key=pair.Value;try{send(key.Key,key.Scan,key.Extended,false);held.Remove(pair.Key);}catch(Exception ex){failure=ex;}}if(failure!=null)throw failure;}
}
