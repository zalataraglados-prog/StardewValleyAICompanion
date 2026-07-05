# Phase 1A-3 Manual SMAPI Runtime Acceptance

Scope: transparent runtime validation loop and manual SMAPI acceptance tooling only. This document does not expand Farm, NPC, Map, Shop, executor, automation, pathing, buying, selling, or save-editing behavior.

## Tool

Use `scripts/Invoke-SmapiRuntimeAcceptance.ps1` from the repo root after starting Stardew Valley through SMAPI with `StardewAI.TransparentBridge` loaded.

The script is a background HTTP collector only. It does not launch SMAPI, start Stardew Valley, focus windows, send keyboard or mouse input, or mutate game state. For isolated-copy validation, start SMAPI yourself from the isolated Stardew Valley directory, then pass that directory to the script so the artifact metadata records which copy produced the Bridge responses.

Bridge-only capture:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Invoke-SmapiRuntimeAcceptance.ps1
```

Bridge-only capture from an isolated Stardew copy using explicit Bridge URL and artifact directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Invoke-SmapiRuntimeAcceptance.ps1 -IsolatedStardewDirectory "I:\StardewValleyAICompanion-runtime\Stardew Valley" -BridgeUrl http://127.0.0.1:8765 -ArtifactsDirectory artifacts\smapi-runtime-acceptance
```

