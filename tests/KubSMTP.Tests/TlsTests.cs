using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using KubSMTP.Core;
using KubSMTP.Services;

internal static class TlsTests
{
 public static async Task RunAsync(string directory,MailDraft draft,Action<bool,string> assert)
 {
  foreach(var scenario in new[]{"untrusted","expired","future","hostname","combined"})
  {
   using var rsa=RSA.Create(2048);
   var request=new CertificateRequest("CN=local.test",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
   var san=new SubjectAlternativeNameBuilder();
   if(scenario is "hostname" or "combined")san.AddDnsName("wrong.test");else san.AddIpAddress(IPAddress.Loopback);
   request.CertificateExtensions.Add(san.Build());
   var now=DateTimeOffset.UtcNow;
   using var generated=request.CreateSelfSigned(scenario=="future"?now.AddDays(1):now.AddDays(-2),scenario is "expired" or "combined"?now.AddDays(-1):now.AddDays(2));
   // Windows Schannel needs a persisted private key for the local TLS server.
   using var certificate=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.DefaultKeySet);
   foreach(var mode in new[]{Encryption.ImplicitTls,Encryption.StartTls,Encryption.Opportunistic})
   foreach(var german in new[]{false,true})
   {
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
    var port=((IPEndPoint)listener.LocalEndpoint).Port;
    var commands=new List<string>();
    var server=ServeAsync(listener,certificate,mode,commands,timeout.Token);
    var entries=new List<string>();
    var logPath=Path.Combine(directory,$"tls-{scenario}-{mode}-{german}.log");
    try
    {
     await new SmtpService(german).RunAsync(new("127.0.0.1",port,mode,Authentication.Password,"test-user","tls-secret-password"),draft,entries.Add,logPath,timeout.Token);
     await server;
     var label=$"{scenario}, {mode}, {(german?"de":"en")}";
     var live=string.Join('\n',entries);
     var file=File.ReadAllText(logPath);
     assert(commands.Contains("QUIT")&&commands.Contains("DATA")&&commands.Any(command=>command.StartsWith("AUTH ")),"TLS connection, authentication and mail completed: "+label);
     assert(live.Contains(german?"Serverzertifikat ungültig":"Invalid server certificate")&&live.Contains("W TLS:"),"Certificate warning in live monitor: "+label);
     assert(file.Contains(live.Split('\n').First(line=>line.Contains("W TLS:")))&&entries.All(file.Contains),"Certificate diagnostics also written to file: "+label);
     assert(live.Contains("UntrustedRoot")||live.Contains("PartialChain"),"Untrusted certificate reason: "+label);
     assert(live.Contains(german?"Hostname stimmt nicht überein":"Hostname mismatch")== (scenario is "hostname" or "combined"),"Accurate hostname diagnosis: "+label);
     assert(live.Contains(german?"ist abgelaufen":"has expired")== (scenario is "expired" or "combined"),"Accurate expiry diagnosis: "+label);
     assert(live.Contains(german?"ist noch nicht gültig":"is not yet valid")== (scenario=="future"),"Accurate future validity diagnosis: "+label);
     assert(live.Contains("I TLS:")&&!live.Contains("I TLS: OFF"),"Connection stays encrypted: "+label);
     assert(!file.Contains("tls-secret-password")&&!file.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("\0test-user\0tls-secret-password")))&&!file.Contains(draft.Body),"TLS log redacts credentials and message: "+label);
    }
    finally
    {
     timeout.Cancel();listener.Stop();
     // Observe the server task even if a client assertion or handshake fails.
     try{await server;}catch(OperationCanceledException)when(timeout.IsCancellationRequested){}
    }
   }
  }
 }

 private static async Task ServeAsync(TcpListener listener,X509Certificate2 certificate,Encryption mode,List<string> commands,CancellationToken cancellation)
 {
  using var socket=await listener.AcceptTcpClientAsync(cancellation);
  using var network=socket.GetStream();
  using var tls=new SslStream(network,leaveInnerStreamOpen:true);
  Stream stream=network;
  var secure=mode==Encryption.ImplicitTls;
  if(secure){await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate},cancellation);stream=tls;}
  StreamReader Reader()=>new(stream,Encoding.ASCII,false,1024,leaveOpen:true);
  StreamWriter Writer()=>new(stream,Encoding.ASCII,1024,leaveOpen:true){NewLine="\r\n",AutoFlush=true};
  var reader=Reader();var writer=Writer();
  try
  {
   await writer.WriteLineAsync("220 local.test ESMTP".AsMemory(),cancellation);
   var data=false;
   while(await reader.ReadLineAsync(cancellation) is {} line)
   {
    if(data){if(line=="."){data=false;await writer.WriteLineAsync("250 queued".AsMemory(),cancellation);}continue;}
    commands.Add(line);
    if(line.StartsWith("EHLO"))await writer.WriteLineAsync((secure?"250-local.test\r\n250 AUTH PLAIN":"250-local.test\r\n250 STARTTLS").AsMemory(),cancellation);
    else if(line=="STARTTLS")
    {
     await writer.WriteLineAsync("220 Begin TLS".AsMemory(),cancellation);
     reader.Dispose();writer.Dispose();
     await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate},cancellation);
     stream=tls;secure=true;reader=Reader();writer=Writer();
    }
    else if(line.StartsWith("AUTH "))await writer.WriteLineAsync("235 authenticated".AsMemory(),cancellation);
    else if(line=="DATA"){data=true;await writer.WriteLineAsync("354 go".AsMemory(),cancellation);}
    else if(line=="QUIT"){await writer.WriteLineAsync("221 bye".AsMemory(),cancellation);break;}
    else await writer.WriteLineAsync("250 OK".AsMemory(),cancellation);
   }
  }
  finally{reader.Dispose();writer.Dispose();}
 }
}
