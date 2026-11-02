using System.Globalization;
using System.Windows.Data;
namespace InputFlowStudio;
public sealed class ActionLabelConverter : IValueConverter {
 public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is ActionKind k ? k switch { ActionKind.Move=>"移动鼠标",ActionKind.LeftClick=>"左键单击",ActionKind.RightClick=>"右键单击",ActionKind.Down=>"左键按下",ActionKind.Up=>"左键释放",ActionKind.DoubleClick=>"双击",ActionKind.Wheel=>"滚动滚轮",ActionKind.Wait=>"等待",ActionKind.RightDown=>"右键按下",ActionKind.RightUp=>"右键释放",ActionKind.KeyPress=>"键盘按键",ActionKind.KeyDown=>"键盘按下",ActionKind.KeyUp=>"键盘释放",_=>k.ToString()} : "选择操作";
 public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)=>throw new NotSupportedException();
}