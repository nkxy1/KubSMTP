using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using KubSMTP.Core;
using KubSMTP.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Documents;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
namespace KubSMTP.App;
public sealed class SessionViewModel:ObservableObject
{
 private bool busy;
 public bool Busy {get=>busy;set=>SetProperty(ref busy,value);}
 public ConcurrentQueue<string> Pending {get;}=new();
}
public sealed partial class MainWindow:Window
{
 private readonly bool german=Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault()?.StartsWith("de",StringComparison.OrdinalIgnoreCase)==true;
 private static readonly System.Resources.ResourceManager Labels=new("KubSMTP.App.Resources.Strings",typeof(MainWindow).Assembly); private string T(string de,string en)=>Labels.GetString(en,System.Globalization.CultureInfo.GetCultureInfo(german?"de-DE":"en-US")) ?? (german?de:en);
 private readonly SessionViewModel vm=new();private readonly UserData data=new();
 private readonly List<string> paths=[];
 private readonly int[] commonPorts=[25,465,587,2525];
 private readonly string[] charsets=["utf-8","iso-8859-1","windows-1252","us-ascii"];
 private string plainBody="",htmlBody="";private int bodyFormat;
 private readonly SystemThemeController systemTheme;
 private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(150)};private readonly Queue<string> lines=new();private CancellationTokenSource? cts;private bool logVisible=true;
 public MainWindow()
 {
  InitializeComponent();
  systemTheme=new SystemThemeController(root,this);
  Title="KubSMTP";
  AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory,"Assets","KubSMTP.ico"));
  AppWindow.Resize(new Windows.Graphics.SizeInt32(1440,1060));
  UiCapture.Register(root,this,Path.Combine(data.Root,"Exports"));
  ExtendsContentIntoTitleBar=true;SetTitleBar(AppHeader);
  pageTitle.Text=T("SMTP-Verbindung testen","Test an SMTP connection");
  intro.Text=T("Verbindung konfigurieren. Nachricht senden. Antworten verstehen.","Configure a connection. Send a message. Understand the response.");
  toggleLabel.Text=T("Liveprotokoll","Live log");toggleLog.Click+=(_,_)=>{logVisible=!logVisible;ApplyColumns();};
  connectionTitle.Text=T("Verbindung","Connection");hostLabel.Text=T("Servername / IP-Adresse","Server name / IP address");
  encryptionLabel.Text=T("Verschlüsselung","Encryption");authLabel.Text=T("Anmeldung","Authentication");
  userLabel.Text=T("Benutzername","Username");secretLabel.Text=T("Passwort / Zugriffstoken","Password / access token");
  sessionHint.Text=T("Zugangsdaten nur für diese Sitzung","Credentials for this session only");
  foreach(var number in commonPorts)portSelection.Items.Add(number.ToString());
  portSelection.Items.Add(T("Benutzerdefiniert","Custom"));
  portSelection.SelectionChanged+=(_,_)=>{
   var standard=portSelection.SelectedIndex>=0&&portSelection.SelectedIndex<commonPorts.Length;
   port.Visibility=standard?Visibility.Collapsed:Visibility.Visible;
   if(standard)port.Text=commonPorts[portSelection.SelectedIndex].ToString();
  };
  foreach(var text in new[]{T("STARTTLS erforderlich","STARTTLS required"),T("TLS ab Verbindungsbeginn (SSL/TLS)","TLS on connect (SSL/TLS)"),T("STARTTLS falls verfügbar","STARTTLS when available"),T("Unverschlüsselt","Unencrypted")})encryption.Items.Add(text);
  foreach(var text in new[]{T("Keine Anmeldung","No authentication"),T("Benutzername / Passwort","Username / password"),"OAuth2 Token"})authentication.Items.Add(text);
  authentication.SelectionChanged+=(_,_)=>{user.IsEnabled=secret.IsEnabled=authentication.SelectedIndex!=0;};
  encryption.SelectedIndex=0;authentication.SelectedIndex=0;
  testConnection.Content=T("Verbindung testen","Test connection");testConnection.Click+=async(_,_)=>await Run(false);
  profileOptions.Header=T("Serverprofil und Zugangsdaten","Server profile and credentials");
  rememberLabel.Text=T("Zugangsdaten für diesen Windows-Benutzer verschlüsselt speichern","Save credentials encrypted for this Windows user");
  remember.IsChecked=File.Exists(Path.Combine(data.Root,"Secrets","credential.bin"));
  saveProfile.Content=T("Serverprofil speichern","Save server profile");
  saveProfile.Click+=(_,_)=>Guard(()=>{data.Save(new(host.Text,SelectedPort(),(Encryption)encryption.SelectedIndex,(Authentication)authentication.SelectedIndex,user.Text));if(remember.IsChecked==true)Credentials.Save(data.Root,secret.Password);else Credentials.Forget(data.Root);});
  messageTitle.Text=T("Testnachricht","Test message");fromLabel.Text=T("Absender","From");toLabel.Text=T("Empfänger","To");
  ccLabel.Text=T("CC  optional","CC  optional");bccLabel.Text=T("BCC  optional","BCC  optional");
  subjectLabel.Text=T("Betreff","Subject");bodyLabel.Text=T("Nachricht","Message");
  format.Items.Add(T("Klartext","Plain text"));format.Items.Add("HTML");format.SelectedIndex=0;
  foreach(var name in new[]{"UTF-8","ISO-8859-1","Windows-1252","US-ASCII"})charset.Items.Add(name);
  charset.SelectedIndex=0;ToolTipService.SetToolTip(charset,T("Codierung der Nachricht","Message encoding"));
  plainBody=T("Dies ist eine Testmail.","This is a test email.")+Environment.NewLine+Environment.NewLine+
   T("Sie wurde mit KubSMTP versendet, um die SMTP-Verbindung und den Mailversand zu testen.","It was sent with KubSMTP to test the SMTP connection and email delivery.");
  htmlBody=german?"<!DOCTYPE html>\r\n<html lang=\"de\">\r\n<body>\r\n  <h2>Testmail</h2>\r\n  <p>Dies ist eine Testmail.</p>\r\n  <p>Sie wurde mit <strong>KubSMTP</strong> versendet, um die SMTP-Verbindung und den Mailversand zu testen.</p>\r\n</body>\r\n</html>":"<!DOCTYPE html>\r\n<html lang=\"en\">\r\n<body>\r\n  <h2>Test email</h2>\r\n  <p>This is a test email.</p>\r\n  <p>It was sent with <strong>KubSMTP</strong> to test the SMTP connection and email delivery.</p>\r\n</body>\r\n</html>";
  body.Text=plainBody;
  format.SelectionChanged+=(_,_)=>{
   if(bodyFormat==1)htmlBody=body.Text;else plainBody=body.Text;
   bodyFormat=format.SelectedIndex;body.Text=bodyFormat==1?htmlBody:plainBody;
  };
  attachmentSummary.Text=T("Keine Anhänge","No attachments");addAttachment.Content=T("Anhang hinzufügen","Add attachment");
  addAttachment.Click+=async(_,_)=>{try{var picker=new FileOpenPicker();WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));picker.FileTypeFilter.Add("*");foreach(var file in await picker.PickMultipleFilesAsync())if(!paths.Contains(file.Path))paths.Add(file.Path);RenderAttachments();}catch(Exception e){Error(e);}};
  draftMenu.Content=T("Entwurf ▾","Draft ▾");saveDraft.Text=T("Entwurf speichern","Save draft");openDraft.Text=T("Entwurf laden","Load draft");
  saveDraft.Click+=(_,_)=>Guard(()=>File.WriteAllText(Path.Combine(data.Root,"Drafts","draft.json"),JsonSerializer.Serialize(Draft())));
  openDraft.Click+=(_,_)=>Guard(()=>LoadDraft(JsonSerializer.Deserialize<MailDraft>(File.ReadAllText(Path.Combine(data.Root,"Drafts","draft.json")))!));
  var sendLabel=new TextBlock{Text=T("Testmail senden","Send test email")};
  sendLabel.SetBinding(TextBlock.ForegroundProperty,new Microsoft.UI.Xaml.Data.Binding{Source=send,Path=new PropertyPath("Foreground")});
  send.Content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={new FontIcon{Glyph="\uE724",FontSize=14},sendLabel}};send.Click+=async(_,_)=>await Run(true);
  logTitle.Text=T("Liveprotokoll","Live log");logSubtitle.Text=T("SMTP-Kommunikation","SMTP communication");logState.Text=T("BEREIT","READY");
  clearLog.Content=T("Leeren","Clear");clearLog.Click+=(_,_)=>{lines.Clear();logText.Blocks.Clear();};
  copyLog.Content=T("Kopieren","Copy");copyLog.Click+=(_,_)=>{var packet=new DataPackage();packet.SetText(string.Join(Environment.NewLine,lines));Clipboard.SetContent(packet);};
  exportLog.Content=T("Exportieren","Export");exportLog.Click+=(_,_)=>Guard(()=>File.WriteAllText(Path.Combine(data.Root,"Exports",$"SMTP-{DateTime.Now:yyyyMMdd-HHmmss}.txt"),string.Join(Environment.NewLine,lines)));
  // Export is explicit; keep the generated file in the user's export directory.
  redactionHint.Text=T("Zugangsdaten werden maskiert.","Credentials are redacted.");
  ToolTipService.SetToolTip(pause,T("Anzeige pausieren","Pause display"));
  cancel.Content=T("Abbrechen","Cancel");cancel.Click+=(_,_)=>cts?.Cancel();
  versionLabel.Text="v."+typeof(MainWindow).Assembly.GetName().Version!.ToString(3);
  storageLabel.Text=T("Daten: %LOCALAPPDATA%\\KubSMTP","Data: %LOCALAPPDATA%\\KubSMTP");ToolTipService.SetToolTip(storageLabel,data.Root);
  var profile=data.Load();host.Text=profile.Host;var portIndex=Array.IndexOf(commonPorts,profile.Port);portSelection.SelectedIndex=portIndex>=0?portIndex:commonPorts.Length;port.Text=profile.Port.ToString();encryption.SelectedIndex=(int)profile.Encryption;authentication.SelectedIndex=(int)profile.Authentication;user.Text=profile.Username;secret.Password=Credentials.Load(data.Root);
  status.Text=T("Bereit für einen Test","Ready for a test");
  lines.Enqueue(T("Bereit. Test starten, um die SMTP-Kommunikation zu sehen.","Ready. Start a test to see the SMTP exchange."));RenderLog();
  timer.Tick+=(_,_)=>Drain();timer.Start();Closed+=(_,_)=>{cts?.Cancel();timer.Stop();systemTheme.Dispose();};
  divider.DragDelta+=(_,e)=>{workspace.ColumnDefinitions[0].Width=new GridLength(Math.Clamp(workspace.ColumnDefinitions[0].ActualWidth+e.HorizontalChange,320,Math.Max(320,workspace.ActualWidth-300)));};
  foreach(var pair in new (Control Control,string Name)[]{(host,hostLabel.Text),(portSelection,"Port"),(port,T("Benutzerdefinierter Port","Custom port")),(encryption,encryptionLabel.Text),(authentication,authLabel.Text),(user,userLabel.Text),(secret,secretLabel.Text),(from,fromLabel.Text),(to,toLabel.Text),(cc,"CC"),(bcc,"BCC"),(subject,subjectLabel.Text),(body,bodyLabel.Text),(format,T("Nachrichtenformat","Message format")),(charset,T("Codierung der Nachricht","Message encoding"))})Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pair.Control,pair.Name);
 }
 private int SelectedPort()
 {
  if(!int.TryParse(port.Text,out var number)||number is <1 or >65535)throw new ArgumentException(T("Port muss zwischen 1 und 65535 liegen.","Port must be between 1 and 65535."));
  return number;
 }
 private void ApplyColumns(){logPanel.Visibility=logVisible?Visibility.Visible:Visibility.Collapsed;workspace.ColumnDefinitions[2].Width=logVisible?new GridLength(1,GridUnitType.Star):new GridLength(0);workspace.ColumnDefinitions[1].Width=new GridLength(logVisible?12:0);}
 private Button Button(string text,Action action){var b=new Button{Content=text};b.Click+=(_,_)=>action();return b;}
 private MailDraft Draft()=>new(from.Text,to.Text,cc.Text,bcc.Text,subject.Text,body.Text,format.SelectedIndex==1,paths.ToArray(),charsets[Math.Max(0,charset.SelectedIndex)]);
 private void LoadDraft(MailDraft d){from.Text=d.From;to.Text=d.To;cc.Text=d.Cc;bcc.Text=d.Bcc;subject.Text=d.Subject;format.SelectedIndex=d.Html?1:0;body.Text=d.Body;var index=Array.IndexOf(charsets,d.Charset);charset.SelectedIndex=index>=0?index:0;paths.Clear();paths.AddRange(d.Attachments);RenderAttachments();}
 private void RenderAttachments(){attachmentSummary.Text=paths.Count==0?T("Keine Anhänge","No attachments"):paths.Count+T(" Anhang/Anhänge"," attachment(s)");attachments.Children.Clear();foreach(var path in paths.ToArray()){var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};row.Children.Add(new TextBlock{Text=Path.GetFileName(path)+" · "+(File.Exists(path)?new FileInfo(path).Length.ToString("N0")+" B":T("Datei fehlt","File missing")),VerticalAlignment=VerticalAlignment.Center});row.Children.Add(Button(T("Entfernen","Remove"),()=>{paths.Remove(path);RenderAttachments();}));attachments.Children.Add(row);}}
 private void Guard(Action action){try{action();status.Text=T("Gespeichert / geladen","Saved / loaded");}catch(Exception e){Error(e);}}
 private void Error(Exception e)=>status.Text=T("Fehler: ","Error: ")+e.Message;
 private static SolidColorBrush LogBrush(string hex)
 {
  var value=Convert.ToUInt32(hex,16);
  return new SolidColorBrush(ColorHelper.FromArgb(255,(byte)(value>>16),(byte)(value>>8),(byte)value));
 }
 private void RenderLog()
 {
  var paragraph=new Paragraph{Margin=new Thickness(0),LineHeight=20};
  foreach(var line in lines)
  {
   var text=line;var kind='I';var separator=line.IndexOf(' ');
   if(separator>0 && separator+2<line.Length && TimeSpan.TryParse(line[..separator],System.Globalization.CultureInfo.InvariantCulture,out _))
   {
    paragraph.Inlines.Add(new Run{Text=line[..separator]+"  ",Foreground=LogBrush("8B9EB7"),FontSize=10});
    kind=line[separator+1];text=line[(separator+2)..].TrimStart();
    paragraph.Inlines.Add(new Run{Text=kind+"  ",Foreground=LogBrush("96A9C1")});
   }
   paragraph.Inlines.Add(new Run{Text=text,Foreground=LogBrush(kind switch{'C'=>"7BD7F0",'S'=>"BCE29B",'E' or 'W'=>"FFC195",_=>"A8B8CF"})});
   paragraph.Inlines.Add(new LineBreak());
  }
  logText.Blocks.Clear();logText.Blocks.Add(paragraph);
 }
 private void Drain(){if(pause.IsChecked==true)return;var changed=false;while(vm.Pending.TryDequeue(out var line)){lines.Enqueue(line);while(lines.Count>2500)lines.Dequeue();changed=true;}if(changed){RenderLog();if(follow.IsChecked==true)logScroll.ChangeView(null,logScroll.ScrollableHeight,null);}}
 private async Task Run(bool send)
 {
  if(vm.Busy)return;
  var succeeded=false;var cancelled=false;
  try{
   var number=SelectedPort();
   var options=new ConnectionOptions(host.Text,number,(Encryption)encryption.SelectedIndex,(Authentication)authentication.SelectedIndex,user.Text,secret.Password);var draft=send?Draft():null;
   vm.Busy=true;formScroll.IsEnabled=false;cancel.IsEnabled=true;cancel.Visibility=Visibility.Visible;logState.Text="LIVE";cts=new CancellationTokenSource(TimeSpan.FromMinutes(2));status.Text=T("Verbindung läuft …","Connecting …");
   await Task.Run(()=>new SmtpService(german).RunAsync(options,draft,s=>{vm.Pending.Enqueue(s);while(vm.Pending.Count>5000)vm.Pending.TryDequeue(out _);},Path.Combine(data.Logs,$"SMTP-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log"),cts.Token));
   status.Text=send?T("Vom SMTP-Server angenommen. Zustellung nicht bestätigt.","Accepted by SMTP server. Delivery is not confirmed."):T("Verbindung erfolgreich getestet.","Connection tested successfully.");
   succeeded=true;
  }catch(OperationCanceledException){cancelled=true;status.Text=T("Abgebrochen / Zeitüberschreitung. Versandstatus gegebenenfalls unbekannt.","Cancelled / timed out. Delivery status may be unknown.");}catch(System.Text.EncoderFallbackException){status.Text=T("Die gewählte Codierung unterstützt nicht alle Zeichen in Betreff oder Nachricht. Bitte UTF-8 wählen.","The selected encoding cannot represent all characters in the subject or message. Please choose UTF-8.");}catch(Exception e){Error(e);}finally{formScroll.IsEnabled=true;cancel.IsEnabled=false;cancel.Visibility=Visibility.Collapsed;logState.Text=T("BEREIT","READY");cts?.Dispose();cts=null;Drain();data.Rotate();}
  try
  {
   if(send){var dialog=new ContentDialog{
    XamlRoot=root.XamlRoot,
    Title=succeeded?T("Testmail erfolgreich versendet","Test email sent successfully"):cancelled?T("Versand abgebrochen","Sending cancelled"):T("Versand fehlgeschlagen","Sending failed"),
    Content=new TextBlock{Text=succeeded?T("Die Testmail wurde vom SMTP-Server angenommen. Die Zustellung im Postfach ist damit noch nicht bestätigt.","The test email was accepted by the SMTP server. Delivery to the recipient's inbox is not yet confirmed."):status.Text,TextWrapping=TextWrapping.Wrap,MaxWidth=440},
    CloseButtonText="OK",DefaultButton=ContentDialogButton.Close
   };
   dialog.SetBinding(FrameworkElement.RequestedThemeProperty,new Microsoft.UI.Xaml.Data.Binding{Source=root,Path=new PropertyPath("ActualTheme")});
   await dialog.ShowAsync();}
  }
  finally{vm.Busy=false;}
 }
}





