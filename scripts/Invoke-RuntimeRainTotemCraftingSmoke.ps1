param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot =
        "E:\StardewValleyAICompanion-runtime",
    [string] $SavesPath = "",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-rain-totem-crafting-smoke-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [string] $OutputDirectory =
        "artifacts\runtime-rain-totem-crafting-smoke",
    [int] $StartupTimeoutSeconds = 120
)

$arguments = @{
    ProjectRoot = $ProjectRoot
    RuntimeRoot = $RuntimeRoot
    SavesPath = $SavesPath
    SaveSlot = $SaveSlot
    RunId = $RunId
    OutputDirectory = $OutputDirectory
    StartupTimeoutSeconds = $StartupTimeoutSeconds
    RecipeName = "Rain Totem"
    SnapshotProfile = "full"
    SnapshotFieldName = "rain_totem_crafting"
    SetupOptionId = "debug.setup_rain_totem_crafting_target"
    CraftOptionId = "executor.craft_rain_totem"
    ExpectedPrimitiveKind = "craft_rain_totem"
    QueueId = "runtime-rain-totem-crafting-smoke"
}
& (Join-Path $PSScriptRoot `
    "Invoke-RuntimeStorageCraftingSmoke.ps1") @arguments
