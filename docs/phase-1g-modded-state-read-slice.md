# Phase 1G: modded_state Read Slice

## Scope

`modded_state` is a transparent, read-only slice for mod registry metadata, SMAPI save data stored in the loaded save, and public `IHaveModData.modData` dictionaries.

The Phase 1G adapter reads only:

- `IModRegistry.GetAll()`
- `IModInfo.Manifest.UniqueID`
- `IModInfo.Manifest.Name`
- `IModInfo.Manifest.Version`
- `IModInfo.Manifest.Author`
- `IModInfo.IsContentPack`
- `Game1.CustomData` entries prefixed with `smapi/mod-data/`
- `Game1.player.modData`
- `Game1.currentLocation.modData`
- `Game1.getFarm().modData`

It does not scan files, inspect mod folders, read arbitrary JSON/data assets, call mod APIs, write config, or inspect arbitrary CLR private fields inside mod instances.

## Fields

The adapter reports the `modded_state` section with:

- `installed_count`: count of installed SMAPI registry entries.
- `installed`: installed mod/content-pack metadata from SMAPI registry.
- `content_pack_count`: count of registry entries marked `IsContentPack`.
- `content_packs`: installed entries marked `IsContentPack`.
- `arbitrary_mod_private_save_data`: raw SMAPI save data entries from `Game1.CustomData` with parsed `mod_id`, data key, and raw JSON string.
- `private_mod_state`: complete generic mod state summary for stable game data surfaces: public modData dictionaries and raw SMAPI save data. It explicitly reports that arbitrary CLR private fields are not exposed because they are not a stable game data surface.

Registry fields use source `IModRegistry.GetAll()` and adapter `smapi_mod_registry`. Save-data fields use `Game1.CustomData`. Public mod data fields use `IHaveModData.modData`.

## Completion Boundary

The section no longer reports unavailable fields for generic SMAPI save data. The verified SMAPI implementation stores `Helper.Data.ReadSaveData` entries under `Game1.CustomData` keys shaped like:

`smapi/mod-data/<mod id>/<key>`

Those entries are now enumerated as raw JSON strings. Arbitrary private CLR fields inside loaded mod classes remain outside the transparent game-data contract; reading them would be reflection over implementation internals, not a stable save/runtime data surface.

## Integration Status

`ModdedStateReadAdapter` is registered in `TransparentStateCollector` and included in runtime snapshots.
