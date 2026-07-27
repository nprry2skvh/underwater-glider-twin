function Get-ProjectUnityVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $versionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        return $null
    }

    $match = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
    if ($match) {
        return $match.Matches[0].Groups[1].Value.Trim()
    }

    return $null
}

function Resolve-UnityEditorPath {
    param(
        [string]$UnityPath,
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    if ($UnityPath -and (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
        return (Resolve-Path -LiteralPath $UnityPath).Path
    }

    $projectVersion = Get-ProjectUnityVersion -ProjectPath $ProjectPath
    $candidates = New-Object System.Collections.Generic.List[string]

    if ($env:UNITY_EDITOR_PATH) {
        $candidates.Add($env:UNITY_EDITOR_PATH)
    }

    if ($env:UNITY_PATH) {
        $candidates.Add($env:UNITY_PATH)
    }

    $editorRoots = @(
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor"),
        "D:\Program Files\Unity\Hub\Editor",
        "E:\Program Files\Unity\Hub\Editor"
    )

    foreach ($root in $editorRoots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            continue
        }

        if ($projectVersion) {
            $candidates.Add((Join-Path $root "$projectVersion\Editor\Unity.exe"))
        }

        Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { $candidates.Add((Join-Path $_.FullName "Editor\Unity.exe")) }
    }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $versionText = if ($projectVersion) { " $projectVersion" } else { "" }
    throw "Unity editor$versionText was not found. Install it with Unity Hub or pass -UnityPath 'C:\Path\To\Unity.exe'."
}
