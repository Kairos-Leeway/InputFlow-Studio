namespace InputFlowStudio;
public enum EngineState { Idle, Recording, Running, Paused, Stopping }
public sealed record PlaybackProgress(long CurrentLoop,long CompletedLoops,int TotalLoops);
public sealed class Engine : IDisposable {
 PlaybackProgress progress=new(0,0,0);
 public PlaybackProgress Progress=>Volatile.Read(ref progress);
 readonly IMouseOutput mouse;
 readonly IMouseRecorder recorder;
 readonly IKeyboardOutput keyboard;
 CancellationTokenSource? cancel;
 PlaybackClock? clock;
 Task? playback;
 public Engine(IMouseOutput? mouse=null,IMouseRecorder? recorder=null,IKeyboardOutput? keyboard=null){this.keyboard=keyboard??new NativeKeyboardOutput();this.mouse=mouse??new NativeMouseOutput();this.recorder=recorder??new MouseRecorder();}
 public EngineState State {get;private set;}
 public bool Recording=>State==EngineState.Recording;
 public bool Running=>State is EngineState.Running or EngineState.Paused or EngineState.Stopping;
 public bool Paused {get=>State==EngineState.Paused;set{if(State is not (EngineState.Running or EngineState.Paused))return;clock?.SetPaused(value);SetState(value?EngineState.Paused:EngineState.Running);}}
 public event Action<Step>? Captured;
 public event Action? StateChanged;
 void SetState(EngineState state){State=state;StateChanged?.Invoke();}
 public void Record(IntPtr excludedWindow=default,bool recordMovement=true,bool recordKeyboard=true,bool includeControlHotkeys=false,IReadOnlyList<Shortcut>? shortcuts=null,Shortcut? emergencyStop=null){if(State!=EngineState.Idle)return;if(recorder is MouseRecorder nativeRecorder){nativeRecorder.RecordMovement=recordMovement;nativeRecorder.RecordKeyboard=recordKeyboard;nativeRecorder.IncludeControlHotkeys=includeControlHotkeys;nativeRecorder.ControlHotkeys=shortcuts??Array.Empty<Shortcut>();nativeRecorder.EmergencyStop=emergencyStop;}recorder.Start(s=>Captured?.Invoke(s),excludedWindow);SetState(EngineState.Recording);}
 public void SetSpeed(double speed)=>clock?.SetSpeed(speed);
 public void Stop(){
  if(Recording){recorder.Stop();try{ReleaseAll();}finally{SetState(EngineState.Idle);}return;}
  if(Running){SetState(EngineState.Stopping);cancel?.Cancel();}
 }
 public static void ValidatePlan(Plan plan){
  if(plan.Steps.Count==0)throw new InvalidOperationException("请先添加步骤");
  if(plan.Loops<0||plan.LoopDelay<0||!double.IsFinite(plan.Speed)||plan.Speed<.1||plan.Speed>10)throw new InvalidOperationException("次数和间隔不能为负，倍速范围为 0.1–10");
  if(plan.Steps.Any(s=>s.IsKeyboard&&(s.KeyCode<1||s.KeyCode>254||s.ScanCode<0||s.ScanCode>255||s.KeyModifiers<0||s.KeyModifiers>15)))throw new InvalidOperationException("键盘按键或修饰键参数无效");
  if(plan.Steps.Any(s=>s.Duration<0||s.Interval<0||!Enum.IsDefined(s.Kind)))throw new InvalidOperationException("步骤耗时和间隔不能为负，操作类型必须有效");
 }
 public async Task Run(Plan plan){
  if(State!=EngineState.Idle)return;
  ValidatePlan(plan);
  var steps=plan.Steps.Select(s=>new Step{Kind=s.Kind,X=s.X,Y=s.Y,Duration=s.Duration,Interval=s.Interval,Wheel=s.Wheel,KeyCode=s.KeyCode,ScanCode=s.ScanCode,Extended=s.Extended,KeyModifiers=s.KeyModifiers}).ToArray();
  if(steps.Any(s=>s.Duration<0||s.Interval<0||!Enum.IsDefined(s.Kind)))throw new InvalidOperationException("步骤耗时和间隔不能为负，操作类型必须有效");
  int loops=plan.Loops,loopDelay=plan.LoopDelay;
  Volatile.Write(ref progress,new(1,0,loops));
  cancel=new();clock=new(plan.Speed);SetState(EngineState.Running);
  var token=cancel.Token;var timeline=clock;
  try{
   playback=Task.Factory.StartNew(()=>Play(steps,loops,loopDelay,timeline,token),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
   await playback;
  }catch(OperationCanceledException) when(token.IsCancellationRequested){}
  finally{playback=null;clock=null;timeline.Dispose();cancel.Dispose();cancel=null;SetState(EngineState.Idle);}
 }
 void Play(Step[] steps,int loops,int loopDelay,PlaybackClock timeline,CancellationToken token){
  double due=0;
  void Wait(double ms){due+=ms;timeline.WaitUntil(due,token);}
  void Check()=>timeline.WaitUntil(due,token);
  try{
   for(long loop=0;loops==0||loop<loops;loop++){
    token.ThrowIfCancellationRequested();
    Volatile.Write(ref progress,new(loop+1,loop,loops));
    foreach(var step in steps){
     Wait(step.Interval);
     if(step.IsKeyboard){
      var modifiers=KeyCodes.ModifierKeys((uint)step.KeyModifiers);
      if(step.Kind!=ActionKind.KeyUp){foreach(int key in modifiers){Check();keyboard.Key(key,0,false,true);}Check();keyboard.Key(step.KeyCode,step.ScanCode,step.Extended,true);}
      if(step.Kind==ActionKind.KeyPress)Wait(step.Duration);
      if(step.Kind!=ActionKind.KeyDown){Check();keyboard.Key(step.KeyCode,step.ScanCode,step.Extended,false);foreach(int key in modifiers.Reverse()){Check();keyboard.Key(key,0,false,false);}}
      continue;
     }
     if(step.Kind==ActionKind.Wait){Wait(step.Duration);continue;}
     if(step.Kind==ActionKind.Move&&step.Duration>0){
      var from=mouse.Position();double began=due;int ticks=Math.Max(1,(int)Math.Ceiling(step.Duration/8.0));
      for(int t=1;t<=ticks;t++){due=began+(double)step.Duration*t/ticks;Check();mouse.Move((int)(from.X+((double)step.X-from.X)*t/ticks),(int)(from.Y+((double)step.Y-from.Y)*t/ticks));}
      continue;
     }
     mouse.Move(step.X,step.Y);Check();
     switch(step.Kind){
      case ActionKind.Down:mouse.Button(MouseButton.Left,true);break;
      case ActionKind.Up:mouse.Button(MouseButton.Left,false);break;
      case ActionKind.RightDown:mouse.Button(MouseButton.Right,true);break;
      case ActionKind.RightUp:mouse.Button(MouseButton.Right,false);break;
      case ActionKind.Wheel:mouse.Wheel(step.Wheel);break;
      case ActionKind.LeftClick:case ActionKind.RightClick:case ActionKind.DoubleClick:
       int count=step.Kind==ActionKind.DoubleClick?2:1;var button=step.Kind==ActionKind.RightClick?MouseButton.Right:MouseButton.Left;
       for(int c=0;c<count;c++){Check();mouse.Button(button,true);Wait(step.Duration);mouse.Button(button,false);if(c+1<count)Wait(60);}
       break;
     }
    }
    Volatile.Write(ref progress,new(loop+1,loop+1,loops));
    if(loops==0||loop+1<loops){Wait(loopDelay);if(loopDelay==0)token.WaitHandle.WaitOne(1);}
   }
  }finally{ReleaseAll();}
 }
 void ReleaseAll(){try{mouse.ReleaseAll();}finally{keyboard.ReleaseAll();}}
 public void Dispose(){Stop();try{playback?.GetAwaiter().GetResult();}catch(OperationCanceledException){}catch(Exception ex){System.Diagnostics.Trace.TraceError("Playback shutdown: "+ex);}finally{recorder.Stop();}}
}
