param(
    [string]$UnityPath = ""
)

$ErrorActionPreference = "Stop"
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $workspaceRoot "UnderwaterGliderTwin"
$resultsDirectory = Join-Path $workspaceRoot "TestResults"
$logPath = Join-Path $resultsDirectory "WindowsBuild.log"

if (-not (Test-Path -LiteralPath $projectPath -PathType Container)) {
    throw "Unity project was not found: $projectPath"
}

. "$PSScriptRoot\ResolveUnityEditor.ps1"
$UnityPath = Resolve-UnityEditorPath -UnityPath $UnityPath -ProjectPath $projectPath
Initialize-UnityPackageManagerEnvironment

New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
Remove-Item -Force -ErrorAction SilentlyContinue $logPath

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
    "-quit"
    "-projectPath"
    $projectPath
    "-executeMethod"
    "UnderwaterGliderTwin.Editor.BuildWindows.Build"
    "-logFile"
    $logPath
)

$unityProcess = Start-Process -FilePath $UnityPath -ArgumentList (ConvertTo-UnityArgumentText $unityArguments) -PassThru -WindowStyle Hidden
$timeoutSeconds = 600
if (-not $unityProcess.WaitForExit($timeoutSeconds * 1000)) {
    try {
        Stop-Process -Id $unityProcess.Id -Force
    }
    finally {
        $unityProcess.WaitForExit()
    }
    throw "Unity did not finish the Windows build within $timeoutSeconds seconds. See $logPath"
}

$unityExitCode = $unityProcess.ExitCode
$buildSucceeded = Test-Path -LiteralPath $logPath -PathType Leaf
if ($buildSucceeded) {
    $buildSucceeded = Select-String -LiteralPath $logPath -SimpleMatch -Quiet "Build Finished, Result: Success"
}

if (-not $buildSucceeded) {
    throw "Windows build did not report success. See $logPath"
}

if ($unityExitCode -ne 0) {
    Write-Warning "Unity returned exit code $unityExitCode after reporting a successful build. See $logPath"
}

Write-Host "Windows build succeeded. See $logPath"
exit 0
