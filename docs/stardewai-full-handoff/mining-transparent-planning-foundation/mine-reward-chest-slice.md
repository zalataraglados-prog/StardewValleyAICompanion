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

Completion requires native chest removal, the floor consumed marker, unchanged Luck XP, and either the exact inventory unit-state multiset delta or the floor-100 Stardrop transitions (`CF_Mines`, base max stamina `+34`). Runtime evidence remains pending until testing is explicitly permitted.
