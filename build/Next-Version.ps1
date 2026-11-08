$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskVersionPath = Join-Path $taskRoot 'version.json'
$taskVersion = Get-Content -LiteralPath $taskVersionPath -Raw | ConvertFrom-Json
if ($taskVersion.patch -ge 100) { $taskVersion.minor++; $taskVersion.patch = 0 } else { $taskVersion.patch++ }
$taskNumber = '{0}.{1}.{2}' -f $taskVersion.major,$taskVersion.minor,$taskVersion.patch
$taskVersion | ConvertTo-Json | Set-Content -LiteralPath $taskVersionPath
$taskPropsPath = Join-Path $taskRoot 'Directory.Build.props'
$taskProps = [xml](Get-Content -LiteralPath $taskPropsPath -Raw)
$taskProps.Project.PropertyGroup.Version = $taskNumber
$taskProps.Project.PropertyGroup.AssemblyVersion = "$taskNumber.0"
$taskProps.Project.PropertyGroup.FileVersion = "$taskNumber.0"
$taskProps.Save($taskPropsPath)
Write-Output "v.$taskNumber"
