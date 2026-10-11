[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $BaseRef = "",
    [string] $HeadRef = "HEAD",
    [string[]] $ChangedFile = @(),
    [string] $ChangedFilesPath = "",
    [string] $Output = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
} else {
    $ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
}
if (-not (Test-Path -LiteralPath $ProjectRoot -PathType Container)) {
    throw "Project root is missing: $ProjectRoot"
}

. (Join-Path $PSScriptRoot "lib\ValidationImpactAdvisory.ps1")

$selection = Get-ValidationImpactChangedFiles `
    -ProjectRoot $ProjectRoot `
    -BaseRef $BaseRef `
    -HeadRef $HeadRef `
    -ChangedFile $ChangedFile `
    -ChangedFilesPath $ChangedFilesPath
$report = New-ValidationImpactAdvisory `
    -ChangedFiles $selection.Files `
    -ChangeSource $selection.Source `
    -BaseRef $selection.BaseRef `
    -HeadRef $selection.HeadRef
$json = $report | ConvertTo-Json -Depth 12

if (-not [string]::IsNullOrWhiteSpace($Output)) {
    $outputPath = if ([IO.Path]::IsPathRooted($Output)) {
        [IO.Path]::GetFullPath($Output)
    } else {
        [IO.Path]::GetFullPath((Join-Path $ProjectRoot $Output))
    }
    $directory = Split-Path -Parent $outputPath
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $temporary = $outputPath + ".tmp-" + [Guid]::NewGuid().ToString("N")
    try {
        [IO.File]::WriteAllText(
            $temporary,
            $json + [Environment]::NewLine,
            [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporary -Destination $outputPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporary -PathType Leaf) {
            [IO.File]::Delete($temporary)
        }
    }
}

$json
