using System.Windows;
using System.Windows.Controls;
using InputFlowStudio;

static partial class Checks {
 static void AgreementChecks(){
  string text=LicenseWindow.ReadAgreement();
  Assert(text.Contains("协议版本：1.1")&&text.Contains("重大过失")&&text.Contains("GitHub")&&text.Contains("未采用应用级加密"),"Embedded agreement is missing material disclosures");
  foreach(bool light in new[]{true,false}){
   var window=new LicenseWindow(light);var root=(FrameworkElement)window.Content;root.Measure(new Size(730,680));root.Arrange(new Rect(0,0,730,680));root.UpdateLayout();
   var box=Descendants(root).OfType<TextBox>().Single();Assert(box.IsReadOnly&&box.Text==text&&box.VerticalScrollBarVisibility==ScrollBarVisibility.Auto,"Agreement viewer cannot show full embedded text");window.Close();
  }
  Console.WriteLine("Embedded EULA 1.1, material risk/privacy disclosures, light/dark read-only scrollable agreement viewer: PASS");
 }
}
