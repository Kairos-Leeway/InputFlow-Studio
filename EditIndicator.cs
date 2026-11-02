using System.Windows;
using System.Windows.Controls;
namespace InputFlowStudio;
public static class EditIndicator {
 public static readonly DependencyProperty ChangedProperty=DependencyProperty.RegisterAttached("Changed",typeof(bool),typeof(EditIndicator),new PropertyMetadata(false,Changed));
 public static bool GetChanged(DependencyObject value)=>(bool)value.GetValue(ChangedProperty);
 public static void SetChanged(DependencyObject value,bool changed)=>value.SetValue(ChangedProperty,changed);
 static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs args){string key=(bool)args.NewValue?"DirtyInk":"Line";if(target is Control control)control.SetResourceReference(Control.BorderBrushProperty,key);else if(target is Border border){border.SetResourceReference(Border.BorderBrushProperty,key);border.BorderThickness=(bool)args.NewValue?new Thickness(1):new Thickness(1,0,0,0);border.CornerRadius=new CornerRadius(6);}}
}
