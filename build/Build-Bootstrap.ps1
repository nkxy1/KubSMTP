param([string]$Zig,[string]$OutputDirectory,[string]$Version)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskIcon=(Join-Path $taskRoot 'assets/KubSMTP.ico').Replace('\','\\')
$taskManifest=(Join-Path $taskRoot 'src/KubSMTP.Bootstrap/bootstrap.manifest').Replace('\','\\')
$taskVersionTuple=$Version.Replace('.',',')+',0'
$taskRc=@"
1 ICON "$taskIcon"
1 24 "$taskManifest"
1 VERSIONINFO
 FILEVERSION $taskVersionTuple
 PRODUCTVERSION $taskVersionTuple
 FILEOS 0x40004
 FILETYPE 1
BEGIN
 BLOCK "StringFileInfo"
 BEGIN
  BLOCK "040904B0"
  BEGIN
   VALUE "FileDescription", "KubSMTP portable startup"
   VALUE "FileVersion", "$Version.0"
   VALUE "ProductName", "KubSMTP"
   VALUE "ProductVersion", "$Version.0"
  END
 END
 BLOCK "VarFileInfo"
 BEGIN
  VALUE "Translation", 0x409, 1200
 END
END
"@
$taskRcPath=Join-Path $OutputDirectory 'bootstrap.rc'
$taskResPath=Join-Path $OutputDirectory 'bootstrap.res'
Set-Content -LiteralPath $taskRcPath -Value $taskRc -Encoding utf8
& $Zig rc /fo $taskResPath $taskRcPath
if($LASTEXITCODE -ne 0){throw 'Native resource compilation failed'}
& $Zig cc -target x86_64-windows-gnu -O2 -s -municode '-Wl,--subsystem,windows' -DUNICODE -D_UNICODE (Join-Path $taskRoot 'src/KubSMTP.Bootstrap/bootstrap.c') $taskResPath -lcomctl32 -lgdi32 -lshell32 -lole32 -lbcrypt -lcabinet -luuid -static -o (Join-Path $OutputDirectory 'bootstrap.exe')
if($LASTEXITCODE -ne 0){throw 'Native bootstrap compilation failed'}
