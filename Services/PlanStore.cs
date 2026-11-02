using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
namespace InputFlowStudio;
public interface IPlanStore {ObservableCollection<Plan> Load();void Save(IEnumerable<Plan> plans);}
public sealed class JsonPlanStore(string folder) : IPlanStore {
 string FilePath=>Path.Combine(folder,"plans.json");
 public ObservableCollection<Plan> Load(){Directory.CreateDirectory(folder);if(!File.Exists(FilePath))return new();try{return JsonSerializer.Deserialize<ObservableCollection<Plan>>(File.ReadAllText(FilePath))??new();}catch{File.Copy(FilePath,FilePath+".broken-"+DateTime.Now.Ticks);return new();}}
 public void Save(IEnumerable<Plan> plans){Directory.CreateDirectory(folder);var json=JsonSerializer.Serialize(plans,new JsonSerializerOptions{WriteIndented=true});File.WriteAllText(FilePath+".tmp",json);File.Move(FilePath+".tmp",FilePath,true);}
}
public sealed class MemoryPlanStore : IPlanStore {
 public string Json {get;private set;}="[]";
 public ObservableCollection<Plan> Load()=>JsonSerializer.Deserialize<ObservableCollection<Plan>>(Json)!;
 public void Save(IEnumerable<Plan> plans)=>Json=JsonSerializer.Serialize(plans);
}
public static class PlanNames {
 public static string Validate(IEnumerable<Plan> plans,string? name,Plan? except=null){name=name?.Trim();if(string.IsNullOrEmpty(name))throw new ArgumentException("方案名称不能为空");if(name.Length>100)throw new ArgumentException("方案名称不能超过 100 个字符");if(plans.Any(p=>p!=except&&string.Equals(p.Name.Trim(),name,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("方案名称已存在，请使用其他名称");return name;}
 public static string Unique(IEnumerable<Plan> plans,string? suggestion){string root=string.IsNullOrWhiteSpace(suggestion)?"新方案":suggestion.Trim();if(root.Length>90)root=root[..90];var used=new HashSet<string>(plans.Select(p=>p.Name.Trim()),StringComparer.OrdinalIgnoreCase);string name=root;for(int i=2;used.Contains(name);i++)name=$"{root} ({i})";return name;}
 public static void Normalize(IEnumerable<Plan> plans){var seen=new List<Plan>();foreach(var p in plans){p.Name=Unique(seen,p.Name);seen.Add(p);}}
 public static void ValidateAll(IEnumerable<Plan> plans){var seen=new List<Plan>();foreach(var p in plans){Validate(seen,p.Name);seen.Add(p);}}
}
public enum RecordingDestination { Cancel, Overwrite, NewPlan }
public sealed record RecordingDecision(RecordingDestination Destination,string? Name=null);
public sealed record RecordingDraft(Plan Original,Plan Draft,RecordingDestination Destination);
