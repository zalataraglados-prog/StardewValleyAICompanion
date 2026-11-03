function Read-RuntimeLogicalJsonArtifact {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [string] $CoreAssemblyPath = ""
    )
    $fullPath = [IO.Path]::GetFullPath($Path)
    $stored = Get-Content -LiteralPath $fullPath -Raw -Encoding utf8 | ConvertFrom-Json
    if ([string]$stored.schema_version -ne "stardewai.content_addressed_json.v1") {
        return $stored
    }
    $readerType = "StardewAI.Core.Infrastructure.ContentAddressedJsonArtifactReader" -as [type]
    if ($null -eq $readerType) {
        if ([string]::IsNullOrWhiteSpace($CoreAssemblyPath) -or
            -not (Test-Path -LiteralPath $CoreAssemblyPath -PathType Leaf)) {
            throw "Content-addressed JSON reader assembly is missing: $CoreAssemblyPath"
        }
        Add-Type -Path $CoreAssemblyPath
    }
    $raw = [StardewAI.Core.Infrastructure.ContentAddressedJsonArtifactReader]::ReadAllText($fullPath)
    return ConvertFrom-Json -InputObject $raw
}
