$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

$expected = @'
{
  "schema_version": "strategy_ledger.v1",
  "created_at": "2026-10-18T14:07:08.1234567+08:00",
  "note": "escaped \"quote\", comma, and } bracket",
  "nested": [{"value": "a,b", "enabled": true}],
  "nullable": null
}
'@
$raw = '{"before":{"ignored":true},"ledger":' + $expected +
    ',"after":[1,2,{"text":"still ignored"}]}'
$outputPath = [IO.Path]::GetTempFileName()

try {
    Write-RawJsonProperty -Path $outputPath -RawJson $raw `
        -PropertyName "ledger"
    $actual = [IO.File]::ReadAllText($outputPath, [Text.Encoding]::UTF8)
    if (-not [string]::Equals(
            $expected,
            $actual,
            [StringComparison]::Ordinal)) {
        throw "Raw JSON property bytes changed during extraction."
    }

    try {
        Get-RuntimeRawJsonProperty -RawJson $raw `
            -PropertyName "missing" | Out-Null
        throw "Missing JSON property was accepted."
    }
    catch {
        if ($_.Exception.Message -eq "Missing JSON property was accepted.") {
            throw
        }
    }

    try {
        Get-RuntimeRawJsonProperty `
            -RawJson '{"ledger":{},"ledger":[]}' `
            -PropertyName "ledger" | Out-Null
        throw "Duplicate JSON property was accepted."
    }
    catch {
        if ($_.Exception.Message -eq "Duplicate JSON property was accepted.") {
            throw
        }
    }
}
finally {
    [IO.File]::Delete($outputPath)
}

Write-Output "runtime_raw_json_extraction_pass"
