using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
namespace InputFlowStudio;
/// <summary>Tracks working objects against saved snapshots; undo stores inverse edits rather than full-plan snapshots.</summary>
public sealed class PlanEditTracker {
 sealed record UndoItem(Action Apply,string? RestoreName=null);
 public Func<Plan,string,string?>? ValidateName {get;set;}
 sealed class Entry {public required Plan Plan;public required Plan Saved;public Dictionary<Guid,Step> SavedSteps=new();public List<UndoItem> Undo=new();public Dictionary<object,Dictionary<string,object?>> Values=new(ReferenceEqualityComparer.Instance);public List<Step> Known=new();public int Batch;public List<UndoItem> Group=new();public bool Replay;public PropertyChangedEventHandler? Properties;public NotifyCollectionChangedEventHandler? Collection;}
 readonly Dictionary<Plan,Entry> entries=new();
 static readonly string[] PlanFields={nameof(Plan.Name),nameof(Plan.Loops),nameof(Plan.LoopDelay),nameof(Plan.Speed),nameof(Plan.Shortcut),nameof(Plan.HotKey)};
 static readonly string[] StepFields={nameof(Step.Kind),nameof(Step.X),nameof(Step.Y),nameof(Step.Duration),nameof(Step.Interval),nameof(Step.Wheel),nameof(Step.KeyCode),nameof(Step.ScanCode),nameof(Step.Extended),nameof(Step.KeyModifiers)};
 public event Action<Plan>? Changed;
 public void Attach(Plan plan){if(entries.ContainsKey(plan))return;var seen=new HashSet<Guid>();foreach(var step in plan.Steps)if(step.Id==Guid.Empty||!seen.Add(step.Id)){step.Id=Guid.NewGuid();seen.Add(step.Id);}var e=new Entry{Plan=plan,Saved=MainViewModel.Clone(plan)};e.SavedSteps=e.Saved.Steps.ToDictionary(s=>s.Id);entries.Add(plan,e);e.Properties=(sender,args)=>Property(e,sender!,args.PropertyName);e.Collection=(_,args)=>Collection(e,args);Observe(e,plan,PlanFields);plan.PropertyChanged+=e.Properties;foreach(var step in plan.Steps)AttachStep(e,step);plan.Steps.CollectionChanged+=e.Collection;Refresh(e);}
 public void Detach(Plan plan){if(!entries.Remove(plan,out var e))return;plan.PropertyChanged-=e.Properties;plan.Steps.CollectionChanged-=e.Collection;foreach(var step in e.Known)step.PropertyChanged-=e.Properties;}
 static void Observe(Entry e,object model,string[] fields)=>e.Values[model]=fields.ToDictionary(name=>name,name=>model.GetType().GetProperty(name)!.GetValue(model));
 void AttachStep(Entry e,Step step){Observe(e,step,StepFields);step.PropertyChanged+=e.Properties;e.Known.Add(step);step.SetSaved(e.SavedSteps.GetValueOrDefault(step.Id));}
 void DetachStep(Entry e,Step step){step.PropertyChanged-=e.Properties;e.Values.Remove(step);e.Known.Remove(step);}
 void Property(Entry e,object sender,string? name){if(name==null||!e.Values.TryGetValue(sender,out var values)||!values.TryGetValue(name,out var before))return;var prop=sender.GetType().GetProperty(name)!;var after=prop.GetValue(sender);values[name]=after;if(Equals(before,after))return;Record(e,()=>prop.SetValue(sender,before),sender is Plan&&name==nameof(Plan.Name)?(string?)before:null);Update(e);}
 void Collection(Entry e,NotifyCollectionChangedEventArgs args){
  var plan=e.Plan;
  switch(args.Action){
   case NotifyCollectionChangedAction.Add:var added=args.NewItems!.Cast<Step>().ToArray();for(int i=0;i<added.Length;i++){AttachStep(e,added[i]);e.Known.Remove(added[i]);e.Known.Insert(args.NewStartingIndex+i,added[i]);}Record(e,()=>{foreach(var item in added)plan.Steps.Remove(item);});break;
   case NotifyCollectionChangedAction.Remove:var removed=args.OldItems!.Cast<Step>().ToArray();int oldIndex=args.OldStartingIndex;foreach(var item in removed)DetachStep(e,item);Record(e,()=>{for(int i=0;i<removed.Length;i++)plan.Steps.Insert(Math.Min(oldIndex+i,plan.Steps.Count),removed[i]);});break;
   case NotifyCollectionChangedAction.Move:int from=args.OldStartingIndex,to=args.NewStartingIndex;var moving=e.Known[from];e.Known.RemoveAt(from);e.Known.Insert(to,moving);Record(e,()=>plan.Steps.Move(to,from));break;
   case NotifyCollectionChangedAction.Replace:var old=(Step)args.OldItems![0]!;var replacement=(Step)args.NewItems![0]!;int index=args.NewStartingIndex;DetachStep(e,old);AttachStep(e,replacement);e.Known.Remove(replacement);e.Known.Insert(index,replacement);Record(e,()=>plan.Steps[index]=old);break;
   case NotifyCollectionChangedAction.Reset:var previous=e.Known.ToArray();foreach(var item in previous)DetachStep(e,item);foreach(var item in plan.Steps)AttachStep(e,item);Record(e,()=>{plan.Steps.Clear();foreach(var item in previous)plan.Steps.Add(item);});break;
  }
  Update(e);
 }
 void Record(Entry e,Action undo,string? restoreName=null){if(e.Replay)return;if(e.Batch>0)e.Group.Add(new(undo,restoreName));else{e.Undo.Add(new(undo,restoreName));if(e.Undo.Count>100)e.Undo.RemoveAt(0);}}
 void Update(Entry e){if(e.Batch==0)Refresh(e);}
 void Refresh(Entry e){e.Plan.SetSaved(e.Saved);Changed?.Invoke(e.Plan);}
 public void Edit(Plan plan,Action action){var e=entries[plan];e.Batch++;try{action();}finally{e.Batch--;if(e.Batch==0){var group=e.Group.ToArray();e.Group.Clear();if(!e.Replay&&group.Length>0){e.Undo.Add(new(()=>{for(int i=group.Length-1;i>=0;i--)group[i].Apply();},group.FirstOrDefault(item=>item.RestoreName!=null)?.RestoreName));if(e.Undo.Count>100)e.Undo.RemoveAt(0);}Refresh(e);}}}
 public void WithoutHistory(Plan plan,Action action){var e=entries[plan];bool previous=e.Replay;e.Replay=true;try{Edit(plan,action);}finally{e.Replay=previous;}}
 public int SavedStepCount(Plan plan)=>entries[plan].Saved.Steps.Count;
 public string SavedName(Plan plan)=>entries[plan].Saved.Name;
 public Plan SavedCopy(Plan plan)=>MainViewModel.Clone(entries[plan].Saved);
 public bool CanUndo(Plan plan)=>entries.TryGetValue(plan,out var e)&&e.Undo.Count>0;
 public void Undo(Plan plan){var e=entries[plan];if(e.Undo.Count==0)return;var undo=e.Undo[^1];if(undo.RestoreName is {} name&&ValidateName?.Invoke(plan,name) is {} issue)throw new InvalidOperationException(issue);e.Undo.RemoveAt(e.Undo.Count-1);WithoutHistory(plan,undo.Apply);}
 public void AcceptSaved(Plan plan){var e=entries[plan];e.Saved=MainViewModel.Clone(plan);e.SavedSteps=e.Saved.Steps.ToDictionary(s=>s.Id);foreach(var step in plan.Steps)step.SetSaved(e.SavedSteps.GetValueOrDefault(step.Id));e.Undo.Clear();Refresh(e);}
 public void AcceptSavedProperty(Plan plan,string name){var e=entries[plan];typeof(Plan).GetProperty(name)!.SetValue(e.Saved,typeof(Plan).GetProperty(name)!.GetValue(plan));Refresh(e);}
 public void RestoreField(Plan plan,Step? step,string name){var e=entries[plan];object target=step is null?plan:step;object? baseline=step is null?e.Saved:e.Saved.Steps.FirstOrDefault(s=>s.Id==step.Id);if(baseline==null)throw new InvalidOperationException("新增步骤没有已保存值，可使用“还原当前步骤”移除");var property=target.GetType().GetProperty(name)??throw new ArgumentException("未知参数");Edit(plan,()=>property.SetValue(target,property.GetValue(baseline)));}
 static void CopyFields(object source,object target,string[] names){foreach(var name in names){var property=source.GetType().GetProperty(name)!;property.SetValue(target,property.GetValue(source));}}
 public void RestoreStep(Plan plan,Step step){var e=entries[plan];var original=e.Saved.Steps.FirstOrDefault(s=>s.Id==step.Id);Edit(plan,()=>{if(original==null)plan.Steps.Remove(step);else CopyFields(original,step,StepFields);});}
 public void RestorePlan(Plan plan){var e=entries[plan];Edit(plan,()=>{CopyFields(e.Saved,plan,PlanFields);plan.Steps.Clear();foreach(var step in MainViewModel.Clone(e.Saved).Steps)plan.Steps.Add(step);});}
}
