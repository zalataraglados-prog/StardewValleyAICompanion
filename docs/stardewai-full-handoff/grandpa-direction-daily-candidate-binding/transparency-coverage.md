# transparency-coverage.md - Grandpa Direction Daily Candidate Binding (Final)

## Transparent Field Coverage Summary

| Direction | Status | Reason |
|-----------|--------|--------|
| earn_money | Active (direct binding) | Permitted kind/option checks pass for sell_or_ship_inventory_item candidates |
| raise_friendships | Active (direct binding) | Permitted kind/option checks pass for social_talk_current, social_gift_current candidates |
| complete_master_angler | Active (direct binding) | Permitted kind/option checks pass for catch_fish candidates |
| complete_full_shipment | Blocked | Planned contract gap: shipped-items-tracking |
| raise_skill_levels | Blocked | Planned contract gap: skill-level tracking |
| obtain_skull_key | Blocked | Planned contract gap: mine-level tracking |
| complete_museum_collection | Blocked | Planned contract gap: museum-items tracking |
| obtain_rusty_key | Blocked | Planned contract gap: museum-donation tracking |
| complete_community_center | Blocked | Planned contract gap + CC/Joja route commitment unresolved |
| complete_joja_development | Blocked | Planned contract gap + CC/Joja route commitment unresolved |
| marriage_and_house_upgrade | Blocked | Planned contract gap: spouse/house tracking |
| earn_pet_love | Blocked | Planned contract gap: pet-friendship tracking |

## Overall Transparency Coverage

- **Direct-binding directions (3/12)**: Active -- binding uses permitted option/kind checks only; does not inspect snapshot field contents speculatively
- **Blocked directions (9/12)**: Blocked as planned contract gaps -- the catalog records required fields/capabilities but the binder does NOT perform speculative runtime checks against snapshot contents

## No Speculative Field/Capability Inspection

The previous `FieldReadableInSnapshot()` and `CapabilityAvailable()` methods that inspected snapshot paths have been removed. The binder does not check whether transparent fields are "readable" or capabilities are "available" at runtime. The catalog's `required_transparent_fields` and `required_capabilities` represent planned contract gaps -- documentation of what would be needed to enable binding, not runtime checks.

For the three direct-binding rows, `required_transparent_fields` and `required_capabilities` are empty because current candidate availability is the authority.

For the nine blocked rows, `MissingTransparentFields` and `MissingCapabilities` are populated from the catalog in every blocked result.

## Planned Contract Gaps (Future Work)

The following would need to be implemented in the transparent bridge to enable binding for blocked directions:
1. Shipped items tracking (`player.shipped_items_complete`)
2. Per-skill level breakdown (`player.skills_detail`)
3. Deepest mine level (`world_progress.mine_level`)
4. Museum items (`world_progress.museum_items`)
5. Community center bundles (`world_progress.community_center.bundles`)
6. Joja development progress (`world_progress.joja_development_progress`)
7. Spouse/roommate tracking (`player.spouse`)
8. Pet ownership/friendship (`farm.pet`, `npcs.pet_friendship`)

Additionally, CC/Joja route commitment evidence would need to be exported from state that proves which route the player committed to (beyond current transparent state capabilities).