Bridge capture plus Backend ingest comparison:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Invoke-SmapiRuntimeAcceptance.ps1 -BridgeUrl http://127.0.0.1:8765 -BackendUrl http://127.0.0.1:5000 -IngestBackend
```

The script writes timestamped artifacts under `artifacts/smapi-runtime-acceptance/`:

- `bridge-snapshot.json`
- `bridge-events.json`
- `bridge-capabilities.json`
- `backend-summary.json`
- `manual-checklist.json`

The script only uses HTTP GET against the Bridge. `bridge-events.json` captures the HTTP event stream envelope from `GET /api/v1/events?limit=200`, including the current snapshot hash, latest event sequence, latest event hash, chain status, and the event page. With `-IngestBackend`, it POSTs the captured read-only payloads to Backend ingest endpoints and reads Backend comparison endpoints.

`manual-checklist.json` records `isolated_stardew_directory`, `bridge_base_url`, `backend_base_url`, and the run directory. `-OutputDirectory` and its alias `-ArtifactsDirectory` select the artifact root. `-BridgeBaseUrl`/`-BridgeUrl` and `-BackendBaseUrl`/`-BackendUrl` select already-running services.

## Manual Acceptance Steps

1. Start Backend if Backend comparison is needed.
2. Start Stardew Valley through SMAPI on Windows. If validating an isolated copy, start SMAPI from that isolated Stardew Valley directory.
3. Load a test save and wait until the player can see the world.
4. Run the script and keep the run directory printed by the script.
5. Compare the visible save/farm/player identity to `identity` and `environment` in `bridge-snapshot.json`.
6. Compare visible clock/date/weather to `state.time` and snapshot `in_game_time`.
7. Compare visible player location, tile movement, facing, money, health, stamina, selected tool, active menu, and inventory to `state.player`.
8. Move within the same location, run the script again, and compare `state_hash`, player tile fields, and event tail.
9. Warp to another location, run the script again, and verify a `LocationChanged` event with `changed_fields`.
10. Change inventory, run the script again, and verify an `InventoryChanged` event with `changed_fields`.
11. Open and close menus, run the script again, and verify a `MenuChanged` event with `changed_fields`.
12. Wait for time to advance, run the script again, and verify a `TimeChanged` event with `changed_fields`.
13. If `-IngestBackend` was used, confirm `backend-summary.hash_match_after_ingest` is `true` and Backend `/api/v1/sync` reports the same latest hash.
14. Stop Backend and confirm the game keeps running.
15. Restart Backend, run with `-IngestBackend` again, and compare reconnect ingest behavior.

## Pass Criteria

- All captured snapshot fields are field envelopes with `value`, `status`, `source`, `adapter`, `read_at_tick`, and `confidence`.
- Non-readable statuses do not carry default values.
- `bridge-capabilities.json` keeps `can_write_game_state=false` and `can_execute_commands=false`.
- Bridge events are returned through `event_stream.v1`, with `latest_snapshot_hash` matching the captured snapshot `state_hash`.
- Events use `event.v1` and include `event_sequence`, `state_hash_before`, `state_hash_after`, `previous_event_hash`, `event_hash`, and `changed_fields`.
- Event pages report `chain_status=ok`; adjacent events in the page link `previous_event_hash` to the prior `event_hash`.
- Backend comparison, when enabled, preserves the Bridge `state_hash`.
- Any mismatch is recorded with the run directory, visible game observation, JSON path, and expected/actual values.

## Slice Allowlist

Allowed source files for this Phase 1A-3 code-preparation slice:

- `docs/smapi-runtime-acceptance.md`
- `scripts/Invoke-SmapiRuntimeAcceptance.ps1`
- `tests/StardewAI.Backend.Tests/ManualSmapiHarnessTests.cs`

Allowed script inputs for background collection:

- `-IsolatedStardewDirectory` records the isolated copy path and validates that it exists.
- `-BridgeBaseUrl` / `-BridgeUrl` selects the already-running Bridge HTTP endpoint.
- `-BackendBaseUrl` / `-BackendUrl` selects the already-running Backend HTTP endpoint.
- `-OutputDirectory` / `-ArtifactsDirectory` selects the artifact root.

Allowed Bridge HTTP endpoints:

- `GET /api/v1/snapshot`
- `GET /api/v1/capabilities`
- `GET /api/v1/events`

Bridge event stream query parameters:

- `limit`: optional page size, clamped by Bridge to a small bounded page.
- `after_sequence`: optional cursor; returns events with `event_sequence` greater than this value.
- `after_tick`: optional SMAPI/game tick filter; returns events with `game_tick` greater than this value.

The endpoint is HTTP polling only. It does not require WebSocket support for runtime acceptance.

Allowed Backend endpoints, only when `-IngestBackend` is explicitly passed:

- `POST /api/v1/snapshots`
- `POST /api/v1/capabilities`
- `POST /api/v1/events`
- `GET /api/v1/snapshots/latest`
- `GET /api/v1/events`
- `GET /api/v1/capabilities`
- `GET /api/v1/sync`

Allowed Stardew/SMAPI member paths are limited to the fields/events listed in the evidence table below, as already collected by the existing Bridge. This slice does not add new runtime reads.

Allowed event subscriptions for validation:

- `GameLoop.SaveLoaded`
- `GameLoop.DayStarted`
- `GameLoop.TimeChanged`
- `Player.Warped`
- `Player.InventoryChanged`
- `Display.MenuChanged`
- `GameLoop.ReturnedToTitle`

Explicitly forbidden domains for this slice:

- Farm/NPC/Map/Shop data expansion
- executor changes
- game-state mutation
- LLM, OCR, keyboard/mouse automation
- pathing, buying, selling, save editing

## Local Decompiled Evidence

All Stardew/SMAPI member claims in the current Phase 1A runtime capture are backed by local decompiled files:

| Field/event | Decompiled path | Line/pattern | Member path | Source kind | Runtime null/readiness condition |
| --- | --- | --- | --- | --- | --- |
| `environment.game_version` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs` | `334` | `Game1.version` | public game object/static field | none |
| `environment.smapi_version` | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI\Constants.cs` | `33` | `Constants.ApiVersion` | SMAPI public API | none |
| `environment.installed_mods[].id` | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI\IModRegistry.cs`; `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Framework\SMultiplayer.cs` | `9`; `513` | `IModRegistry.GetAll().Manifest.UniqueID` | SMAPI public API | none |
| `environment.installed_mods[].name` | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI\IModRegistry.cs`; `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Framework\SMultiplayer.cs` | `9`; `513` | `IModRegistry.GetAll().Manifest.Name` | SMAPI public API | none |
| `environment.installed_mods[].version` | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI\IModRegistry.cs`; `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Framework\SMultiplayer.cs` | `9`; `513` | `IModRegistry.GetAll().Manifest.Version` | SMAPI public API | none |
| `identity.save_id` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs` | `309` | `Game1.player.farmName.Value` | public game object | `Context.IsWorldReady`; unavailable before player exists |
| `identity.player_id` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs` | `1863` | `Game1.player.UniqueMultiplayerID` | public game object | `Context.IsWorldReady`; unavailable before player exists |
| `time.season` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs` | `1496` | `Game1.currentSeason` | public game object/static property | `Context.IsWorldReady` |
| `time.day` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs` | `761` | `Game1.dayOfMonth` | public game object/static field | `Context.IsWorldReady` |
| `time.time` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs` | `766` | `Game1.timeOfDay` | public game object/static field | `Context.IsWorldReady` |
| `time.weather` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs` | `679`, `681`, `683`, `685` | `Game1.isRaining/isSnowing/isLightning/isDebrisWeather` | derived from public game object/static fields | `Context.IsWorldReady` |
| `player.location_id` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Character.cs`; `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs` | `500`; `539` | `Game1.player.currentLocation.NameOrUniqueName` | public game object | `Context.IsWorldReady`; unavailable when player/location null |
| `player.tile_x` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Character.cs` | `377` | `Game1.player.TilePoint.X` | public game object | `Context.IsWorldReady`; unavailable when player null |
| `player.tile_y` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Character.cs` | `377` | `Game1.player.TilePoint.Y` | public game object | `Context.IsWorldReady`; unavailable when player null |
| `player.facing_direction` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs` | `2016` | `Game1.player.FacingDirection` | public game object | `Context.IsWorldReady`; unavailable when player null |
| `player.money` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs` | `1989` | `Game1.player.Money` | public game object | `Context.IsWorldReady`; unavailable when player null |
| `player.health` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs` | `662` | `Game1.player.health` | public game object | `Context.IsWorldReady`; unavailable when player null |
| `player.max_health` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs` | `664` | `Game1.player.maxHealth` | public game object | `Context.IsWorldReady`; unavailable when player null |
| `player.energy` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs` | `1826` | `Game1.player.Stamina` | public game object | `Context.IsWorldReady`; unavailable when player null |
| `player.max_energy` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs` | `1842` | `Game1.player.MaxStamina` | public game object | `Context.IsWorldReady`; unavailable when player null |
| `player.current_tool` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs`; `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Item.cs` | `1745`; `187`, `200` | `Game1.player.CurrentTool.QualifiedItemId` or `.DisplayName` | public game object | `Context.IsWorldReady`; unavailable when player/current tool null |
| `player.active_menu` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs` | `1678` | `Game1.activeClickableMenu` | public game object/static property | reports `none` when null |
| `player.inventory[].item_id` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Item.cs` | `168` | `Game1.player.Items[].ItemId` | public game object | `Context.IsWorldReady`; item slot may be null |
| `player.inventory[].qualified_item_id` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Item.cs` | `187` | `Game1.player.Items[].QualifiedItemId` | public game object | `Context.IsWorldReady`; item slot may be null |
| `player.inventory[].display_name` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Item.cs` | `200` | `Game1.player.Items[].DisplayName` | public game object | `Context.IsWorldReady`; item slot may be null |
| `player.inventory[].stack` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Item.cs` | `222` | `Game1.player.Items[].Stack` | public game object | `Context.IsWorldReady`; item slot may be null |
| `player.inventory[].quality` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Item.cs` | `242` | `Game1.player.Items[].Quality` | public game object | `Context.IsWorldReady`; item slot may be null |
| `mods.installed_count` | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI\IModRegistry.cs` | `9` | `IModRegistry.GetAll().Length` | SMAPI public API/derived count | none |
| `mods.installed_mods[].mod_id` | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI\IModInfo.cs`; `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Framework\SMultiplayer.cs` | `6`; `513` | `IModInfo.Manifest.UniqueID` | SMAPI public API | none |
| `mods.installed_mods[].name` | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI\IModInfo.cs`; `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Framework\SMultiplayer.cs` | `6`; `513` | `IModInfo.Manifest.Name` | SMAPI public API | none |
| `mods.installed_mods[].version` | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI\IModInfo.cs`; `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Framework\SMultiplayer.cs` | `6`; `513` | `IModInfo.Manifest.Version` | SMAPI public API | none |
| `mods.installed_mods[].author` | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Framework.Logging\LogManager.cs` | `276` | `IModInfo.Manifest.Author` | SMAPI public API | none |
| `GameLoop.TimeChanged` event | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Events\TimeChangedEventArgs.cs` | `9`, `12` | `TimeChangedEventArgs.OldTime/NewTime` | SMAPI event argument | SMAPI raises after time changes |
| `Player.Warped` event | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Events\WarpedEventArgs.cs` | `13`, `16`, `19` | `WarpedEventArgs.OldLocation/NewLocation/IsLocalPlayer` | SMAPI event argument | Bridge only publishes for local player |
| `Player.InventoryChanged` event | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Events\InventoryChangedEventArgs.cs` | `14`, `17`, `20`, `23` | `InventoryChangedEventArgs.Added/Removed/QuantityChanged/IsLocalPlayer` | SMAPI event argument | Bridge only publishes for local player |
| `Display.MenuChanged` event | `I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Events\MenuChangedEventArgs.cs` | `10`, `13` | `MenuChangedEventArgs.OldMenu/NewMenu` | SMAPI event argument | menus may be null |

## Live Runtime Status

- status: `not_executed`
- exact loop phrase: `真实游戏运行时验收尚未执行`
- cycle result: `code_preparation_complete_runtime_pending`
