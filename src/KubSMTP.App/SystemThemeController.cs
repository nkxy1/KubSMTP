using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace KubSMTP.App;

// UISettings notifies on a worker thread; XAML changes belong on the UI thread.
internal sealed class SystemThemeController : IDisposable
{
 private readonly FrameworkElement root;
 private readonly Window window;
 private readonly UISettings settings=new();
 private bool disposed;

 public SystemThemeController(FrameworkElement root,Window window)
 {
  this.root=root;this.window=window;
  settings.ColorValuesChanged+=ColorsChanged;
  root.ActualThemeChanged+=ThemeChanged;
  ApplySystemTheme();
 }

 private void ColorsChanged(UISettings sender,object args)=>root.DispatcherQueue.TryEnqueue(()=>{
  if(!disposed)ApplySystemTheme();
 });

 private void ApplySystemTheme()
 {
  var setting=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",null);
  var background=settings.GetColorValue(UIColorType.Background);
  var dark=setting is int value?value==0:background.R+background.G+background.B<384;
  root.RequestedTheme=dark?ElementTheme.Dark:ElementTheme.Light;
  UpdateCaption();
 }

 private void ThemeChanged(FrameworkElement sender,object args)=>UpdateCaption();
 private void UpdateCaption()
 {
  window.AppWindow.TitleBar.ButtonBackgroundColor=Colors.Transparent;
  window.AppWindow.TitleBar.ButtonInactiveBackgroundColor=Colors.Transparent;
  window.AppWindow.TitleBar.ButtonForegroundColor=root.ActualTheme==ElementTheme.Dark?Colors.White:Colors.Black;
 }

 public void Dispose()
 {
  disposed=true;
  settings.ColorValuesChanged-=ColorsChanged;
  root.ActualThemeChanged-=ThemeChanged;
 }
}
