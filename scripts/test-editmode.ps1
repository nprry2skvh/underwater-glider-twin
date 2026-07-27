param(
    [string]$UnityPath = ""
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

New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
Remove-Item -Force -ErrorAction SilentlyContinue $resultPath, $logPath

$unityArguments = @(
    "-batchmode"
    "-nographics"
    "-projectPath `"$projectPath`""
    "-runTests"
    "-testPlatform EditMode"
    "-testResults `"$resultPath`""
    "-logFile `"$logPath`""
)
$unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $unityArguments -PassThru
$deadline = [DateTime]::UtcNow.AddMinutes(3)

try {
    while (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        if ([DateTime]::UtcNow -ge $deadline) {
            throw "Unity did not write a test result file within three minutes. See $logPath"
        }

        Start-Sleep -Milliseconds 250
    }

    [xml]$results = Get-Content -Raw -LiteralPath $resultPath
    $run = $results.'test-run'
    if ($run.result -ne "Passed" -or [int]$run.failed -ne 0) {
        throw "EditMode tests did not pass. See $resultPath"
    }

    Write-Host "EditMode tests passed: $($run.passed)/$($run.total)"
    Write-Host "Results: $resultPath"
}
finally {
    if ($unityProcess -and -not $unityProcess.HasExited) {
        Stop-Process -Id $unityProcess.Id -Force
        $unityProcess.WaitForExit()
    }
}
