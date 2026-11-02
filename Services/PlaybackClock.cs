using System.Diagnostics;
using System.Runtime.InteropServices;
namespace InputFlowStudio;
/// <summary>A continuously integrated, pauseable virtual clock. Deadlines never restart after each event.</summary>
public sealed class PlaybackClock : IDisposable {
 readonly object gate=new();
 readonly AutoResetEvent changed=new(false);
 readonly Stopwatch real=Stopwatch.StartNew();
 double position,anchor,speed;
 bool paused;
 readonly bool timerActive;
 [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
 [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
 public PlaybackClock(double speed){this.speed=speed;timerActive=timeBeginPeriod(1)==0;}
 void Integrate(){double now=real.Elapsed.TotalMilliseconds;if(!paused)position+=(now-anchor)*speed;anchor=now;}
 public double Position {get {lock(gate){Integrate();return position;}}}
 public void SetSpeed(double value){if(!double.IsFinite(value)||value<.1||value>10)throw new ArgumentOutOfRangeException(nameof(value));lock(gate){Integrate();speed=value;}changed.Set();}
 public void SetPaused(bool value){lock(gate){Integrate();paused=value;}changed.Set();}
 public void WaitUntil(double deadline,CancellationToken token){
  var handles=new WaitHandle[]{token.WaitHandle,changed};
  while(true){
   token.ThrowIfCancellationRequested();double remaining;bool hold;
   lock(gate){Integrate();hold=paused;remaining=(deadline-position)/speed;}
   if(!hold&&remaining<=0)return;
   if(hold){WaitHandle.WaitAny(handles);continue;}
   if(remaining>1.5){WaitHandle.WaitAny(handles,(int)Math.Min(20,Math.Max(1,Math.Floor(remaining-0.5))));}
   else {Thread.SpinWait(100);}
  }
 }
 public void Dispose(){if(timerActive)timeEndPeriod(1);changed.Dispose();}
}
