# Phase 1J Menu-Specific Read Slice

Scope: first batch of menu-specific transparent reads for inventory, chest, shop, and dialogue menus. This slice is read-only and only reads public menu fields verified from the Stardew Valley 1.6 decompile under `I:\StardewValleyAICompanion-decompile`.

## Implemented Fields

Top-level `menus.menu_specific_state` is now available for these menu types:

| Menu | Kind | Read-only source fields |
| --- | --- | --- |
| `InventoryMenu` | `inventory` | `playerInventory`, `drawSlots`, `showGrayedOutSlots`, `capacity`, `rows`, `horizontalGap`, `verticalGap`, `inventory.Count`, `actualInventory.Count`, occupied slot count, `dropItemInvisibleButton`, `moveItemSound` |
| `ItemGrabMenu` with `source == source_chest` | `chest` | `source`, boolean flags, `message` presence only, `sourceItem` type only, `context` type only, receiving/player inventory summaries, side button presence, transferred sprite count |
| `ShopMenu` | `shop` | `ShopId`, `ShopData` presence, `currency`, `readOnly`, `currentItemIndex`, `safetyTimer`, list/dictionary counts, held/hovered item presence, portrait presence, inventory summary, control presence |
| `DialogueBox` | `dialogue` | dialogue/response counts, question and transition booleans, selected response index, timers, bounds, icon/image presence, friendship jewel rectangle |

Unsupported concrete menu types keep `menus.menu_specific_state` unavailable with reason `menu_specific_fields_not_verified_for_menu_type`.

## Explicitly Unavailable Fields

Concrete fields that are not reliable enough in this slice remain unavailable instead of defaulting:

| Path | Reason |
| --- | --- |
| `menus.menu_specific_state.chest.source_item_details` | `chest_source_item_concrete_fields_not_verified_in_this_slice` |
| `menus.menu_specific_state.shop.current_tab` | `shop_current_tab_is_protected_field_not_read_in_this_slice` |
| `menus.menu_specific_state.dialogue.current_text` | `dialogue_current_text_requires_method_call_not_read_in_this_slice` |

Item identities, prices, stock details, response text, dialogue current text, protected/private menu fields, and concrete chest item internals are not read in this slice unless listed above as a count, presence flag, type name, or public scalar field.

## Read-Only Audit Allowlist

Allowed source files:
- `src/StardewAI.TransparentBridge/Adapters/MenuReadAdapter.cs`
- `tests/StardewAI.Backend.Tests/MenuSnapshotIngestTests.cs`
- `docs/phase-1j-menu-specific-read.md`

Allowed member paths:
- `Game1.activeClickableMenu`.
- `IClickableMenu` public base fields from Phase 1F.
- Public fields on `InventoryMenu`, `ItemGrabMenu`, `ShopMenu`, and `DialogueBox` listed in this document.
- Runtime type metadata from `GetType()` for menu/source/context identity only.

Forbidden actions:
- No click handling.
- No close handling.
- No menu selection.
- No calls to `receiveLeftClick`, `receiveRightClick`, `performHoverAction`, `update`, `exitThisMenu`, `closeDialogue`, `getCurrentString`, `tryToPurchaseItem`, inventory add/remove helpers, or equivalent menu action methods.
- No writes to menu fields, inventory lists, game state, or UI selection state.

## Runtime Validation

Live SMAPI validation status: `not_executed`.

Pending runtime checks:
- Open inventory, player chest, shop, dialogue, and an unsupported menu.
- Confirm supported menus publish available `menus.menu_specific_state.value.kind`.
- Confirm unavailable nested fields remain unavailable and do not carry default values.
- Confirm unsupported menus keep top-level `menus.menu_specific_state` unavailable.
- Confirm opening/closing/hovering menus shows no bridge-triggered clicks, closes, selections, purchases, response choices, or state mutation.
