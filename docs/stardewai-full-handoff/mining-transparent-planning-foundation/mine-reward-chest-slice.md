# MineShaft Reward Chest Slice

## Scope

- Ordinary Mine fixed rewards: floors 10, 20, 40, 50, 60, 70, 80, 90, 100, and 110.
- Skull Cavern live treasure rooms and forced treasure layouts, including multiple already-generated chests on internal levels 320 and 420.
- Excluded families: ordinary floor-120 Skull Key, Quarry Mine 77377, Volcano Dungeon, player chests, gift boxes, synchronized/drop-content/custom chests.

## Transparent Contract

`mining.reward_chests[]` is derived only from already-loaded `MineShaft.overlayObjects`. It never calls `addLevelChests`, `getTreasureRoomItem`, or any RNG-producing method. Each row carries the exact live item, inventory receipt projection, chest flags, branch identity, consumed marker state, Stardrop state, and native effect expectations.

`Chest.dumpContents` calls `gainExperience(5, 25 + mineLevel)`, but the installed game's `Farmer.gainExperience` returns immediately for skill 5. The bridge therefore records both facts:

- `native_gain_experience_call_amount = 25 + mineLevel`
- `expected_luck_experience_delta = 0`

These values must never be collapsed into a false positive Luck-XP training label.

## Compiler And Runtime

The small model selects `mining.claim_reward_chests`; the compiler binds one exact chest and emits `executor.claim_mine_reward_chest`. The executor rebinds the live chest, walks using the shared native-input BFS path, faces it, and performs one reward-open `MineShaft.checkAction`. It must not call again while the open chest still contains the reward, because that branch bypasses normal `dumpContents`. After `dumpContents` has cleared the item and the receipt is observed, one separate empty-chest `checkAction` runs the native removal tail without granting or bypassing a reward.

The same executor is reused by the rolling `mining.reach_depth` planner and by intermediate floors of `mining.obtain_skull_key`. A ready chest on the loaded floor is a must-take step before ladder, shaft, opportunistic debris, or target-depth exit. Healing and true deadline/health/energy retreat remain higher priority, and an immediate monster threat is cleared before approaching the chest. Floor 120 still uses the specialized Skull Key contract. `mining.reward_chests` is therefore a required transparent group for all objectives that share the mining floor planner; an unavailable group fails closed instead of silently treating the floor as chest-free.

Completion requires native chest removal, the floor consumed marker, unchanged Luck XP, and either the exact inventory unit-state multiset delta or the floor-100 Stardrop transitions (`CF_Mines`, base max stamina `+34`). Tool projections preserve the source tool's constructor-randomized `swingTicker`, because the native chest transfers the same object instead of creating a fresh clone.

Isolated run `runtime-mine-reward-chest-smoke-20260730-004220` verified one remixed ordinary reward, the floor-100 Stardrop, and both random rewards on forced Skull Cavern floor 320. The direct-entry debug fixture invokes the installed game's private `addLevelChests` only when the loaded target floor has no native reward chest; it neither chooses nor constructs rewards. Every claim then re-enters through the ordinary transparent snapshot and native executor.
