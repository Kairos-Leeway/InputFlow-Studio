using System.IO;
using System.Windows;
using System.Windows.Media;
namespace InputFlowStudio;
public static class ThemeService {
 static string FilePath=>Path.Combine(MainViewModel.Folder,"theme.txt");
 public static bool Load(){try{return !File.Exists(FilePath)||File.ReadAllText(FilePath).Trim()!="Dark";}catch{return true;}}
 public static void Save(bool light){Directory.CreateDirectory(MainViewModel.Folder);File.WriteAllText(FilePath,light?"Light":"Dark");}
 public static void Apply(ResourceDictionary resources,bool light){
 var colors=light?new[]{"#F2F5FB","#FFFFFF","#F6F8FC","#D8E1EE","#192C49","#596D89","#216CC4","#BA3456"}:new[]{"#0D1220","#151D30","#0E1627","#2B3854","#E8EEFF","#9CACC9","#76DDF5","#FF9DAD"};
 resources["DirtyInk"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#C77710":"#E9A44A"));
 var keys=new[]{"Canvas","Panel","Field","Line","Ink","Muted","Accent","RecordInk"};
 for(int i=0;i<keys.Length;i++){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));brush.Freeze();resources[keys[i]]=brush;}
 }
}
