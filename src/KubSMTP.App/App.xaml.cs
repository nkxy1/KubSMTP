using Microsoft.UI.Xaml;
namespace KubSMTP.App;
public partial class App:Application
{
 private Window? window;
 public App(){UnhandledException+=(_,e)=>WriteStartupError(e.Exception);try{InitializeComponent();}catch(Exception e){WriteStartupError(e);throw;} }
 private static void WriteStartupError(Exception e){var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KubSMTP","Logs");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"startup-error.log"),e.ToString());}
 protected override void OnLaunched(LaunchActivatedEventArgs args){try{window=new MainWindow();window.Activate();}catch(Exception e){WriteStartupError(e);throw;}}
}


