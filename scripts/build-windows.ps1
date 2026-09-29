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
if ($unityExitCode -ne 0) {
    throw "Unity Windows build failed with exit code $unityExitCode. See $logPath"
}

$buildSucceeded = Test-Path -LiteralPath $logPath -PathType Leaf
if ($buildSucceeded) {
    $buildSucceeded = Select-String -LiteralPath $logPath -SimpleMatch -Quiet "Build Finished, Result: Success"
}

if (-not $buildSucceeded) {
    throw "Windows build did not report success. See $logPath"
}

$buildOutputDirectory = Join-Path $workspaceRoot "Builds\UnderwaterGliderTwin"
$requiredBuildPaths = @(
    (Join-Path $buildOutputDirectory "UnderwaterGliderTwin.exe")
    (Join-Path $buildOutputDirectory "UnderwaterGliderTwin_Data")
    (Join-Path $buildOutputDirectory "UnityPlayer.dll")
)
foreach ($requiredBuildPath in $requiredBuildPaths) {
    if (-not (Test-Path -LiteralPath $requiredBuildPath)) {
        throw "Windows build output is incomplete. Missing: $requiredBuildPath"
    }
}

Write-Host "Windows build succeeded. See $logPath"
exit 0
