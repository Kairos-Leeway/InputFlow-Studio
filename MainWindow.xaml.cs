using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Text.Json;
using Microsoft.Win32;
namespace InputFlowStudio;
public partial class MainWindow : Window {
 readonly MainViewModel vm;
 readonly RecordingPreparation preparation;
 readonly bool showRecordingUi;
 readonly IPlanDialogs dialogs;
 readonly System.Windows.Threading.DispatcherTimer noticeTimer=new(){Interval=TimeSpan.FromSeconds(6)};
 RecordingDraft? recordingSession;
 bool recordingStarted,modalOpen;
 TrayService? tray;
 HotkeyOriginGate? hotkeyGate;
 int pendingModifier;
 IntPtr handle;
 HotkeyManager? hotkeys;
 RecordingOverlay? overlay;
 bool closing,recordingUiActive;
 WindowState restoreState;
 public MainWindow():this(new MainViewModel(),null,true,AppSettings.Load()){}
 public MainWindow(MainViewModel model,RecordingPreparation? preparation=null,bool showRecordingUi=true,AppSettings? settings=null,IPlanDialogs? dialogs=null){
  vm=model;this.preparation=preparation??new();this.showRecordingUi=showRecordingUi;this.dialogs=dialogs??new PlanDialogs(this,()=>vm.Settings.Theme=="浅色");
  InitializeComponent();vm.ApplySettings(settings??new());SetTheme(vm.Settings.Theme=="浅色",false);DataContext=vm;Kinds.ItemsSource=Enum.GetValues<ActionKind>();
  vm.Engine.StateChanged+=()=>{if(Dispatcher.CheckAccess())EngineChanged();else Dispatcher.InvokeAsync(EngineChanged);};
  noticeTimer.Tick+=(_,_)=>{Notice.Visibility=Visibility.Collapsed;noticeTimer.Stop();};
  SourceInitialized+=(_,_)=>{if(showRecordingUi)tray=new TrayService(()=>Dispatcher.InvokeAsync(ShowMain),()=>Dispatcher.InvokeAsync(Close));handle=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(handle).AddHook(Hook);hotkeys=new(new NativeHotkeyRegistry(handle));Bind();try{hotkeyGate=new HotkeyOriginGate(()=>hotkeys.Active,emergencyStop:()=>Dispatcher.InvokeAsync(()=>Stop(this,new RoutedEventArgs())));}catch(Exception ex){ShowNotice(ex.Message,true);}};
  Closing+=ClosingWindow;
 }
 void ClosingWindow(object? sender,System.ComponentModel.CancelEventArgs e){closing=true;preparation.Cancel();try{InputValidation.Commit(this);vm.Engine.Dispose();PersistRecording();if(vm.AnyUnsaved){var choice=Ask(()=>dialogs.ResolveUnsaved("退出",vm.Plans.Count(p=>p.IsDirty),true));if(choice==UnsavedDecision.Cancel){e.Cancel=true;closing=false;ShowMain();return;}if(choice==UnsavedDecision.Save)vm.SaveAll();}overlay?.Close();overlay=null;hotkeyGate?.Dispose();hotkeys?.Dispose();tray?.Dispose();noticeTimer.Stop();}catch(Exception ex){e.Cancel=true;closing=false;ShowMain();ShowNotice("退出前保存失败，应用已保留打开："+ex.Message,true);}}
 void ShowMain(){if(closing)return;if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;if(!IsVisible)Show();Activate();}
 void ShowNotice(string text,bool error=false){Status.Text=text;NoticeText.Text=text;Notice.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty,error?"RecordInk":"Accent");Notice.Visibility=Visibility.Visible;noticeTimer.Stop();noticeTimer.Start();}
 void DismissNotice(object s,RoutedEventArgs e){Notice.Visibility=Visibility.Collapsed;noticeTimer.Stop();}
 T Ask<T>(Func<T> action){hotkeys?.Suspend();modalOpen=true;try{return action();}finally{modalOpen=false;try{hotkeys?.Resume();}catch(Exception ex){ShowNotice(ex.Message,true);}}}
 void EngineChanged(){Status.Text=vm.StateText;if(vm.Engine.State==EngineState.Idle&&recordingStarted){recordingStarted=false;try{PersistRecording();RestoreRecordingUi(false);}catch(Exception ex){RestoreRecordingUi(true);ShowNotice("录制已保留在内存，保存失败，请点击保存重试："+ex.Message,true);}}}
 void PersistRecording(){if(recordingSession==null)return;bool saved=vm.CommitRecording(recordingSession);string name=recordingSession.Destination==RecordingDestination.NewPlan?recordingSession.Draft.Name:recordingSession.Original.Name;recordingSession=null;ShowNotice(saved?$"录制完成，已保存“{name}”":"没有录到键鼠操作，已保留原方案");}

 void Bind(){try{hotkeys?.Apply(vm.Settings,vm.Plans);}catch(Exception ex){Status.Text=ex.Message;}}
 IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool done){if(msg!=0x312)return IntPtr.Zero;done=true;if(hotkeyGate?.ConsumeInjected(w.ToInt32())==true)return IntPtr.Zero;if(modalOpen)return IntPtr.Zero;switch(w.ToInt32()){
  case 1:Record(this,new());break;case 2:Run(this,new());break;case 3:Stop(this,new());break;case 4:Pause(this,new());break;case 5:Pick();break;case 6:Add(this,new());break;
  default:if(vm.CanEdit&&hotkeys?.FindPlan(w.ToInt32()) is {} plan){vm.Selected=plan;Run(this,new());}break;
 }return IntPtr.Zero;}
 void Pick(){if(!vm.CanEdit||Steps.SelectedItem is not Step step)return;if(step.IsKeyboard){ShowNotice("键盘步骤无需坐标，请使用按键输入框");return;}Native.GetCursorPos(out var point);vm.PickCoordinates(point.X,point.Y);ShowNotice("坐标已暂存到编辑稿，橙色边框表示未保存；可单项还原或撤回");}
 async void Record(object sender,RoutedEventArgs e){
  if(preparation.IsPending){preparation.Cancel();return;}
  if(!vm.CanRecord)return;
  if(vm.Engine.Recording){Stop(sender,e);return;}
  bool started=false;RecordingDraft? staged=null;
  try{
   InputValidation.Commit(this);PersistRecording();
   var decision=vm.NeedsRecordingChoice?Ask(()=>dialogs.ChooseRecording(vm.Selected!,vm.UniqueName(vm.Selected!.Name+" 录制"),(value,mode)=>vm.NameError(value,mode==RecordingDestination.Overwrite?vm.Selected:null))):new RecordingDecision(RecordingDestination.Overwrite,vm.Selected!.Name);
   if(decision.Destination==RecordingDestination.Cancel)return;
   staged=vm.BeginRecordingDraft(decision);recordingSession=staged;vm.IsCountingDown=true;Status.Text=vm.StateText;
   if(showRecordingUi){recordingUiActive=true;restoreState=WindowState;if(vm.Settings.HideMainWindow)Hide();if(vm.Settings.CountdownSeconds>0||vm.Settings.ShowRecordingHints){overlay=new(vm.Settings);if(vm.Settings.CountdownSeconds>0)overlay.ShowCountdown(vm.Settings.CountdownSeconds);else overlay.ShowRecording();overlay.Show();}}
   bool ready=await preparation.Run(seconds=>overlay?.ShowCountdown(seconds),vm.Settings.CountdownSeconds);
   if(!ready||closing)return;
   vm.Engine.Record(handle,vm.Settings.RecordMovement,vm.Settings.RecordKeyboard,vm.Settings.IncludeControlHotkeys,HotkeyManager.Build(vm.Settings,vm.Plans).Select(binding=>binding.Shortcut).ToArray(),Shortcut.Parse(vm.Settings.Stop));recordingStarted=true;started=true;if(vm.Settings.ShowRecordingHints)overlay?.ShowRecording();else{overlay?.Close();overlay=null;}
  }catch(Exception ex){ShowNotice("录制未开始："+ex.Message,true);}
  finally{vm.IsCountingDown=false;if(!started){if(recordingSession==staged){recordingSession=null;vm.RecordingTarget=null;}RestoreRecordingUi(true);if(!closing&&Status.Text.StartsWith("正在准备"))ShowNotice("已取消录制准备，原方案保持不变");}else Status.Text=vm.StateText;}
 }
 void RestoreRecordingUi(bool force){overlay?.Close();overlay=null;if(recordingUiActive&&!closing&&(force||vm.Settings.ShowMainAfterRecording)){WindowState=restoreState==WindowState.Minimized?WindowState.Normal:restoreState;ShowMain();}recordingUiActive=false;}
 async void Run(object s,RoutedEventArgs e){
  if(!vm.CanRun)return;if(vm.Engine.Running){Stop(s,e);return;}bool hiddenForRun=false;var previousState=WindowState;
  try{InputValidation.Commit(this);PersistRecording();if(showRecordingUi&&vm.Selected!.Steps.Any(step=>step.IsKeyboard)&&hotkeyGate==null)throw new InvalidOperationException("键盘回放保护未就绪，请重新启动应用");bool ran=await vm.RunSelectedAsync(()=>ShowNotice($"已保存“{vm.Selected!.Name}”，开始运行"),()=>Ask(()=>dialogs.ResolveUnsaved("运行",1,false))==UnsavedDecision.Save,()=>{if(showRecordingUi&&vm.Settings.HideWhenRunning){hiddenForRun=true;Hide();}});Status.Text=ran?"方案已结束":"已取消运行，修改仍保留在编辑稿";}
  catch(Exception ex){ShowNotice("未能运行："+ex.Message,true);}
  finally{if(hiddenForRun&&!closing){WindowState=previousState==WindowState.Minimized?WindowState.Normal:previousState;ShowMain();}}
 }
 void Stop(object s,RoutedEventArgs e){if(preparation.IsPending){preparation.Cancel();return;}try{vm.Engine.Stop();if(vm.Engine.Running)Status.Text=vm.StateText;else if(recordingSession==null&&Status.Text=="就绪")Status.Text="已停止";}catch(Exception ex){ShowNotice(ex.Message,true);}}
 void Pause(object s,RoutedEventArgs e){if(!vm.CanPause)return;vm.Engine.Paused=!vm.Engine.Paused;Status.Text=vm.StateText;}
 void Save(object s,RoutedEventArgs e){if(!vm.CanEdit)return;try{InputValidation.Commit(this);PersistRecording();vm.Save();ShowNotice($"已保存“{vm.Selected?.Name??"方案库"}”及当前参数");}catch(Exception ex){ShowNotice("保存失败："+ex.Message,true);}}
 void New(object s,RoutedEventArgs e){if(!vm.CanEdit)return;try{InputValidation.Commit(this);PersistRecording();var plan=vm.CreateNewPlan();ShowNotice($"已新建“{plan.Name}”");}catch(Exception ex){ShowNotice(ex.Message,true);}}
 void Copy(object s,RoutedEventArgs e){if(!vm.CanEdit||vm.Selected is not {} plan)return;try{InputValidation.Commit(this);var copy=MainViewModel.Clone(plan);copy.Name+=" 副本";copy.HotKey=0;copy.Shortcut="";vm.AddPlan(copy);ShowNotice($"已复制为“{copy.Name}”");}catch(Exception ex){ShowNotice(ex.Message,true);}}
 void Rename(object s,RoutedEventArgs e){if(!vm.CanEdit||vm.Selected is not {} plan)return;try{var name=Ask(()=>dialogs.Rename(plan.Name,value=>vm.NameError(value,plan)));if(name==null)return;vm.RenamePlan(plan,name);ShowNotice($"名称已暂存为“{plan.Name}”，保存后生效");}catch(Exception ex){ShowNotice(ex.Message,true);}}
 void DeletePlan(object s,RoutedEventArgs e){if(!vm.CanEdit||vm.Selected is not {} plan)return;if(!Ask(()=>dialogs.ConfirmDelete("删除方案",$"确认删除“{plan.Name}”及其 {plan.Steps.Count} 个步骤？此操作无法撤销。")))return;try{vm.RemovePlan(plan);Bind();ShowNotice($"已删除方案“{plan.Name}”");}catch(Exception ex){ShowNotice("删除未完成："+ex.Message,true);}}
 void Add(object s,RoutedEventArgs e){if(!vm.CanEdit||vm.Selected is not {} p)return;Native.GetCursorPos(out var point);var step=new Step{Kind=ActionKind.LeftClick,X=point.X,Y=point.Y,Duration=80};int index=vm.SelectedStep==null?p.Steps.Count:p.Steps.IndexOf(vm.SelectedStep)+1;p.Steps.Insert(index,step);vm.Search="";if(vm.FilterIndex==3)vm.FilterIndex=1;vm.SelectedStep=step;Steps.ScrollIntoView(step);Status.Text=$"已添加第 {step.Number} 步，可在右侧修改操作类型";}
 void DeleteStep(object s,RoutedEventArgs e){if(!vm.CanEdit||vm.Selected is not {} plan||Steps.SelectedItem is not Step step)return;if(!Ask(()=>dialogs.ConfirmDelete("删除步骤",$"确认从编辑稿删除第 {step.Number} 步？保存前可撤回此修改。")))return;plan.Steps.Remove(step);ShowNotice("步骤已从编辑稿删除，可撤回修改或还原整个方案");}
 void UndoEdit(object s,RoutedEventArgs e){if(!vm.CanEdit)return;try{vm.UndoEdit();ShowNotice("已撤回上一项修改");}catch(Exception ex){ShowNotice("无法撤回："+ex.Message,true);}}
 void RestoreStep(object s,RoutedEventArgs e){try{vm.RestoreCurrentStep();ShowNotice("当前步骤已还原到最近保存的参数");}catch(Exception ex){ShowNotice(ex.Message,true);}}
 void RestorePlan(object s,RoutedEventArgs e){if(!vm.CanEdit||vm.Selected==null)return;if(!Ask(()=>dialogs.ConfirmDelete("还原整个方案","确认将当前方案的名称、参数、步骤和顺序还原到最近保存的版本？本次还原仍可撤回。")))return;try{vm.RestoreCurrentPlan();ShowNotice("整个方案已还原到最近保存的版本");}catch(Exception ex){ShowNotice(ex.Message,true);}}
 void RestoreField(object s,RoutedEventArgs e){try{if(s is System.Windows.Controls.Button button&&button.Tag is string tag){var parts=tag.Split('.');vm.RestoreField(parts[0],parts[1]);ShowNotice("参数已还原到最近保存的值");}}catch(Exception ex){ShowNotice(ex.Message,true);}}
 void Up(object s,RoutedEventArgs e)=>Move(-1);void Down(object s,RoutedEventArgs e)=>Move(1);
 void Move(int direction){if(!vm.CanEdit||vm.Selected is not {} p||Steps.SelectedItem is not Step step)return;int i=p.Steps.IndexOf(step);if(i+direction<0||i+direction>=p.Steps.Count)return;p.Steps.Move(i,i+direction);vm.SelectedStep=step;Steps.ScrollIntoView(step);Status.Text="已按原始步骤顺序移动（包括隐藏的移动步骤）";}
 void CaptureKeyboardStep(object sender,System.Windows.Input.KeyEventArgs e){e.Handled=true;var key=e.Key==System.Windows.Input.Key.System?e.SystemKey:e.Key;int code=System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);if(code<=0)return;if(KeyCodes.IsModifier(code)){pendingModifier=code;return;}pendingModifier=0;vm.SetKeyboardKey(code,(int)System.Windows.Input.Keyboard.Modifiers);}
 void CaptureKeyboardRelease(object s,System.Windows.Input.KeyEventArgs e){int code=System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key==System.Windows.Input.Key.System?e.SystemKey:e.Key);if(code==pendingModifier&&code>0){pendingModifier=0;vm.SetKeyboardKey(code,0);}e.Handled=true;}
 void BeginKeyCapture(object s,System.Windows.Input.KeyboardFocusChangedEventArgs e){pendingModifier=0;hotkeys?.Suspend();Status.Text="按下目标键或组合键；点击其他位置结束按键选择";}
 void EndKeyCapture(object s,System.Windows.Input.KeyboardFocusChangedEventArgs e){try{hotkeys?.Resume();}catch(Exception ex){ShowNotice(ex.Message,true);}}
 void PreviousImportant(object s,RoutedEventArgs e)=>Navigate(-1);void NextImportant(object s,RoutedEventArgs e)=>Navigate(1);
 void Navigate(int direction){var step=vm.NavigateImportant(direction);if(step!=null)Steps.ScrollIntoView(step);else Status.Text=direction>0?"已到最后一个匹配的关键操作":"已到第一个匹配的关键操作";}
 void Settings(object s,RoutedEventArgs e){if(!vm.CanEdit)return;hotkeys?.Suspend();try{
  var dialog=new SettingsWindow(vm.Settings,vm.Selected,(candidate,planShortcut)=>{
   var old=vm.Settings;var plan=vm.Selected;var oldShortcut=plan?.Shortcut;
   try{if(plan!=null)vm.Edits.WithoutHistory(plan,()=>plan.Shortcut=planShortcut);HotkeyManager.Build(candidate,vm.Plans);hotkeys?.Apply(candidate,vm.Plans);candidate.Save();if(plan!=null)vm.SaveShortcut(plan,planShortcut);vm.ApplySettings(candidate);SetTheme(candidate.Theme=="浅色",false);}
   catch{if(plan!=null)vm.Edits.WithoutHistory(plan,()=>plan.Shortcut=oldShortcut);try{hotkeys?.Apply(old,vm.Plans);old.Save();}finally{hotkeys?.Suspend();}throw;}
  }){Owner=this};dialog.ShowDialog();
 }catch(Exception ex){Status.Text=ex.Message;}finally{try{hotkeys?.Resume();}catch(Exception ex){Status.Text=ex.Message;}}}
 void SetTheme(bool light,bool save=true){ThemeService.Apply(Resources,light);}
 void Import(object s,RoutedEventArgs e){if(!vm.CanEdit)return;var dialog=new OpenFileDialog{Filter="JSON|*.json"};if(dialog.ShowDialog()!=true)return;try{var plan=JsonSerializer.Deserialize<Plan>(System.IO.File.ReadAllText(dialog.FileName))??throw new Exception("无效方案");plan.HotKey=0;plan.Shortcut="";vm.AddPlan(plan);ShowNotice($"已导入“{plan.Name}”");}catch(Exception ex){Status.Text=ex.Message;}}
 void Export(object s,RoutedEventArgs e){var dialog=new SaveFileDialog{Filter="JSON|*.json",FileName="方案.json"};if(vm.Selected!=null&&dialog.ShowDialog()==true)try{System.IO.File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(vm.Selected,new JsonSerializerOptions{WriteIndented=true}));}catch(Exception ex){Status.Text=ex.Message;}}
}
