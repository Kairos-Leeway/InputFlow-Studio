using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
namespace InputFlowStudio;
public sealed class RecordingOverlay : Window {
 readonly TextBlock heading=new(){FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White};
 readonly TextBlock count=new(){FontSize=62,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center};
 readonly TextBlock hint=new(){FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(194,211,239)),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};
 readonly AppSettings settings;readonly Native.Rect area;bool countdown=true;
 public RecordingOverlay(AppSettings settings){
  this.settings=settings;Opacity=settings.OverlayOpacity;Native.GetCursorPos(out var cursor);var info=new Native.MonitorInfo{Size=Marshal.SizeOf<Native.MonitorInfo>()};Native.GetMonitorInfo(Native.MonitorFromPoint(cursor,2),ref info);area=info.Work;
  WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowActivated=false;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;SizeToContent=SizeToContent.Height;Width=400;
  var stack=new StackPanel();stack.Children.Add(heading);stack.Children.Add(count);stack.Children.Add(hint);
  Content=new Border{Background=new SolidColorBrush(Color.FromArgb(245,20,31,51)),BorderBrush=new SolidColorBrush(Color.FromRgb(99,154,225)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(22),Child=stack};
  SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;var style=Native.GetWindowLongPtr(h,-20).ToInt64();Native.SetWindowLongPtr(h,-20,new IntPtr(style|0x20|0x80|0x08000000));};
  Loaded+=(_,_)=>Place();SizeChanged+=(_,_)=>Place();
 }
 public void ShowCountdown(int seconds,bool running=false){countdown=true;heading.Text=running?"准备运行，请切换到目标窗口":"准备录制，请将鼠标移至目标位置";count.Visibility=Visibility.Visible;count.Text=seconds.ToString();hint.Text=running?(settings.ShowRunningHints?$"{settings.Run} / {settings.Stop} 取消运行准备":""):settings.ShowRecordingHints?$"{settings.Record} / {settings.Stop} 取消录制准备":"";Place();}
 public void ShowRecording(){countdown=false;heading.Foreground=settings.ShowRecordingIndicator?Brushes.Salmon:Brushes.White;heading.Text=settings.ShowRecordingIndicator?"●  正在录制键鼠操作":"录制快捷键";count.Visibility=Visibility.Collapsed;hint.Text=settings.ShowRecordingHints?$"{settings.Record} 停止录制   ·   {settings.Stop} 紧急停止\n{settings.Run} 运行方案（结束录制后可用）\n{settings.Pause} 暂停 / 恢复（回放时可用）":"";Place();}
 public void ShowRunning(EngineState state,PlaybackProgress progress){
  countdown=false;count.Visibility=Visibility.Collapsed;
  heading.Text=settings.ShowRunningIndicator?(state==EngineState.Paused?"Ⅱ  已暂停":state==EngineState.Stopping?"■  正在停止":"●  正在运行方案"):(settings.ShowRunningHints?"运行快捷键":"循环进度");
  heading.Foreground=!settings.ShowRunningIndicator?Brushes.White:state==EngineState.Paused?Brushes.Gold:state==EngineState.Stopping?Brushes.Salmon:Brushes.MediumSpringGreen;
  string loops=settings.ShowLoopCounter?$"第 {progress.CurrentLoop} 轮 / {(progress.TotalLoops==0?"无限":progress.TotalLoops.ToString())}  ·  已完成 {progress.CompletedLoops} 轮":"";
  hint.Text=loops+(settings.ShowRunningHints?(loops.Length>0?"\n":"")+$"{settings.Run} 停止方案   ·   {settings.Stop} 紧急停止\n{settings.Pause} 暂停 / 恢复":"");Place();
 }
 void Place(){if(PresentationSource.FromVisual(this) is not HwndSource source||source.CompositionTarget==null)return;var matrix=source.CompositionTarget.TransformFromDevice;var topLeft=matrix.Transform(new Point(area.Left,area.Top));var bottomRight=matrix.Transform(new Point(area.Right,area.Bottom));double height=ActualHeight>0?ActualHeight:200;
  if(countdown){Left=(topLeft.X+bottomRight.X-Width)/2;Top=(topLeft.Y+bottomRight.Y-height)/2;}
  else{Left=settings.OverlayCorner.Contains("左")?topLeft.X+18:bottomRight.X-Width-18;Top=settings.OverlayCorner.Contains("下")?bottomRight.Y-height-18:topLeft.Y+18;}
 }
}
