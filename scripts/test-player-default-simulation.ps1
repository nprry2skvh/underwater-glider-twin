param(
    [int]$TimeoutSeconds = 30,
    [switch]$ExpectInvalidCsvError
)

$ErrorActionPreference = "Stop"
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Join-Path $workspaceRoot "UnderwaterGliderTwin"
$exePath = Join-Path $workspaceRoot "Builds/UnderwaterGliderTwin/UnderwaterGliderTwin.exe"
$resultsRoot = Join-Path $workspaceRoot "TestResults"
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    throw "Build the Windows Player before running this smoke test: $exePath"
}

$settings = Get-Content -LiteralPath (Join-Path $projectRoot "ProjectSettings/ProjectSettings.asset") -Raw
$company = [regex]::Match($settings, '(?m)^  companyName: (.+)$').Groups[1].Value.Trim()
$product = [regex]::Match($settings, '(?m)^  productName: (.+)$').Groups[1].Value.Trim()
if (-not $company -or -not $product) {
    throw "Could not resolve the Player log directory from ProjectSettings.asset."
}
$loadLog = Join-Path $env:USERPROFILE "AppData/LocalLow/$company/$product/Logs/load.log"
$previousLineCount = if (Test-Path -LiteralPath $loadLog) { @(Get-Content -LiteralPath $loadLog).Count } else { 0 }

New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null
$runId = [guid]::NewGuid().ToString("N")
$screenshotPath = Join-Path $resultsRoot "DefaultSimulation-$runId.png"
$playerLog = Join-Path $resultsRoot "DefaultSimulation-$runId.log"
$arguments = '-logFile "' + $playerLog + '" --screenshot "' + $screenshotPath + '" --quit-after-screenshot'
if ($ExpectInvalidCsvError) {
    $arguments += ' --csv='
}
$process = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $exePath) -PassThru -WindowStyle Hidden
if ($ExpectInvalidCsvError) {
    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(5, $TimeoutSeconds))
    $startupError = $false
    $csvArgumentError = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $loadLog -PathType Leaf) {
            $newLines = @(Get-Content -LiteralPath $loadLog | Select-Object -Skip $previousLineCount)
            $startupError = [bool]($newLines | Select-String -SimpleMatch "Startup failed:" -Quiet)
            $csvArgumentError = [bool]($newLines | Select-String -SimpleMatch "--csv requires a file path." -Quiet)
            if ($startupError -and $csvArgumentError) { break }
        }
        if ($process.WaitForExit(250)) { break }
    }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (-not $startupError -or -not $csvArgumentError -or (Test-Path -LiteralPath $screenshotPath -PathType Leaf)) {
        throw "Invalid explicit CSV argument was silently accepted. See $loadLog and $playerLog"
    }
    Write-Host "Invalid CSV Player smoke passed. Startup error recorded; no simulation fallback."
    return
}
if (-not $process.WaitForExit([Math]::Max(5, $TimeoutSeconds) * 1000)) {
    Stop-Process -Id $process.Id -Force
    throw "Player did not finish without a data-source argument. See $playerLog"
}
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $screenshotPath -PathType Leaf)) {
    throw "Player failed before screenshot capture. Exit=$($process.ExitCode). See $playerLog"
}
if (-not (Test-Path -LiteralPath $loadLog -PathType Leaf)) {
    throw "Player did not write a load log: $loadLog"
}
$newLines = @(Get-Content -LiteralPath $loadLog | Select-Object -Skip $previousLineCount)
if (-not ($newLines | Select-String -SimpleMatch "Loading telemetry from simulation profile." -Quiet)) {
    throw "Player did not select simulation without a data-source argument. See $loadLog and $playerLog"
}

Write-Host "Default-simulation Player smoke passed. Exit=0; source=simulation."
Write-Host "Log: $playerLog"
