# Controller Audit

Status: merged into the main working tree after static review and build validation.

Controller-run validation on 2026-07-14:
- focused social tests: 163/163
- Core: 462/462
- Backend: 49/49
- RuntimeTestHarness build: 0 warnings, 0 errors

Accepted coverage:

- **Native `executor.social_interact`** — validates world/NPC identity/presence/location/tile/adjacency/action rectangle/menu/visibility/sleeping/CanSocialize/CanReceiveGifts/exact gift slot/item/stack and gift limits; only Stardrop Tea bypasses the daily limit, while spouse, birthday, or Stardrop Tea can bypass the weekly limit; calls `Game1.currentLocation.checkAction` as the only state-changing social call; records comprehensive typed before/after output.
- All blocked social executor results record the precise runtime reason in `FailureCategory` and use `TrainingImpactScope=executor_calibration`, so runtime failures do not affect strategy values.
- `recovery.stabilize_day` candidate-to-daily-plan chain is complete for all currently emitted candidates.
- Social talk/gift current-state candidates, daily-plan compilation (move_to_social_stand + social_interact), action queue, typed `executor.social_interact` request, and native `executor.social_interact` RuntimeTestHarness path are statically complete.
- Side-effect-free live vanilla social queries and owned-item gift taste/delta rows.
- Current-location talk and ordinary-gift candidates with exact reachable stand tiles.
- Gift limits and Stardrop Tea exceptions, missing friendship creation baseline, deterministic friendship modifiers, giftability fields, Green Rain/dumped-dialogue gates, and fail-closed special item/NPC branches.
- Blocked-row diagnostics, explicit unknown time/energy sentinels, social compiler envelope, and training output contract.
- Strict collision/route row shape validation, including valid JSON `false` discriminators.
- No direct `NPC.checkAction`, `tryToReceiveActiveObject`, `receiveGift`, friendship/counter/inventory/NPC-position mutation.
- Mainline fishing, mining, shop, route, interact, and harvest behavior preserved.

Intentional blockers:

- Direct high-level `social.talk_npc`/`social.gift_npc` remain gated through daily-plan compilation; only `executor.social_interact` is runtime enabled.
- Future schedule projection and cross-map social routing are not emitted by this current-state slice.
- Modded/overridden NPC social methods fail closed unless their query purity is proven.
- No live game was launched; validation is static-only + build + test.

Validation:

- Conflict-marker scan: clean.
- Targeted `git diff --check`: clean.
- Build: 0 warnings, 0 errors.
- Tests: 163 focused social tests passed.

Remaining work:

- Isolated E: runtime integration: talk smoke, ordinary gift smoke including one-item-to-null, blocked/replan cases, output artifact audit, then duration calibration.
- Runtime failures remain executor calibration and never strategy-negative.
