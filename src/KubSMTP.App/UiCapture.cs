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
    if(arguments.Contains("--capture-bottom") && root.FindName("formScroll") is Microsoft.UI.Xaml.Controls.ScrollView scroll){scroll.ScrollTo(0,scroll.ScrollableHeight,new Microsoft.UI.Xaml.Controls.ScrollingScrollOptions(Microsoft.UI.Xaml.Controls.ScrollingAnimationMode.Disabled));await Task.Delay(300);}
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

