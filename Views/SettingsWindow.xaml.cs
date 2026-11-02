using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace InputFlowStudio;
public partial class SettingsWindow : Window {
 readonly HashSet<DependencyObject> invalidInputs=new();
 AppSettings draft;
 readonly Action<AppSettings,string?> apply;
 readonly Func<Task<string>>? checkUpdates;
 public SettingsWindow(AppSettings settings,Plan? plan,Action<AppSettings,string?> apply,Func<Task<string>>? checkUpdates=null){InitializeComponent();AddHandler(Validation.ErrorEvent,new EventHandler<ValidationErrorEventArgs>((_,e)=>{if(e.OriginalSource is DependencyObject input){if(e.Action==ValidationErrorEventAction.Added)invalidInputs.Add(input);else invalidInputs.Remove(input);}}));ThemeService.Apply(Resources,settings.Theme=="浅色");this.apply=apply;this.checkUpdates=checkUpdates;CheckUpdateButton.IsEnabled=checkUpdates!=null;draft=settings.Copy();DataContext=draft;ThemeChoice.ItemsSource=new[]{"浅色","深色"};Corner.ItemsSource=new[]{"右上角","左上角","右下角","左下角"};PlanName.Text=plan?.Name??"尚未选择方案";PlanShortcut.IsEnabled=plan!=null;PlanShortcut.Text=plan?.Shortcut??(plan?.HotKey>0?KeyInterop.KeyFromVirtualKey(plan.HotKey).ToString():"");}
 async void CheckUpdate(object sender,RoutedEventArgs e){if(checkUpdates==null)return;CheckUpdateButton.IsEnabled=false;UpdateStatusText.Text="正在检查 GitHub Releases…";try{UpdateStatusText.Text=await checkUpdates();}catch(Exception ex){UpdateStatusText.Text=ex.Message;}finally{CheckUpdateButton.IsEnabled=true;}}
 void OpenRelease(object sender,RoutedEventArgs e){try{UpdateService.OpenReleasePage();}catch(Exception ex){UpdateStatusText.Text=ex.Message;}}
 void OpenAgreement(object sender,RoutedEventArgs e){try{new LicenseWindow(draft.Theme=="浅色"){Owner=this}.ShowDialog();}catch(Exception ex){UpdateStatusText.Text=ex.Message;}}
 void Capture(object sender,KeyEventArgs e){if(e.Key==Key.Tab)return;e.Handled=true;var box=(TextBox)sender;Key key=e.Key==Key.System?e.SystemKey:e.Key;if(key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)return;
 string text=key==Key.Back&&Equals(box.Tag,"Optional")?"":Shortcut.Capture(key,Keyboard.Modifiers);box.SetCurrentValue(TextBox.TextProperty,text);box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();ErrorText.Text="";
 }
 void Defaults(object s,RoutedEventArgs e){invalidInputs.Clear();draft=new();DataContext=draft;PlanShortcut.Text="";ErrorText.Text="";}
 void Cancel(object s,RoutedEventArgs e)=>DialogResult=false;
 bool HasErrors(DependencyObject parent){if(Validation.GetHasError(parent))return true;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)if(HasErrors(System.Windows.Media.VisualTreeHelper.GetChild(parent,i)))return true;return false;}
 void Apply(object s,RoutedEventArgs e){try{if(invalidInputs.Count>0||HasErrors(this))throw new ArgumentException("请修正数字输入，确保格式正确");draft.Validate();apply(draft.Copy(),PlanShortcut.IsEnabled?PlanShortcut.Text:null);DialogResult=true;}catch(Exception ex){ErrorText.Text=ex.Message;}}
}
