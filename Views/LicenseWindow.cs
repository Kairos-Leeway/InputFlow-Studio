using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace InputFlowStudio;

public sealed class LicenseWindow : Window {
 public static string ReadAgreement(){using var stream=typeof(LicenseWindow).Assembly.GetManifestResourceStream("InputFlowStudio.EULA.txt")??throw new InvalidOperationException("许可协议资源缺失");using var reader=new StreamReader(stream);return reader.ReadToEnd();}
 public LicenseWindow(bool light){
  Title="用户协议与风险声明 · InputFlow Studio";Width=780;Height=760;MinWidth=580;MinHeight=450;WindowStartupLocation=WindowStartupLocation.CenterOwner;
  Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/InputFlowStudio;component/Themes/Studio.xaml",UriKind.Relative)});ThemeService.Apply(Resources,light);SetResourceReference(BackgroundProperty,"Canvas");SetResourceReference(ForegroundProperty,"Ink");FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI");
  var root=new DockPanel{Margin=new Thickness(24)};
  var title=new TextBlock{Text="用户协议与风险声明",FontSize=23,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)};DockPanel.SetDock(title,Dock.Top);root.Children.Add(title);
  var close=new Button{Content="关闭",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0),MinWidth=90};close.Click+=(_,_)=>Close();DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
  var text=new TextBox{Text=ReadAgreement(),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(16),FontSize=13};root.Children.Add(text);Content=root;
 }
}
