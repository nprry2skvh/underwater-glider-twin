param(
    [switch]$All,
    [string]$Filter = "",
    [string]$UnityPath = "",
    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = "Stop"
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $workspaceRoot "UnderwaterGliderTwin"
$resultsDirectory = Join-Path $workspaceRoot "TestResults"
$resultPath = Join-Path $resultsDirectory "PlayModeResults.xml"
$logPath = Join-Path $resultsDirectory "PlayMode.log"
. "$PSScriptRoot\ResolveUnityEditor.ps1"
$UnityPath = Resolve-UnityEditorPath -UnityPath $UnityPath -ProjectPath $projectPath
Initialize-UnityPackageManagerEnvironment
New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
Remove-Item -Force -ErrorAction SilentlyContinue $resultPath, $logPath
$arguments = @("-batchmode", "-projectPath", $projectPath, "-runTests", "-testPlatform", "PlayMode", "-testResults", $resultPath, "-logFile", $logPath)
if (-not $All -and -not [string]::IsNullOrWhiteSpace($Filter)) { $arguments += @("-testFilter", $Filter) }
function ConvertTo-UnityArgumentText { param([string[]]$Values) return [string]::Join(" ", ($Values | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\\"') + '"' } else { $_ } })) }
$process = Start-Process -FilePath $UnityPath -ArgumentList (ConvertTo-UnityArgumentText $arguments) -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit([Math]::Max(60, $TimeoutMinutes * 60) * 1000)) { Stop-Process -Id $process.Id -Force; throw "Unity PlayMode tests timed out. See $logPath" }
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw "Unity did not write PlayMode results. See $logPath" }
[xml]$results = Get-Content -Raw -LiteralPath $resultPath
$run = $results.'test-run'
Write-Host "PlayMode result: $($run.result) passed=$($run.passed) failed=$($run.failed) total=$($run.total)"
if ($run.result -ne "Passed" -or [int]$run.failed -ne 0) { throw "PlayMode tests did not pass. See $resultPath" }
