param(
    [Parameter(Mandatory=$true)][string]$Filter,
    [string]$UnityPath = "",
    [int]$TimeoutMinutes = 15
)
& "$PSScriptRoot\test-playmode.ps1" -Filter $Filter -UnityPath $UnityPath -TimeoutMinutes $TimeoutMinutes
