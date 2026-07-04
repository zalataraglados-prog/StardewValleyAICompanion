# Phase 1G: modded_state Read Slice

## Scope

`modded_state` is a transparent, read-only slice for mod registry metadata that SMAPI exposes without entering a mod's private save data or runtime internals.

The Phase 1G adapter reads only:

- `IModRegistry.GetAll()`
- `IModInfo.Manifest.UniqueID`
- `IModInfo.Manifest.Name`
- `IModInfo.Manifest.Version`
- `IModInfo.Manifest.Author`
- `IModInfo.IsContentPack`

It does not scan files, inspect mod folders, read arbitrary JSON/data assets, call mod APIs, write config, or read any mod-specific private save/state storage.

## Fields

The adapter reports the `modded_state` section with:

- `installed_count`: count of installed SMAPI registry entries.
- `installed`: installed mod/content-pack metadata from SMAPI registry.
- `content_pack_count`: count of registry entries marked `IsContentPack`.
- `content_packs`: installed entries marked `IsContentPack`.
- `private_mod_state`: explicit unavailable envelope for arbitrary private mod state.

Every available field uses source `IModRegistry.GetAll()` and adapter `smapi_mod_registry`.

## Unavailable

The section includes:

- `modded_state.private_mod_state`
- `modded_state.arbitrary_mod_private_save_data`

Reason:

`arbitrary_mod_private_state_unavailable_without_mod_specific_read_only_api`

This is intentional. There is no transparent, generic, safe way to read every mod's private state without mod-specific APIs, contracts, or explicit integration. Phase 1G therefore exposes only install/content-pack style metadata that SMAPI already provides.

## Integration Status

`ModdedStateReadAdapter` is implemented as a standalone adapter. Runtime registration is intentionally not included in this slice; the main controller should wire it into `TransparentStateCollector` separately.
