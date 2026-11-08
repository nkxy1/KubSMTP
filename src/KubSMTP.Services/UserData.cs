using System.Text.Json;
using KubSMTP.Core;
namespace KubSMTP.Services;
public sealed class UserData
{
 public string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KubSMTP");
 public string Logs => Path.Combine(Root,"Logs");
 public UserData(){Directory.CreateDirectory(Logs);foreach(var name in new[]{"Drafts","Exports"})Directory.CreateDirectory(Path.Combine(Root,name));Rotate();}
 public ServerProfile Load(){try{return JsonSerializer.Deserialize<ServerProfile>(File.ReadAllText(Path.Combine(Root,"profiles.json")))??new();}catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){return new();}}
 public void Save(ServerProfile profile){var path=Path.Combine(Root,"profiles.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(profile));File.Move(path+".tmp",path,true);}
 public void Rotate(){var files=new DirectoryInfo(Logs).GetFiles("*.log").OrderByDescending(f=>f.LastWriteTimeUtc);long size=0;foreach(var f in files){size+=f.Length;if(f.LastWriteTimeUtc<DateTime.UtcNow.AddDays(-14)||size>50L*1024*1024)try{f.Delete();}catch(IOException){}}}
}
