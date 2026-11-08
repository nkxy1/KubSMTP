using System.Text;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using KubSMTP.Core;
namespace KubSMTP.Services;
public sealed class SmtpService(bool german = false)
{
 public static InternetAddressList Addresses(string value) => string.IsNullOrWhiteSpace(value)?new():InternetAddressList.Parse(value.Replace(';',','));
 public static MimeMessage Compose(MailDraft draft)
 {
  Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
  var encoding=Encoding.GetEncoding(draft.Charset,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
  // Reject unrepresentable text instead of silently replacing characters with '?'.
  encoding.GetByteCount(draft.Body);encoding.GetByteCount(draft.Subject);
  var message=new MimeMessage();message.From.Add(MailboxAddress.Parse(draft.From));message.To.AddRange(Addresses(draft.To));message.Cc.AddRange(Addresses(draft.Cc));message.Subject=draft.Subject;
  message.Headers.Replace(HeaderId.Subject,encoding,draft.Subject);
  var builder=new BodyBuilder();if(draft.Html)builder.HtmlBody=draft.Body;else builder.TextBody=draft.Body;
  foreach(var file in draft.Attachments)builder.Attachments.Add(file);
  message.Body=builder.ToMessageBody();
  foreach(var part in message.BodyParts.OfType<TextPart>().Where(part=>!part.IsAttachment))part.SetText(encoding,draft.Body);
  return message;
 }
 public async Task RunAsync(ConnectionOptions options,MailDraft? draft,Action<string> log,string logPath,CancellationToken cancellation)
 {
  if(string.IsNullOrWhiteSpace(options.Host)||options.Port is <1 or >65535)throw new ArgumentException("Invalid server / port");
  using var writer=new StreamWriter(logPath,false,Encoding.UTF8){AutoFlush=true};
  void Write(string s){var line=$"{DateTime.Now:HH:mm:ss.fff} {s}";writer.WriteLine(line);log(line);}
  using var logger=new SafeLogger(Write);using var client=new SmtpClient(logger){Timeout=30000};
  var host=options.Host.Trim().Trim('[',']');
  client.ServerCertificateValidationCallback=(_,certificate,chain,errors)=>
  {
   if(errors==System.Net.Security.SslPolicyErrors.None)return true;
   Write(german?"W TLS: Serverzertifikat ungültig. Die TLS-Verbindung wird trotzdem fortgesetzt.":"W TLS: Invalid server certificate. Continuing the TLS connection anyway.");
   foreach(var reason in TlsCertificateDiagnostics.Describe(host,certificate,chain,errors,german))Write("W TLS: "+reason);
   return certificate is not null;
  };
  using var message=draft is null?null:Compose(draft);
  var recipients=draft is null?[]:Addresses(draft.To).Mailboxes.Concat(Addresses(draft.Cc).Mailboxes).Concat(Addresses(draft.Bcc).Mailboxes).ToArray();
  if(draft is not null&&recipients.Length==0)throw new ArgumentException("No recipients");
  var mode=options.Encryption switch{Encryption.ImplicitTls=>SecureSocketOptions.SslOnConnect,Encryption.StartTls=>SecureSocketOptions.StartTls,Encryption.Opportunistic=>SecureSocketOptions.StartTlsWhenAvailable,_=>SecureSocketOptions.None};
  try{
   await client.ConnectAsync(host,options.Port,mode,cancellation);
   client.Capabilities &= ~(SmtpCapabilities.Chunking | SmtpCapabilities.BinaryMime);
   Write(client.IsSecure?$"I TLS: {client.SslProtocol}":"I TLS: OFF");
   if(options.Authentication!=Authentication.None){if(!client.IsSecure)throw new InvalidOperationException("Authentication requires TLS");if(options.Authentication==Authentication.OAuth2)await client.AuthenticateAsync(new SaslMechanismOAuth2(options.Username,options.Secret),cancellation);else await client.AuthenticateAsync(options.Username,options.Secret,cancellation);}
   if(message is not null){if(client.MaxSize>0){using var count=new CountingStream();await message.WriteToAsync(count,cancellation);if(count.Length>client.MaxSize)throw new InvalidOperationException("Message exceeds server SIZE limit");}await client.SendAsync(message,message.From.Mailboxes.Single(),recipients,cancellation);}
   await client.DisconnectAsync(true,cancellation);
  }finally{logger.Flush();}
 }
}
// Deliberately suppresses all authentication exchanges and DATA bytes before either sink.
public sealed class SafeLogger(Action<string> output):IProtocolLogger
{
 public IAuthenticationSecretDetector? AuthenticationSecretDetector {get;set;} = null!;
 private readonly Decoder clientDecoder=Encoding.UTF8.GetDecoder(),serverDecoder=Encoding.UTF8.GetDecoder(); private readonly StringBuilder client=new(),server=new();private bool auth,data,awaitData;
 public void LogConnect(Uri uri)=>output($"I CONNECT {uri.Host}:{uri.Port}");
 public void LogClient(byte[] buffer,int offset,int count)=>Decode(clientDecoder,client,buffer,offset,count,true);
 public void LogServer(byte[] buffer,int offset,int count)=>Decode(serverDecoder,server,buffer,offset,count,false);
 private void Decode(Decoder decoder,StringBuilder pending,byte[] bytes,int offset,int count,bool sent){var chars=new char[count+2];var size=decoder.GetChars(bytes,offset,count,chars,0,false);Feed(pending,new string(chars,0,size),sent);}
 private void Feed(StringBuilder pending,string text,bool sent){foreach(var ch in text){if(ch=='\n'){Line(pending.ToString().TrimEnd('\r'),sent);pending.Clear();}else if(pending.Length<8192)pending.Append(ch);}}
 private void Line(string line,bool sent)
 {
  if(sent){if(auth)return;if(data){if(line=="."){data=false;output("C [MIME DATA omitted]");}return;}if(line.StartsWith("AUTH ",StringComparison.OrdinalIgnoreCase)){auth=true;output("C AUTH [REDACTED]");return;}if(line=="DATA")awaitData=true;output("C "+line);}
  else{if(auth){if(line.StartsWith("235")||line.StartsWith("535")||line.StartsWith("5")){auth=false;output("S "+line[..Math.Min(3,line.Length)]+" AUTH result");}return;}if(awaitData){data=line.StartsWith("354");awaitData=false;}output("S "+line);}
 }
 public void Flush(){client.Clear();server.Clear();}
 public void Dispose()=>Flush();
}




