param([string]$Executable,[string]$Label='startup',[int]$Samples=3)
$ErrorActionPreference='Stop'
function Read-SharedTrace([string]$Path) {
 try {
  $taskStream=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
  $taskReader=[IO.StreamReader]::new($taskStream)
  try{return $taskReader.ReadToEnd()}finally{$taskReader.Dispose()}
 }catch [IO.IOException]{return ''}
}
$taskQa=Join-Path ([IO.Path]::GetTempPath()) ('KubSMTP-startup-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskQa -Force | Out-Null
$taskExe=Join-Path $taskQa 'KubSMTP.App.exe'
Copy-Item -LiteralPath $Executable -Destination $taskExe
$taskFile=[IO.File]::OpenRead($taskExe)
try{$taskFile.Position=$taskFile.Length-56;$taskFooter=[byte[]]::new(8);[void]$taskFile.Read($taskFooter,0,8);$taskNative=[Text.Encoding]::ASCII.GetString($taskFooter).StartsWith('KUBEND1')}finally{$taskFile.Dispose()}
$taskResults=@()
for($taskIndex=0;$taskIndex -lt $Samples;$taskIndex++) {
 $taskInfo=[Diagnostics.ProcessStartInfo]::new($taskExe)
 $taskInfo.UseShellExecute=$false
 $taskInfo.WindowStyle=[Diagnostics.ProcessWindowStyle]::Normal
 $taskInfo.Arguments='--capture-ui --capture-compact'
 $taskInfo.WorkingDirectory=$taskQa
 $taskInfo.EnvironmentVariables['DOTNET_BUNDLE_EXTRACT_BASE_DIR']=Join-Path $taskQa 'extracted'
 $taskTrace=Join-Path $taskQa "startup-$taskIndex.txt"
 $taskInfo.EnvironmentVariables['KUBSMTP_PORTABLE_CACHE']=Join-Path $taskQa 'cache'
 $taskInfo.EnvironmentVariables['KUBSMTP_PORTABLE_TRACE']=$taskTrace
 $taskWatch=[Diagnostics.Stopwatch]::StartNew()
 $taskProcess=[Diagnostics.Process]::Start($taskInfo)
 $taskVisible=$null
 $taskFeedback=$null
 while(!$taskProcess.HasExited -and $taskWatch.Elapsed.TotalSeconds -lt 30) {
  $taskProcess.Refresh()
  if($null -eq $taskFeedback -and $taskProcess.MainWindowHandle -ne [IntPtr]::Zero){$taskFeedback=$taskWatch.Elapsed.TotalMilliseconds}
  if($taskNative){
   if($null -eq $taskVisible -and (Read-SharedTrace $taskTrace).Contains('main-window 1')){$taskVisible=$taskWatch.Elapsed.TotalMilliseconds;if($null -eq $taskFeedback){$taskFeedback=$taskVisible}}
  }elseif($null -eq $taskVisible){$taskVisible=$taskFeedback}
  Start-Sleep -Milliseconds 25
 }
 if(!$taskProcess.HasExited){$taskProcess.Kill();throw 'Startup timeout'}
 if($taskProcess.ExitCode -ne 0 -or $null -eq $taskVisible){throw "Startup failed: $($taskProcess.ExitCode)"}
 $taskResults += [pscustomobject]@{Run=$taskIndex+1;Cache=if($taskIndex -eq 0){'empty extraction cache'}else{'reused extraction cache'};FeedbackMs=[Math]::Round($taskFeedback);WindowMs=[Math]::Round($taskVisible);ExitMs=[Math]::Round($taskWatch.Elapsed.TotalMilliseconds)}
 $taskProcess.Dispose()
}
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskResults | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRoot "artifacts/startup-$Label.json")
$taskResults | Format-Table
