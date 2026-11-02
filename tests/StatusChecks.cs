using System.Windows;
using System.Windows.Controls;
using InputFlowStudio;

static partial class Checks {
 static async Task PlaybackStatusChecks(){
  var settings=new AppSettings{RunCountdownSeconds=2,ShowRunningHints=false,ShowRecordingHints=false};
  settings.Validate();var copy=settings.Copy();Assert(copy.RunCountdownSeconds==2&&!copy.ShowRunningHints&&copy.ShowRecordingIndicator,"New settings lost on serialization");
  settings.RunCountdownSeconds=11;try{settings.Validate();throw new Exception("Accepted invalid playback countdown");}catch(ArgumentException){}settings.RunCountdownSeconds=2;
  var sink=new FakeMouse();var engine=new Engine(sink,new FakeRecorder());var vm=new MainViewModel(engine,false);vm.Selected!.Steps.Add(new(){Kind=ActionKind.Wait,Duration=100,Interval=0});vm.Save();
  var ticks=new List<int>();
  var prep=new RecordingPreparation(async(span,token)=>{ticks.Add(ticks.Count+1);await Task.Delay(10000,token);});
  var window=new MainWindow(vm,prep,false,settings,new FakeDialogs());PrepareLayout(window);
  Button(window,"RunButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  Assert(vm.IsRunCountingDown&&vm.CanRun&&!vm.CanRecord&&!vm.CanEdit&&!vm.CanChangeSpeed&&!vm.CanPause,"Run preparation interlocks failed");
  Assert(!engine.Running&&ticks.Count==1,"Playback began before countdown finished");
  Button(window,"RunButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  for(int i=0;i<100&&vm.IsRunCountingDown;i++)await Task.Delay(5);
  Assert(!vm.IsRunCountingDown&&vm.CanEdit&&!engine.Running&&sink.Releases==0,"Canceled countdown injected input or left editing locked");
  var order=new List<int>();Assert(await new RecordingPreparation((span,token)=>Task.CompletedTask).Run(order.Add,3)&&order.SequenceEqual(new[]{3,2,1}),"Countdown did not descend");
  var plan=WaitPlan(25);plan.Loops=3;plan.LoopDelay=0;await engine.Run(plan);Assert(engine.Progress==new PlaybackProgress(3,3,3),"Finite loop completion count wrong");
  plan.Loops=0;plan.Steps[0].Duration=80;var task=engine.Run(plan);
  while(engine.Progress.CompletedLoops<1)await Task.Delay(5);
  engine.Paused=true;var paused=engine.Progress;await Task.Delay(120);Assert(engine.Progress==paused,"Loop counter advanced while paused");engine.Stop();await task;
  Assert(engine.Progress.TotalLoops==0&&engine.Progress.CompletedLoops>=1&&engine.Progress.CompletedLoops<=engine.Progress.CurrentLoop,"Infinite loop count lost on stop");
  plan.Loops=1;plan.Steps[0].Duration=500;task=engine.Run(plan);await Task.Delay(20);engine.Stop();await task;Assert(engine.Progress.CompletedLoops==0,"Partial loop was counted complete");
  var overlay=new RecordingOverlay(copy);overlay.ShowRecording();var texts=Descendants((FrameworkElement)overlay.Content).OfType<TextBlock>().Select(x=>x.Text).ToArray();Assert(texts.Any(x=>x.Contains("正在录制"))&&!texts.Any(x=>x.Contains(copy.Record)),"Recording indicator depends on hotkey hints");
  overlay.ShowRunning(EngineState.Paused,new(4,3,0));texts=Descendants((FrameworkElement)overlay.Content).OfType<TextBlock>().Select(x=>x.Text).ToArray();Assert(texts.Any(x=>x.Contains("已暂停"))&&texts.Any(x=>x.Contains("已完成 3")&&x.Contains("无限"))&&!texts.Any(x=>x.Contains(copy.Pause)),"Paused overlay/count/hint toggle wrong");overlay.Close();
  Console.WriteLine("Playback countdown cancellation/interlocks, settings persistence, finite/infinite/partial/paused loop counters, independent recording/running hints and indicators: PASS");
 }
}
