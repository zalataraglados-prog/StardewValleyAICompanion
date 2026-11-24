param([string]$RunId = ('island-suffix-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
$ErrorActionPreference = 'Stop'
$lab = Split-Path -Parent $PSScriptRoot
$source = 'D:\Recovered-From-OldPC\I\StardewAITraining\golden-walnut-source-schedule-v102\validation-saves\ginger-island-unlocked-baseline-20261103\CrabPotAudit_442159967'
$hashes = @{
    CrabPotAudit_442159967 = '7F34A5731208788F37EF2D1F76B64B890466293878F876ABCA6FE4A05A052CB0'
    SaveGameInfo = '0F2716DE726B87A8EC9AAE6B89E1713C6207BD24EA119E8DC188DFDF2C0620FC'
}
foreach ($file in $hashes.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $source $file)).Hash -cne $hashes[$file]) { throw 'Original unlocked fixture changed.' }
}
$run = New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot $RunId)
$saveRoot = New-Item -ItemType Directory -Path (Join-Path $run.FullName 'saves')
$slot = New-Item -ItemType Directory -Path (Join-Path $saveRoot.FullName 'CrabPotAudit_442159967')
foreach ($file in $hashes.Keys) {
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $slot.FullName $file)
    if ((Get-FileHash -LiteralPath (Join-Path $slot.FullName $file)).Hash -cne $hashes[$file]) { throw 'Isolated fixture copy differs.' }
}
$template = Join-Path $lab 'Spring15本机续跑\runs\r2-spring26-harvest-retention\summer10-native-cold-readback-20261124\runtime-settings.json'
$settings = Get-Content -LiteralPath $template -Raw | ConvertFrom-Json -AsHashtable
foreach ($key in 'STARDEWAI_TEST_SAVES','STARDEWAI_SAVE_ISOLATION_PATH','STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT') { $settings[$key] = $saveRoot.FullName }
foreach ($key in 'STARDEWAI_TRAINING_RUN_ID','STARDEWAI_PRODUCT_RUN_ID') { $settings[$key] = $RunId }
$settings.STARDEWAI_TEST_SLOT = 'CrabPotAudit_442159967'
$settings.STARDEWAI_TRAINING_OUTPUT_DIR = Join-Path $run.FullName 'training'
$settings.STARDEWAI_STRATEGY_LEDGER_DIR = Join-Path $run.FullName 'strategy-ledgers'
$settings.STARDEWAI_PRODUCT_JOURNAL_ROOT = Join-Path $run.FullName 'product-journal'
$settings | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run.FullName 'runtime-settings.json') -Encoding utf8
[ordered]@{
    run_id=$RunId; save_root=$saveRoot.FullName; slot='CrabPotAudit_442159967'
    expected_save_id='自动化'; expected_player_id='-8577884337094229036'; expected_total_day=472
    original_source=$source; original_hashes=$hashes
    scope='Local post-unlock native fishing validation; existing island capability evidence reused.'
    formal_training_authorized=$false; day_zero_claim=$false
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run.FullName 'fixture.json') -Encoding utf8
$run.FullName
