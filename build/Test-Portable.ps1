param([string]$Executable=(Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/KubSMTP.App.exe'),[int]$TimeoutSeconds=45)
$ErrorActionPreference='Stop'
$taskSource=Get-Item -LiteralPath $Executable
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskQa=Join-Path ([IO.Path]::GetTempPath()) ('KubSMTP-portable-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskQa -Force | Out-Null
$taskExe=Join-Path $taskQa 'KubSMTP-Portable.exe'
Copy-Item -LiteralPath $taskSource.FullName -Destination $taskExe
$taskEmptyRuntime=Join-Path $taskQa 'no-runtime'
New-Item -ItemType Directory -Path $taskEmptyRuntime -Force | Out-Null
$taskExtract=Join-Path $taskQa 'cache'
$taskExports=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'KubSMTP/Exports'
foreach($taskMode in @('cold','warm')) {
 $taskStarted=Get-Date
 $taskInfo=[Diagnostics.ProcessStartInfo]::new($taskExe)
 $taskInfo.UseShellExecute=$false
 $taskInfo.WorkingDirectory=$taskQa
 $taskInfo.WindowStyle=[Diagnostics.ProcessWindowStyle]::Normal
 $taskInfo.Arguments='--capture-ui --capture-theme-cycle --capture-compact'
 foreach($taskName in @('DOTNET_ROOT','DOTNET_ROOT_X64')){$taskInfo.EnvironmentVariables[$taskName]=$taskEmptyRuntime}
 $taskInfo.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP']='0'
 $taskInfo.EnvironmentVariables['DOTNET_DISABLE_GUI_ERRORS']='1'
 $taskInfo.EnvironmentVariables['DOTNET_HOST_TRACE']='1'
 $taskInfo.EnvironmentVariables['DOTNET_HOST_TRACEFILE']=Join-Path $taskQa "$taskMode-host.txt"
 $taskInfo.EnvironmentVariables['KUBSMTP_PORTABLE_CACHE']=$taskExtract
 $taskInfo.EnvironmentVariables['KUBSMTP_PORTABLE_TRACE']=Join-Path $taskQa "$taskMode-bootstrap.txt"
 $taskInfo.EnvironmentVariables['PATH']="$env:SystemRoot\System32;$env:SystemRoot"
 $taskProcess=[Diagnostics.Process]::Start($taskInfo)
 try {
  if(!$taskProcess.WaitForExit($TimeoutSeconds*1000)){$taskProcess.Kill();throw "Portable startup timed out. Diagnostics: $taskQa"}
  if($taskProcess.ExitCode -ne 0){throw "Portable startup failed ($($taskProcess.ExitCode)). Diagnostics: $taskQa"}
 }finally{$taskProcess.Dispose()}
 foreach($taskImage in @('UI-preview-light-compact.png','UI-preview-cycle-dark.png')) {
  $taskCapture=Get-Item -LiteralPath (Join-Path $taskExports $taskImage)
  if($taskCapture.LastWriteTime -lt $taskStarted -or $taskCapture.Length -lt 1024){throw "No new rendered UI: $taskImage"}
 }
 $taskHost=Get-Content -LiteralPath (Join-Path $taskQa "$taskMode-host.txt") -Raw
 if(!$taskHost.Contains('Executing as a self-contained app')){throw 'Payload requires an external runtime'}
 if($taskHost.Contains('Detected Single-File app bundle')){throw 'Payload must not perform a second hidden extraction'}
 $taskBootstrap=Get-Content -LiteralPath (Join-Path $taskQa "$taskMode-bootstrap.txt") -Raw
 if($taskMode -eq 'cold') {
  foreach($taskEvent in @('progress-visible 1','extract-start 0','extract-complete 10000','main-window 1')){if(!$taskBootstrap.Contains($taskEvent)){throw "Missing event: $taskEvent"}}
  $taskValues=@([regex]::Matches($taskBootstrap,'(?m)^progress (\d+)') | ForEach-Object {[int]$_.Groups[1].Value})
  if($taskValues.Count -lt 2 -or $taskValues[-1] -ne 10000 -or !($taskValues | Where-Object {$_ -gt 0 -and $_ -lt 10000})){throw 'No intermediate extraction progress'}
  for($taskIndex=1;$taskIndex -lt $taskValues.Count;$taskIndex++){if($taskValues[$taskIndex] -lt $taskValues[$taskIndex-1]){throw 'Progress went backwards'}}
 }elseif(!$taskBootstrap.Contains('cache-hit 1') -or $taskBootstrap.Contains('extract-start') -or $taskBootstrap.Contains('progress-visible')){throw 'Warm startup extracted files or displayed extraction UI'}
}
foreach($taskDependency in @('coreclr.dll','System.Private.CoreLib.dll','Microsoft.UI.Xaml.dll','KubSMTP.App.pri')) {
 if(!(Get-ChildItem -LiteralPath $taskExtract -Filter $taskDependency -File -Recurse)){throw "Missing dependency: $taskDependency"}
}
$taskReport=[ordered]@{Version=$taskSource.VersionInfo.FileVersion;Executable=$taskSource.FullName;Bytes=$taskSource.Length;OnlyExeCopied=$true;SelfContainedHostConfirmed=$true;NativeProgressVerified=$true;WarmCacheVerified=$true;RenderedLightAndDark=$true;Diagnostics=$taskQa}
$taskReportPath=Join-Path $taskRoot ("artifacts/portable-check-v"+$taskSource.VersionInfo.FileVersion+'.json')
$taskReport | ConvertTo-Json | Set-Content -LiteralPath $taskReportPath
Write-Output 'PASS Native extraction progress, warm cache, standalone runtime and both UI themes.'
Write-Output "Report: $taskReportPath"
