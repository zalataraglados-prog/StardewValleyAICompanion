# Phase 1F Menu Read Slice

Scope: read-only transparent collection for `Game1.activeClickableMenu`. This slice adds adapter code only; it is intentionally not registered in `ModEntry`.

## Implemented Fields

| Section | Field | Status | Member path |
| --- | --- | --- | --- |
| `menus` | `active_menu` | available | `Game1.activeClickableMenu` type only; reports closed state when null |
| `menus` | `identity` | available when a menu is open | `Game1.activeClickableMenu.GetType()` |
| `menus` | `screen_bounds` | available when public base fields exist | `IClickableMenu.xPositionOnScreen`, `yPositionOnScreen`, `width`, `height` |
| `menus` | `public_state` | available when public base fields exist | `IClickableMenu.destroy`, `invisible`, `gameWindowSizeChanged`, `upperRightCloseButton`, `currentlySnappedComponent` presence only |
| `menus` | `menu_specific_state` | unavailable | concrete menu fields not verified in this slice |

When `Game1.activeClickableMenu` is null, `menus.active_menu` remains available with `is_open=false`; other menu fields use unavailable envelopes with `value=null` and reason `no_active_clickable_menu`.

## Unavailable Envelopes

Fields that are not safely proven are not defaulted. They use `status=unavailable`, `value=null`, `confidence=0.0`, and a specific reason:

| Field | Reason |
| --- | --- |
| `menus.identity` | `no_active_clickable_menu` when no menu is open |
| `menus.screen_bounds` | `no_active_clickable_menu` or `iclickablemenu_public_bounds_fields_not_available` |
| `menus.public_state` | `no_active_clickable_menu` or `iclickablemenu_public_state_fields_not_available` |
| `menus.menu_specific_state` | `menu_specific_fields_not_verified_in_this_slice` |

## Read-Only Audit Allowlist

Allowed source files:
- `src/StardewAI.TransparentBridge/Adapters/MenuReadAdapter.cs`
- `tests/StardewAI.Backend.Tests/MenuSnapshotIngestTests.cs`
- `docs/phase-1f-menu-read-slice.md`

Allowed member paths:
- `Game1.activeClickableMenu`.
- `IClickableMenu` public base fields listed above.
- Runtime type metadata from `Game1.activeClickableMenu.GetType()`.

Forbidden actions:
- No click handling.
- No close handling.
- No menu selection.
- No `receiveLeftClick`, `receiveRightClick`, `performHoverAction`, `exitThisMenu`, or equivalent menu action calls.
- No writes to menu fields or game state.

## Runtime Validation

Live SMAPI validation status: `not_executed`.

Pending runtime checks:
- Open no menu, inventory/menu, shop, dialogue, and chest screens.
- Confirm `/api/v1/snapshots/latest` includes `menus.active_menu`, `identity`, `screen_bounds`, `public_state`, and unavailable `menu_specific_state` after the main controller registers the adapter.
- Confirm opening and closing menus does not trigger clicks, closes, selections, or state mutation from this adapter.
