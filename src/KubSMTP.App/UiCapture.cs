using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
namespace KubSMTP.App;

// Development-only capture of this app's own visual tree, without desktop automation.
internal static class UiCapture
{
 public static void Register(FrameworkElement root,Window window,string exportDirectory)
 {
  var arguments=Environment.GetCommandLineArgs();
  if(!arguments.Contains("--capture-ui"))return;
  bool dark=arguments.Contains("--capture-dark");
  root.RequestedTheme=dark?ElementTheme.Dark:ElementTheme.Light;
  bool compact=arguments.Contains("--capture-compact");
  if(compact)window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1040,960));
  async Task Save(string name)
  {
   var image=new RenderTargetBitmap();await image.RenderAsync(root);
   var pixels=await image.GetPixelsAsync();
   var folder=await StorageFolder.GetFolderFromPathAsync(exportDirectory);
   var file=await folder.CreateFileAsync(name,CreationCollisionOption.ReplaceExisting);
   using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
   var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
   encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)image.PixelWidth,(uint)image.PixelHeight,96,96,pixels.ToArray());
   await encoder.FlushAsync();
  }
  root.Loaded+=async(_,_)=>{
   try{
    await Task.Delay(1500);
    if(arguments.Contains("--capture-layout-cycle") && window is MainWindow main)
    {
     var panel=(FrameworkElement)root.FindName("logPanel");
     var form=(FrameworkElement)root.FindName("formScroll");
     var fields=(FrameworkElement)root.FindName("fields");
     var initial=window.AppWindow.ClientSize;
     if(panel.Visibility!=Visibility.Collapsed)throw new InvalidOperationException("Live log must be hidden on startup");
     var workspace=(FrameworkElement)root.FindName("workspace");
     if(root is Microsoft.UI.Xaml.Controls.Grid layout && layout.RowDefinitions[2].ActualHeight-workspace.ActualHeight-workspace.Margin.Top-workspace.Margin.Bottom>2)throw new InvalidOperationException("Unused space above status bar");
     await Save("UI-layout-start.png");
     main.ToggleLog();await Task.Delay(400);
     var expanded=window.AppWindow.ClientSize;
     if(panel.Visibility!=Visibility.Visible || expanded.Width<=initial.Width)throw new InvalidOperationException("Live log must expand the window");
     if(Math.Abs(panel.ActualWidth-form.ActualWidth)>4 || Math.Abs(panel.ActualHeight-form.ActualHeight)>1)throw new InvalidOperationException("Form and live log must have equal size");
     if(form.ActualHeight>fields.ActualHeight+1)throw new InvalidOperationException("Unused space below form");
     await Save("UI-layout-log.png");
     main.ToggleLog();await Task.Delay(400);
     var restored=window.AppWindow.ClientSize;
     if(panel.Visibility!=Visibility.Collapsed || Math.Abs(restored.Width-initial.Width)>2 || restored.Height!=initial.Height)throw new InvalidOperationException($"Hiding the log must restore the compact window: {initial.Width}x{initial.Height} -> {expanded.Width}x{expanded.Height} -> {restored.Width}x{restored.Height}");
     File.WriteAllText(Path.Combine(exportDirectory,"layout-check.json"),System.Text.Json.JsonSerializer.Serialize(new{Initial=initial.Width,Expanded=expanded.Width,Restored=restored.Width,Height=restored.Height,EqualPaneSizes=true,NoUnusedFormSpace=true}));
    }
    if(arguments.Contains("--capture-log") && window is MainWindow logWindow){logWindow.ToggleLog();await Task.Delay(400);}
    if(arguments.Contains("--capture-bottom") && root.FindName("formScroll") is Microsoft.UI.Xaml.Controls.ScrollViewer scroll){scroll.ChangeView(null,scroll.ScrollableHeight,null,true);await Task.Delay(300);}
    if(arguments.Contains("--capture-theme-cycle"))
    {
     var opposite=dark?ElementTheme.Light:ElementTheme.Dark;
     root.RequestedTheme=opposite;await Task.Delay(400);
     if(root.ActualTheme!=opposite)throw new InvalidOperationException("Runtime theme change failed");
     await Save($"UI-preview-cycle-{(dark?"light":"dark")}.png");
     root.RequestedTheme=dark?ElementTheme.Dark:ElementTheme.Light;await Task.Delay(400);
    }
    if(root.ActualTheme!=(dark?ElementTheme.Dark:ElementTheme.Light))throw new InvalidOperationException("Requested capture theme was not applied");
    await Save($"UI-preview-{(arguments.Contains("--capture-bottom")?"bottom-":"")}{(dark?"dark":"light")}{(compact?"-compact":"")}.png");
   }catch(Exception e){File.WriteAllText(Path.Combine(exportDirectory,"capture-error.txt"),e.ToString());}
   finally{window.Close();}
  };
 }
}

