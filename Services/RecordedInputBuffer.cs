namespace InputFlowStudio;
public static class KeyCodes {
 public static bool IsModifier(int key)=>key is 16 or 17 or 18 or 91 or 92 or 160 or 161 or 162 or 163 or 164 or 165;
 public static uint Modifiers(IEnumerable<int> keys){uint mods=0;foreach(var key in keys)mods|=key switch{17 or 162 or 163=>2u,18 or 164 or 165=>1u,16 or 160 or 161=>4u,91 or 92=>8u,_=>0u};return mods;}
 public static bool Matches(Shortcut shortcut,IEnumerable<int> keys){var held=keys.ToHashSet();return held.Contains((int)shortcut.Key)&&Modifiers(held)==shortcut.Modifiers;}
 public static int[] ModifierKeys(uint mask)=>new[]{(2u,17),(1u,18),(4u,16),(8u,91)}.Where(pair=>(mask&pair.Item1)!=0).Select(pair=>pair.Item2).ToArray();
}
/// <summary>Filters complete control chords before publishing steps; timestamps remain shared by mouse and keyboard.</summary>
public sealed class RecordedInputBuffer {
 sealed class Frame(Step step,uint time){public Step Step=step;public uint Time=time;public bool Excluded;}
 sealed class Held(int key,int scan,bool extended){public int Key=key,Scan=scan;public bool Extended=extended,Suppressed,Used;public List<int> Downs=new();}
 readonly List<Frame> frames=new();readonly Dictionary<int,Held> held=new();readonly Shortcut[] shortcuts;readonly bool includeHotkeys;readonly Shortcut? emergencyStop;readonly uint start;bool mouseHeld;
 public RecordedInputBuffer(uint start,bool includeHotkeys,IEnumerable<Shortcut> shortcuts,IEnumerable<int>? initiallyHeld=null,Shortcut? emergencyStop=null){this.emergencyStop=emergencyStop;this.start=start;this.includeHotkeys=includeHotkeys;this.shortcuts=shortcuts.ToArray();foreach(int key in initiallyHeld??Array.Empty<int>())held[key]=new(key,0,false){Suppressed=true};}
 int Add(Step step,uint time){frames.Add(new(step,time));return frames.Count-1;}
 static Step KeyStep(Held key,bool down)=>new(){Kind=down?ActionKind.KeyDown:ActionKind.KeyUp,KeyCode=key.Key,ScanCode=key.Scan,Extended=key.Extended,Duration=0,Interval=0};
 void UseModifiers(uint time){foreach(var key in held.Values.Where(h=>KeyCodes.IsModifier(h.Key))){if(key.Suppressed||key.Downs.Count==0){key.Suppressed=false;key.Downs.Add(Add(KeyStep(key,true),time));}key.Used=true;}}
 void Suppress(Held key){foreach(int index in key.Downs)frames[index].Excluded=true;key.Suppressed=true;}
 public void Keyboard(int key,int scan,bool extended,bool down,uint time){
  if(!down){if(!held.Remove(key,out var released))return;if(!released.Suppressed)Add(KeyStep(released,false),time);return;}
  if(!held.TryGetValue(key,out var current)){current=new(key,scan,extended);held.Add(key,current);}
  if(!includeHotkeys){var shortcut=shortcuts.FirstOrDefault(s=>(s.Key==key||KeyCodes.IsModifier(key))&&KeyCodes.Matches(s,held.Keys));if(emergencyStop is {} stop&&key==stop.Key&&(KeyCodes.Modifiers(held.Keys)&stop.Modifiers)==stop.Modifiers)shortcut=stop;if(shortcut.Key!=0){if(held.TryGetValue((int)shortcut.Key,out var trigger))Suppress(trigger);foreach(var modifier in held.Values.Where(h=>KeyCodes.IsModifier(h.Key)&&!h.Used))Suppress(modifier);return;}}
  if(current.Suppressed&&!KeyCodes.IsModifier(key))return;
  if(!KeyCodes.IsModifier(key))UseModifiers(time);
  current.Suppressed=false;current.Downs.Add(Add(KeyStep(current,true),time));
 }
 public void Mouse(Step step,uint time){if(step.Kind!=ActionKind.Move||mouseHeld)UseModifiers(time);Add(step,time);if(step.Kind is ActionKind.Down or ActionKind.RightDown)mouseHeld=true;if(step.Kind is ActionKind.Up or ActionKind.RightUp)mouseHeld=false;}
 public IReadOnlyList<Step> Finish(uint time){foreach(var key in held.Values.Reverse())if(!key.Suppressed&&key.Downs.Count>0)Add(KeyStep(key,false),time);held.Clear();var result=new List<Step>();uint last=start;foreach(var frame in frames.Where(f=>!f.Excluded)){frame.Step.Interval=(int)Math.Min(int.MaxValue,(long)unchecked(frame.Time-last));last=frame.Time;result.Add(frame.Step);}return result;}
}
