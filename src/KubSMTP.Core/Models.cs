namespace KubSMTP.Core;
public enum Encryption { StartTls, ImplicitTls, Opportunistic, None }
public enum Authentication { None, Password, OAuth2 }
public sealed record ConnectionOptions(string Host, int Port, Encryption Encryption, Authentication Authentication, string Username, string Secret);
public sealed record MailDraft(string From, string To, string Cc, string Bcc, string Subject, string Body, bool Html, string[] Attachments, string Charset = "utf-8");
public sealed record ServerProfile(string Host = "", int Port = 587, Encryption Encryption = Encryption.StartTls, Authentication Authentication = Authentication.None, string Username = "");
public static class ProductVersion
{
 public static (int Major,int Minor,int Patch) Next(int major,int minor,int patch) => patch >= 100 ? (major,minor+1,0) : (major,minor,patch+1);
}
