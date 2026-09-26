param(
    [string]$UnityPath = "",
    [int]$TimeoutMinutes = 15
)

$ErrorActionPreference = "Stop"
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $workspaceRoot "UnderwaterGliderTwin"
$resultsDirectory = Join-Path $workspaceRoot "TestResults"
$resultPath = Join-Path $resultsDirectory "EditModeResults.xml"
$logPath = Join-Path $resultsDirectory "EditMode.log"

if (-not (Test-Path -LiteralPath $projectPath -PathType Container)) {
    throw "Unity project was not found: $projectPath"
}

. "$PSScriptRoot\ResolveUnityEditor.ps1"
$UnityPath = Resolve-UnityEditorPath -UnityPath $UnityPath -ProjectPath $projectPath
Initialize-UnityPackageManagerEnvironment

New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
Remove-Item -Force -ErrorAction SilentlyContinue $resultPath, $logPath

function ConvertTo-UnityArgumentText {
    param([string[]]$Arguments)

    $quotedArguments = foreach ($argument in $Arguments) {
        if ($argument -match '[\s"]') {
            '"' + ($argument -replace '"', '\"') + '"'
        }
        else {
            $argument
        }
    }

    [string]::Join(" ", $quotedArguments)
}

$unityArguments = @(
    "-batchmode"
    "-nographics"
    "-projectPath"
    $projectPath
    "-runTests"
    "-testPlatform"
    "EditMode"
    "-testResults"
    $resultPath
    "-logFile"
    $logPath
)

$unityProcess = Start-Process -FilePath $UnityPath -ArgumentList (ConvertTo-UnityArgumentText $unityArguments) -PassThru -WindowStyle Hidden
$timeoutSeconds = [Math]::Max(60, $TimeoutMinutes * 60)
if (-not $unityProcess.WaitForExit($timeoutSeconds * 1000)) {
    try {
        Stop-Process -Id $unityProcess.Id -Force
    }
    finally {
        $unityProcess.WaitForExit()
    }
    throw "Unity did not write a test result file within $TimeoutMinutes minutes. See $logPath"
}

$unityExitCode = $unityProcess.ExitCode

if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    throw "Unity did not write a test result file. Exit code: $unityExitCode. See $logPath"
}

[xml]$results = Get-Content -Raw -LiteralPath $resultPath
$run = $results.'test-run'
if ($run.result -ne "Passed" -or [int]$run.failed -ne 0) {
    throw "EditMode tests did not pass. See $resultPath"
}

if ($unityExitCode -ne 0) {
    Write-Warning "Unity returned exit code $unityExitCode after writing passed test results. See $logPath"
}

Write-Host "EditMode tests passed: $($run.passed)/$($run.total)"
Write-Host "Results: $resultPath"
