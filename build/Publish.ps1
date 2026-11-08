param(
 [string]$Dotnet = 'dotnet',
 [string]$Zig = (Join-Path $env:LOCALAPPDATA 'KubSMTP-BuildTools/zig-x86_64-windows-0.17.1/zig.exe'),
 [string]$Python = (Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskVersion = Get-Content -LiteralPath (Join-Path $taskRoot 'version.json') -Raw | ConvertFrom-Json
$taskNumber = '{0}.{1}.{2}' -f $taskVersion.major,$taskVersion.minor,$taskVersion.patch
# Publish to a fresh directory so old framework-dependent files cannot mix in.
$taskOutput = Join-Path $taskRoot ('artifacts/.publish-' + [Guid]::NewGuid().ToString('N'))
$taskPayload = Join-Path $taskOutput 'payload'
& $Dotnet publish (Join-Path $taskRoot 'src/KubSMTP.App/KubSMTP.App.csproj') -c Release -r win-x64 --self-contained true -p:PortablePayload=true -p:PublishSingleFile=false -p:DebugType=embedded -o $taskPayload
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$taskRuntime = Get-Content -LiteralPath (Join-Path $taskPayload 'KubSMTP.App.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($taskRuntime.runtimeOptions.framework -or !$taskRuntime.runtimeOptions.includedFrameworks) { throw 'Payload requires an external .NET runtime' }
$taskNative = Join-Path $taskOutput 'native'
& (Join-Path $PSScriptRoot 'Build-Bootstrap.ps1') -Zig $Zig -OutputDirectory $taskNative -Version $taskNumber
$taskPacked = Join-Path $taskOutput 'KubSMTP.App.exe'
& $Python (Join-Path $PSScriptRoot 'Pack-Portable.py') $taskPayload (Join-Path $taskNative 'bootstrap.exe') $taskPacked
if ($LASTEXITCODE -ne 0) { throw 'Portable packaging failed' }
$taskPortable = Join-Path $taskRoot 'artifacts/KubSMTP.App.exe'
Copy-Item -LiteralPath $taskPacked -Destination $taskPortable -Force
foreach ($taskFolder in @('KubSMTP-win-x64',"KubSMTP-win-x64-v$taskNumber")) {
 $taskDestination = Join-Path $taskRoot "artifacts/$taskFolder"
 New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
 Copy-Item -LiteralPath $taskPortable -Destination (Join-Path $taskDestination 'KubSMTP.App.exe') -Force
}
# Only remove this invocation's generated staging directory, inside artifacts.
$taskArtifacts = [IO.Path]::GetFullPath((Join-Path $taskRoot 'artifacts'))
$taskResolvedOutput = [IO.Path]::GetFullPath($taskOutput)
if ([IO.Path]::GetDirectoryName($taskResolvedOutput) -ne $taskArtifacts -or [IO.Path]::GetFileName($taskResolvedOutput) -notlike '.publish-*') {
 throw 'Refusing to remove staging directory outside artifacts.'
}
Remove-Item -LiteralPath $taskResolvedOutput -Recurse -Force
Write-Output "Portable v.$taskNumber : $taskPortable"
