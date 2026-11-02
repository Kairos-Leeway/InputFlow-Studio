using System.Windows;
namespace InputFlowStudio;
public sealed class TrayService : IDisposable {
 readonly System.Windows.Forms.NotifyIcon tray;
 readonly System.Drawing.Icon icon;
 public TrayService(Action show,Action exit){
  using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/InputFlowStudio;component/Assets/App.ico")).Stream;
  icon=new System.Drawing.Icon(stream);
  var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add("打开主界面",null,(_,_)=>show());menu.Items.Add("退出 InputFlow Studio",null,(_,_)=>exit());
  tray=new System.Windows.Forms.NotifyIcon{Icon=icon,Text="InputFlow Studio · 双击打开主界面",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=(_,_)=>show();
 }
 public void Dispose(){tray.Visible=false;tray.ContextMenuStrip?.Dispose();tray.Dispose();icon.Dispose();}
}
