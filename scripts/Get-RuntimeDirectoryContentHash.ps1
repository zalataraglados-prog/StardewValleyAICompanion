[CmdletBinding()]
param([Parameter(Mandatory)] [string] $Path)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

Get-DirectoryContentHash -Path $Path
