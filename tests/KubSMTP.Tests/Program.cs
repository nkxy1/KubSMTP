using System.Net;
using System.Net.Sockets;
using System.Text;
using KubSMTP.Core;
using KubSMTP.Services;
static void Assert(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
Assert(ProductVersion.Next(1,0,99)==(1,0,100),"Version 99 → 100");Assert(ProductVersion.Next(1,0,100)==(1,1,0),"Version 100 → minor");
var draft=new MailDraft("Sender <sender@example.test>","Alice <alice@example.test>; bob@example.test","cc@example.test","hidden@example.test","Grüße","Geheimer Body",false,[]);
using(var message=SmtpService.Compose(draft)){Assert(message.To.Mailboxes.Count()==2,"Address lists with display names");Assert(!message.ToString().Contains("hidden@example.test"),"No BCC header");Assert(message.TextBody==draft.Body,"UTF-8 body");}
foreach(var (charset,text) in new[]{("utf-8","Grüße – Test 😊"),("iso-8859-1","Grüße"),("windows-1252","Grüße – 10 €"),("us-ascii","SMTP test")})
foreach(var html in new[]{false,true})
{
 var content=html?$"<html><body><p>{text}</p></body></html>":text;
 using var message=SmtpService.Compose(draft with{Body=content,Subject=text,Html=html,Charset=charset});
 using var wire=new MemoryStream();message.WriteTo(wire);wire.Position=0;
 using var decoded=MimeKit.MimeMessage.Load(wire);
 var part=decoded.BodyParts.OfType<MimeKit.TextPart>().Single();
 Assert(part.ContentType.Charset==charset&&part.Text.TrimEnd('\r','\n')==content&&decoded.Subject==text,$"MIME encoding roundtrip: {charset}, HTML={html} (actual charset: {part.ContentType.Charset})");
 Assert(Encoding.ASCII.GetString(wire.ToArray()).Contains(charset,StringComparison.OrdinalIgnoreCase),$"Selected charset in wire headers: {charset}, HTML={html}");
}
try{using var invalid=SmtpService.Compose(draft with{Charset="us-ascii",Body="Grüße"});throw new Exception("Unrepresentable characters were accepted");}catch(EncoderFallbackException){Assert(true,"Unsupported characters rejected without data loss");}
var legacyJson=System.Text.Json.JsonSerializer.Serialize(draft).Replace(",\"Charset\":\"utf-8\"","");
Assert(System.Text.Json.JsonSerializer.Deserialize<MailDraft>(legacyJson)!.Charset=="utf-8","Legacy drafts default to UTF-8");
Assert(System.Text.Json.JsonSerializer.Deserialize<MailDraft>(System.Text.Json.JsonSerializer.Serialize(draft with{Charset="windows-1252"}))!.Charset=="windows-1252","Draft encoding is persisted");
var entries=new List<string>();using(var logger=new SafeLogger(entries.Add)){
 void C(string s){var b=Encoding.UTF8.GetBytes(s);foreach(var v in b)logger.LogClient([v],0,1);}
 void S(string s){var b=Encoding.UTF8.GetBytes(s);logger.LogServer(b,0,b.Length);}
 C("AUTH LOGIN\r\n");S("334 challenge\r\n");C("secret-password\r\n");S("235 OK\r\n");C("DATA\r\n");S("354 go\r\n");C("Geheimer Body\r\n.\r\n");S("250 OK\r\n");
 Assert(!string.Join('\n',entries).Contains("secret-password")&&!string.Join('\n',entries).Contains("Geheimer Body"),"Fragmented AUTH and DATA redaction");
}
foreach(var mode in new[]{Encryption.None,Encryption.Opportunistic,Encryption.StartTls}){
 var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var number=((IPEndPoint)listener.LocalEndpoint).Port;var commands=new List<string>();var received=new List<string>();
 var server=Task.Run(async()=>{using var socket=await listener.AcceptTcpClientAsync();using var stream=socket.GetStream();using var r=new StreamReader(stream);using var w=new StreamWriter(stream){NewLine="\r\n",AutoFlush=true};await w.WriteLineAsync("220 local.test ESMTP");bool data=false;while(await r.ReadLineAsync() is {} line){commands.Add(line);if(data){if(line=="."){data=false;await w.WriteLineAsync("250 queued");}else received.Add(line);continue;}if(line.StartsWith("EHLO"))await w.WriteLineAsync("250 local.test");else if(line=="DATA"){data=true;await w.WriteLineAsync("354 go");}else if(line=="QUIT"){await w.WriteLineAsync("221 bye");break;}else await w.WriteLineAsync("250 OK");}});
 var log=Path.GetTempFileName();try{await new SmtpService().RunAsync(new("127.0.0.1",number,mode,Authentication.None,"",""),draft,_=>{},log,new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);Assert(mode!=Encryption.StartTls,"STARTTLS required must fail");Assert(commands.Contains("RCPT TO:<hidden@example.test>"),"BCC envelope "+mode);Assert(!string.Join('\n',received).Contains("hidden@example.test"),"BCC hidden from DATA "+mode);Assert(!File.ReadAllText(log).Contains("Geheimer Body"),"File log body redaction "+mode);}catch(NotSupportedException) when(mode==Encryption.StartTls){Assert(true,"Missing required STARTTLS rejected");}finally{listener.Stop();File.Delete(log);}await server;
}
var temp=Path.Combine(Path.GetTempPath(),"KubSMTP-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
try{
 var file=Path.Combine(temp,"Anhang.txt");File.WriteAllText(file,"Unicode äöü");
 using(var message=SmtpService.Compose(draft with{Attachments=[file]})){Assert(message.Attachments.Count()==1,"MIME attachment");}
 if(OperatingSystem.IsWindows()){Credentials.Save(temp,"private-token");Assert(Credentials.Load(temp)=="private-token","DPAPI roundtrip");Assert(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(temp,"Secrets","credential.bin"))).Contains("private-token"),"DPAPI ciphertext");Credentials.Forget(temp);}
 await TlsTests.RunAsync(temp,draft,Assert);
}finally{Directory.Delete(temp,true);}
Console.WriteLine("All checks passed.");
