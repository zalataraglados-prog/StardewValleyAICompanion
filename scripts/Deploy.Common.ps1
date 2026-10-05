function Assert-StardewAIBuildOutputFresh {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $OutputPath,
        [Parameter(Mandatory = $true)]
        [string[]] $InputRoots,
        [string[]] $InputFiles = @()
    )

    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "Build output not found: $OutputPath"
    }

    $inputs = [Collections.Generic.List[IO.FileInfo]]::new()
    foreach ($root in $InputRoots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            throw "Build input root not found: $root"
        }

        Get-ChildItem -LiteralPath $root -Recurse -File |
            Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
                $_.Extension -in @('.cs', '.csproj', '.props', '.targets', '.json')
            } |
            ForEach-Object { $inputs.Add($_) }
    }

    foreach ($path in $InputFiles) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $inputs.Add((Get-Item -LiteralPath $path))
        }
    }

    $latestInput = $inputs |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $latestInput) {
        throw "No build inputs were found for freshness validation."
    }

    $output = Get-Item -LiteralPath $OutputPath
    if ($latestInput.LastWriteTimeUtc -gt $output.LastWriteTimeUtc) {
        throw (
            "NoBuild refused because build output is stale: output={0}; " +
            "latest_input={1}" -f $OutputPath, $latestInput.FullName)
    }
}

function Copy-StardewAIVerifiedFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourcePath,
        [Parameter(Mandatory = $true)]
        [string] $DestinationPath
    )

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
    $sourceHash = (Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash
    $destinationHash = (
        Get-FileHash -LiteralPath $DestinationPath -Algorithm SHA256).Hash
    if ($sourceHash -ne $destinationHash) {
        throw (
            "Deployed file hash mismatch: source={0}; destination={1}" -f
            $SourcePath, $DestinationPath)
    }

    return $sourceHash
}
