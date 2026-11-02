using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace InputFlowStudio;
public static class InputValidation {
 public static void Commit(DependencyObject root){if(root is Window window&&window.Content is DependencyObject content)root=content;foreach(var box in TextBoxes(root)){box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();if(Validation.GetHasError(box)){box.Focus();throw new InvalidOperationException("请先修正参数输入，保存或运行未执行");}}}
 static IEnumerable<TextBox> TextBoxes(DependencyObject parent){if(parent is TextBox box)yield return box;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var child in TextBoxes(VisualTreeHelper.GetChild(parent,i)))yield return child;}
}
