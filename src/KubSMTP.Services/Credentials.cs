using System.Security.Cryptography;
using System.Text;
namespace KubSMTP.Services;
public static class Credentials
{
 public static void Save(string root,string value){if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();Directory.CreateDirectory(Path.Combine(root,"Secrets"));File.WriteAllBytes(Path.Combine(root,"Secrets","credential.bin"),ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));}
 public static string Load(string root){if(!OperatingSystem.IsWindows())return "";try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(root,"Secrets","credential.bin")),null,DataProtectionScope.CurrentUser));}catch(Exception e) when(e is IOException or CryptographicException or UnauthorizedAccessException){return "";}}
 public static void Forget(string root)=>File.Delete(Path.Combine(root,"Secrets","credential.bin"));
}
