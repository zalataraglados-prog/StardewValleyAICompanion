param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot =
        "E:\StardewValleyAICompanion-runtime",
    [string] $SavesPath = "",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-crab-pot-crafting-smoke-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [string] $OutputDirectory =
        "artifacts\runtime-crab-pot-crafting-smoke",
    [int] $StartupTimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

$arguments = @{
    ProjectRoot = $ProjectRoot
    RuntimeRoot = $RuntimeRoot
    SavesPath = $SavesPath
    SaveSlot = $SaveSlot
    RunId = $RunId
    OutputDirectory = $OutputDirectory
    StartupTimeoutSeconds = $StartupTimeoutSeconds
    RecipeName = "Crab Pot"
    SnapshotProfile = "full"
    SnapshotFieldName = "crab_pot_crafting"
    SetupOptionId = "debug.setup_crab_pot_crafting_target"
    CraftOptionId = "executor.craft_crab_pot"
    ExpectedPrimitiveKind = "craft_crab_pot"
    QueueId = "runtime-crab-pot-crafting-smoke"
}
& (Join-Path $PSScriptRoot "Invoke-RuntimeStorageCraftingSmoke.ps1") @arguments
