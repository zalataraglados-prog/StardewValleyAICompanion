# Transparency Coverage

## Covered

- NPC identity, display name, runtime type/provenance, master-data presence, gift-taste master-data presence, loaded current instance, location, tile, bounding box, facing direction, villager/child/datable/simple-non-villager, invisible, sleeping, controller, busy observation, schedule flags, loaded schedule identity, and birthday fields are inventoried by `NpcReadAdapter`.
- Friendship points, hearts, status, talked today, gifts today/week, last gift date, proposal, dating, engaged, married, roommate, divorced, wedding/birthing dates, and proposer are required for social planning.
- Inventory item identity, slot, quality, stack, object shape, quest item, big craftable, furniture/wallpaper, protection, `not_giftable` base tag, special item flag, and `can_be_given_as_gift` are available from `PlayerReadAdapter`.
- Current talk candidates require complete social legality, current loaded NPC facts, clear menu, current route window completion, and an adjacent non-collision stand tile.
- Current gift candidates additionally require complete gift legality, vanilla limit exceptions, non-divorced relationship where a row exists, unprotected/non-quest/non-special item, no dumped/green-rain rejection, no special switch item or roommate-proposal tag, and complete deterministic taste/delta evidence.
- Compiler preserves requested NPC/item and social evidence in `SocialPlanEnvelope`.
- **Native `executor.social_interact` is implemented** — validates world/location/NPC identity/presence/tile/adjacency/action rectangle/menu/visibility/sleeping/CanSocialize/CanReceiveGifts/exact gift slot/item/stack and gift limits; only Stardrop Tea bypasses the daily limit, while spouse, birthday, or Stardrop Tea can bypass the weekly limit; calls `Game1.currentLocation.checkAction` as the only state-changing social call and records comprehensive typed before/after output.
- All blocked social executor results record the precise runtime reason in `FailureCategory` and use `TrainingImpactScope=executor_calibration`, ensuring runtime failures do not affect strategy values.
- `recovery.stabilize_day` candidate-to-daily-plan chain compiles to close-menu, refresh-plan/wait, or verified at-home sleep operations for all currently emitted candidates.
- Social talk/gift current-state candidates, daily-plan compilation (move_to_social_stand + social_interact), action queue, typed `executor.social_interact` request, and native `executor.social_interact` RuntimeTestHarness path are statically complete.
- Direct high-level `social.talk_npc`/`social.gift_npc` remain gated through daily-plan compilation; only `executor.social_interact` is runtime enabled.

## Fail-Closed

- `CanSocialize`, `CanReceiveGifts`, and gift taste are called only for vanilla runtime methods whose declaring type proves the non-overridden query path; modded/overridden rows are marked incomplete.
- Blocked social rows remain visible in `social_candidates` with stable block reasons; ranking and compiler matching only use available rows.
- Future schedule windows are not emitted because complete non-mutating precedence for green rain, island, festivals, marriage/divorce, rain, season/day/week, and special overrides is not implemented.
- Gift jealousy, dialogue branches, rejection text, event/menu side effects, and runtime item/friendship mutations are not claimed deterministic in this slice.
- Unknown/modded NPCs and missing taste/schedule/route inputs fail closed with visible blocked-row diagnostics where enough identity exists to form a row.
- Runtime social failures (NPC moved, tile mismatch, unexpected state) resolve as `blocked` with `executor_calibration` scope and never produce strategy-negative feedback.

## Pending

- Isolated E: runtime integration: talk smoke test, ordinary gift smoke test including one-item-to-null, blocked/replan cases, output artifact audit, then duration calibration.
- No live game was launched; validation is static-only for the executor code.
- Social executor duration remains planner-budget-assumed until runtime calibration.
