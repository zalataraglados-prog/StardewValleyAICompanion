function Read-RuntimeLogicalJsonArtifact {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [string] $CoreAssemblyPath = "",
        [switch] $Raw
    )
    $fullPath = [IO.Path]::GetFullPath($Path)
    $text = Get-Content -LiteralPath $fullPath -Raw -Encoding utf8
    $stored = $text | ConvertFrom-Json
    if ([string]$stored.schema_version -ne "stardewai.content_addressed_json.v1") {
        if ($Raw) { return $text }
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
    $logicalText = [StardewAI.Core.Infrastructure.ContentAddressedJsonArtifactReader]::ReadAllText($fullPath)
    if ($Raw) { return $logicalText }
    return ConvertFrom-Json -InputObject $logicalText
}
