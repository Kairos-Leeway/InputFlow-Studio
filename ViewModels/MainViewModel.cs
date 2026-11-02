using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Data;
using System.Globalization;
namespace InputFlowStudio;
public class MainViewModel : INotifyPropertyChanged {
 public event PropertyChangedEventHandler? PropertyChanged;
 public ObservableCollection<Plan> Plans {get;private set;}=new();
 readonly ConcurrentQueue<Step> pending=new();
 readonly DispatcherTimer captureTimer;
 readonly IPlanStore store;
 public PlanEditTracker Edits {get;}=new();
 public bool NeedsRecordingChoice=>Selected!=null&&(Selected.Steps.Count>0||Edits.SavedStepCount(Selected)>0);
 public bool HasUnsaved=>Selected?.IsDirty==true;
 public bool AnyUnsaved=>Plans.Any(p=>p.IsDirty);
 public bool CanEditStep=>CanEdit&&SelectedStep!=null;
 public bool CanRestoreStep=>CanEditStep&&SelectedStep!.IsDirty;
 public bool CanRestorePlan=>CanEdit&&HasUnsaved;
 public bool CanUndo=>CanEdit&&Selected!=null&&Edits.CanUndo(Selected);
 public string SaveState=>HasUnsaved?"● 有未保存修改":"✓ 已保存";
 Plan? selected;
 public Plan? Selected {get=>selected;set{
  if(!CanEdit)return;
  if(selected!=null){selected.Steps.CollectionChanged-=StepsChanged;foreach(var item in selected.Steps)item.PropertyChanged-=StepChanged;}
  selected=value;
  if(selected!=null){selected.Steps.CollectionChanged+=StepsChanged;for(int i=0;i<selected.Steps.Count;i++){selected.Steps[i].Number=i+1;selected.Steps[i].PropertyChanged+=StepChanged;}StepsView=CollectionViewSource.GetDefaultView(selected.Steps);StepsView.Filter=Match;}else StepsView=null;
  Notify(nameof(StepsView));Notify(nameof(StepSummary));SelectedStep=null;
  Notify(nameof(Selected));Notify(nameof(PlaybackSpeed));NotifyState();
 }}
 public Plan? RecordingTarget {get;set;}
 public Engine Engine {get;}
 bool countingDown;
 public bool IsCountingDown {get=>countingDown;set{countingDown=value;NotifyState();}}
 public AppSettings Settings {get;private set;}=new();
 public void ApplySettings(AppSettings settings){Settings=settings;NotifyState();}
 public bool CanEdit=>Engine.State==EngineState.Idle&&!IsCountingDown;
 public bool CanRecord=>Selected!=null&&(IsCountingDown||Engine.State is EngineState.Idle or EngineState.Recording);
 public bool CanRun=>!IsCountingDown&&Selected!=null&&(Engine.State is EngineState.Running or EngineState.Paused||(CanEdit&&Selected.Steps.Count>0));
 public bool CanPause=>Engine.State is EngineState.Running or EngineState.Paused;
 public bool CanChangeSpeed=>!IsCountingDown&&Selected!=null&&Engine.State is not (EngineState.Recording or EngineState.Stopping);
 public string RecordText=>IsCountingDown?$"取消准备   {Settings.Record}":Engine.Recording?$"■  停止录制   {Settings.Record}":$"●  录制   {Settings.Record}";
 public string RunText=>Engine.State==EngineState.Stopping?"正在停止…":Engine.Running?$"■  停止方案   {Settings.Run}":$"▶  运行方案   {Settings.Run}";
 public string PauseText=>Engine.Paused?$"▶  恢复   {Settings.Pause}":$"Ⅱ  暂停   {Settings.Pause}";
 public string AddText=>$"＋  添加步骤   {Settings.Add}";
 public string ToolbarHint=>$"{Settings.Stop} 紧急停止 · {Settings.Pick} 拾取坐标";
 public string StopHint=>$"{Settings.Stop} 随时停止并释放按键";
 public string EmptyHint=>Selected?.Steps.Count>0?"没有匹配的步骤，请切换全部步骤或清空搜索":$"添加步骤，或按 {Settings.Record} 开始录制";
 public string StateText=>IsCountingDown?$"正在准备录制 · {Settings.Stop} 取消":Engine.State switch {EngineState.Recording=>$"录制中 · {Settings.Record} 完成，{Settings.Stop} 紧急停止",EngineState.Running=>$"运行中 · {Settings.Run} 停止，{Settings.Pause} 暂停",EngineState.Paused=>$"已暂停 · {Settings.Pause} 恢复，{Settings.Run} 停止",EngineState.Stopping=>"正在停止并释放鼠标按键…",_=>"就绪"};
 public ICollectionView? StepsView {get;private set;}
 Step? selectedStep;
 public Step? SelectedStep {get=>selectedStep;set{selectedStep=value;Notify(nameof(SelectedStep));Notify(nameof(CanUndo));Notify(nameof(CanEditStep));Notify(nameof(CanRestoreStep));}}
 public string[] FilterOptions {get;}={"全部步骤","关键操作（隐藏移动）","鼠标点击 / 按下释放","等待 / 滚轮","仅键盘"};
 int filterIndex=1;string search="";
 public int FilterIndex {get=>filterIndex;set{filterIndex=value;Notify(nameof(FilterIndex));RefreshView();}}
 public string Search {get=>search;set{search=value??"";Notify(nameof(Search));RefreshView();}}
 public string StepSummary=>$"显示 {(StepsView as CollectionView)?.Count??0} / 共 {Selected?.Steps.Count??0} 步";
 bool Match(object item){if(item is not Step step)return false;bool match=filterIndex switch {1=>step.Kind!=ActionKind.Move,2=>step.Kind is ActionKind.LeftClick or ActionKind.RightClick or ActionKind.Down or ActionKind.Up or ActionKind.RightDown or ActionKind.RightUp or ActionKind.DoubleClick,3=>step.Kind is ActionKind.Wait or ActionKind.Wheel,4=>step.IsKeyboard,_=>true};
 if(!match)return false;if(string.IsNullOrWhiteSpace(search))return true;
 string label=new ActionLabelConverter().Convert(step.Kind,typeof(string),null!,CultureInfo.CurrentCulture).ToString()!;
 return $"#{step.Number} {label} {step.Kind} {(step.IsKeyboard?step.KeyDisplay:"")} {step.X} {step.Y}".Contains(search.Trim(),StringComparison.OrdinalIgnoreCase);
 }
 void RefreshView(){StepsView?.Refresh();Notify(nameof(StepSummary));Notify(nameof(EmptyHint));}
 void StepChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName is nameof(Step.Kind) or nameof(Step.X) or nameof(Step.Y) or nameof(Step.KeyCode) or nameof(Step.KeyModifiers))RefreshView();}
 public Step? NavigateImportant(int direction){if(Selected==null)return null;int start=SelectedStep==null?(direction>0?-1:Selected.Steps.Count):Selected.Steps.IndexOf(SelectedStep);for(int i=start+direction;i>=0&&i<Selected.Steps.Count;i+=direction){var step=Selected.Steps[i];if(step.Kind!=ActionKind.Move&&StepsView?.Contains(step)==true){SelectedStep=step;return step;}}return null;}
 public double PlaybackSpeed {get=>Selected?.Speed??1;set{
  if(!CanChangeSpeed||Selected==null||!double.IsFinite(value))return;
  var speed=Math.Round(Math.Clamp(value,.1,10),1);
  Selected.Speed=speed;Engine.SetSpeed(speed);Notify(nameof(PlaybackSpeed));
 }}
 public static string Folder=>System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"MouseFlowStudio");
 public MainViewModel():this(new Engine(),true){}
 public MainViewModel(Engine engine,bool load,IPlanStore? store=null){
  Engine=engine;Edits.ValidateName=(plan,name)=>NameError(name,plan);this.store=store??(load?new JsonPlanStore(Folder):new MemoryPlanStore());
  if(load)Plans=this.store.Load();
  PlanNames.Normalize(Plans);
  if(Plans.Count==0)Plans.Add(new());foreach(var plan in Plans)Edits.Attach(plan);Edits.Changed+=plan=>{Notify(nameof(HasUnsaved));Notify(nameof(AnyUnsaved));Notify(nameof(CanUndo));Notify(nameof(SaveState));if(plan==Selected)Notify(nameof(PlaybackSpeed));};Selected=Plans[0];Plans.CollectionChanged+=(_,e)=>{if(e.NewItems!=null)foreach(Plan plan in e.NewItems)Edits.Attach(plan);if(e.OldItems!=null)foreach(Plan plan in e.OldItems)Edits.Detach(plan);};
  Engine.Captured+=step=>pending.Enqueue(step);
  captureTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
  captureTimer.Tick+=(_,_)=>FlushCapture();
  Engine.StateChanged+=()=>OnUI(()=>{
   if(Engine.Recording)captureTimer.Start();else{captureTimer.Stop();FlushCapture();}
   NotifyState();
  });
 }
 public void FlushCapture(){while(pending.TryDequeue(out var step))RecordingTarget?.Steps.Add(step);}
 void StepsChanged(object? sender,NotifyCollectionChangedEventArgs e){
  if(e.OldItems!=null)foreach(Step step in e.OldItems)step.PropertyChanged-=StepChanged;
  if(e.NewItems!=null)foreach(Step step in e.NewItems){step.PropertyChanged-=StepChanged;step.PropertyChanged+=StepChanged;}
  if(Selected!=null){int start=e.Action==NotifyCollectionChangedAction.Add?Math.Max(0,e.NewStartingIndex):0;for(int i=start;i<Selected.Steps.Count;i++)Selected.Steps[i].Number=i+1;}
  Notify(nameof(StepSummary));Notify(nameof(EmptyHint));NotifyState();
 }
 void OnUI(Action action){if(Application.Current is {} app&&!app.Dispatcher.CheckAccess())app.Dispatcher.BeginInvoke(action);else action();}
 void Notify(string name)=>PropertyChanged?.Invoke(this,new(name));
 void NotifyState(){foreach(var name in new[]{nameof(CanEdit),nameof(CanRecord),nameof(CanRun),nameof(CanPause),nameof(CanChangeSpeed),nameof(RecordText),nameof(RunText),nameof(PauseText),nameof(StateText),nameof(AddText),nameof(ToolbarHint),nameof(StopHint),nameof(EmptyHint),nameof(HasUnsaved),nameof(AnyUnsaved),nameof(CanUndo),nameof(SaveState),nameof(CanEditStep),nameof(CanRestoreStep),nameof(CanRestorePlan)})Notify(name);}
 IEnumerable<Plan> SavedVersions()=>Plans.Select(p=>Edits.SavedCopy(p));
 IEnumerable<Plan> WithReplacement(Plan original,Plan replacement)=>Plans.Select(p=>p==original?replacement:Edits.SavedCopy(p));
 public string UniqueName(string name)=>PlanNames.Unique(Plans.Concat(Plans.Select(p=>new Plan{Name=Edits.SavedName(p)})),name);
 public void Save(){FlushCapture();if(Selected==null)return;PlanNames.ValidateAll(Plans);var current=Selected;var next=WithReplacement(current,Clone(current)).ToArray();PlanNames.ValidateAll(next);store.Save(next);Edits.AcceptSaved(current);}
 public void SaveAll(){FlushCapture();PlanNames.ValidateAll(Plans);store.Save(Plans);foreach(var plan in Plans)Edits.AcceptSaved(plan);}
 public async Task<bool> RunSelectedAsync(Action? saved=null,Func<bool>? confirmSave=null,Action? starting=null){if(!CanRun||Engine.Running)return false;Engine.ValidatePlan(Selected!);if(HasUnsaved){if(confirmSave==null||!confirmSave())return false;Save();saved?.Invoke();}starting?.Invoke();await Engine.Run(Selected!);return true;}
 public string? NameError(string value,Plan? except=null){try{PlanNames.Validate(Plans,value,except);PlanNames.Validate(Plans.Where(p=>p!=except).Select(p=>new Plan{Name=Edits.SavedName(p)}),value);return null;}catch(ArgumentException ex){return ex.Message;}}
 public Plan CreateNewPlan(string suggestion="新方案"){var plan=Settings.CreatePlan();plan.Name=UniqueName(suggestion);AddPlan(plan);return plan;}
 public void AddPlan(Plan plan){plan.Name=UniqueName(plan.Name);store.Save(SavedVersions().Append(plan));Plans.Add(plan);Edits.Attach(plan);Selected=plan;}
 public void SaveReplacing(Plan original,Plan replacement)=>store.Save(WithReplacement(original,replacement));
 public void RenamePlan(Plan plan,string name){var error=NameError(name,plan);if(error!=null)throw new ArgumentException(error);plan.Name=name.Trim();}
 public void RemovePlan(Plan plan){store.Save(Plans.Where(p=>p!=plan).Select(p=>Edits.SavedCopy(p)));Plans.Remove(plan);if(Selected==plan)Selected=Plans.FirstOrDefault();Edits.Detach(plan);}
 public static Plan Clone(Plan plan)=>JsonSerializer.Deserialize<Plan>(JsonSerializer.Serialize(plan))!;
 public void SaveShortcut(Plan plan,string? shortcut){var snapshot=Edits.SavedCopy(plan);snapshot.Shortcut=shortcut;store.Save(WithReplacement(plan,snapshot));Edits.WithoutHistory(plan,()=>plan.Shortcut=shortcut);Edits.AcceptSavedProperty(plan,nameof(Plan.Shortcut));}
 public RecordingDraft BeginRecordingDraft(RecordingDecision decision){
  if(!CanEdit||Selected==null)throw new InvalidOperationException("当前无法准备录制");if(decision.Destination==RecordingDestination.Cancel)throw new InvalidOperationException("已取消录制");
  string requested=decision.Name??Selected.Name;var issue=NameError(requested,decision.Destination==RecordingDestination.Overwrite?Selected:null);if(issue!=null)throw new ArgumentException(issue);
  var draft=decision.Destination==RecordingDestination.Overwrite?Clone(Selected):Settings.CreatePlan();draft.Steps.Clear();draft.Name=requested.Trim();
  if(decision.Destination==RecordingDestination.NewPlan){draft.Shortcut="";draft.HotKey=0;}
  RecordingTarget=draft;return new(Selected,draft,decision.Destination);
 }
 public bool CommitRecording(RecordingDraft session){
  FlushCapture();if(session.Draft.Steps.Count==0){RecordingTarget=null;return false;}
  var issue=NameError(session.Draft.Name,session.Destination==RecordingDestination.Overwrite?session.Original:null);if(issue!=null)throw new ArgumentException(issue);
  if(session.Destination==RecordingDestination.Overwrite){
   var replacement=Clone(session.Original);replacement.Name=session.Draft.Name;replacement.Steps=session.Draft.Steps;store.Save(WithReplacement(session.Original,replacement));
   Edits.WithoutHistory(session.Original,()=>{session.Original.Name=replacement.Name;session.Original.Steps.Clear();foreach(var step in session.Draft.Steps)session.Original.Steps.Add(step);});Edits.AcceptSaved(session.Original);Selected=session.Original;
  }else{store.Save(SavedVersions().Append(session.Draft));Plans.Add(session.Draft);Edits.Attach(session.Draft);Selected=session.Draft;}
  RecordingTarget=null;return true;
 }
 public void UndoEdit(){if(!CanUndo||Selected==null)return;var id=SelectedStep?.Id;Edits.Undo(Selected);SelectedStep=Selected.Steps.FirstOrDefault(s=>s.Id==id);}
 public void RestoreCurrentStep(){if(!CanEdit||Selected==null||SelectedStep==null)return;var step=SelectedStep;Edits.RestoreStep(Selected,step);if(!Selected.Steps.Contains(step))SelectedStep=null;}
 public void RestoreCurrentPlan(){if(!CanEdit||Selected==null)return;var error=NameError(Edits.SavedCopy(Selected).Name,Selected);if(error!=null)throw new ArgumentException(error);Edits.RestorePlan(Selected);SelectedStep=null;}
 public void RestoreField(string scope,string property){if(!CanEdit||Selected==null)return;if(scope=="Step"&&property=="Keyboard"){Edits.Edit(Selected,()=>{foreach(string key in new[]{nameof(Step.KeyCode),nameof(Step.KeyModifiers),nameof(Step.ScanCode),nameof(Step.Extended)})Edits.RestoreField(Selected,SelectedStep,key);});}else Edits.RestoreField(Selected,scope=="Step"?SelectedStep:null,property);}
 public void SetKeyboardKey(int code,int modifiers){if(!CanEdit||Selected==null||SelectedStep==null)return;var step=SelectedStep;Edits.Edit(Selected,()=>{step.KeyCode=code;step.KeyModifiers=modifiers;step.ScanCode=0;step.Extended=false;});}
 public void PickCoordinates(int x,int y){if(!CanEdit||Selected==null||SelectedStep==null)return;var step=SelectedStep;Edits.Edit(Selected,()=>{step.X=x;step.Y=y;});}
}
