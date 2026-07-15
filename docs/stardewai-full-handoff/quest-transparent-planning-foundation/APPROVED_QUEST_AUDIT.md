# Approved Quest Audit

Use decompiled source as authority. The scout report is advisory and contains known errors listed in `CONTROLLER_REVIEW.md`.

Approved facts:

- `Quest` is concrete and can represent `type_basic=1`; it also has 11 serialized subclasses. Runtime class, not `questType` alone, must distinguish the three type-9 subclasses.
- Current `QuestProgressRef` already records `quest_type`, but omits runtime class and the per-subclass progress/target fields needed for machine planning.
- Ordinary quests and `SpecialOrder` are separate systems and must remain separate candidate families.
- `SpecialOrder` has nine objective subclasses and six reward subclasses; objective/reward runtime type and per-type fields are currently missing.
- `quest.advance=120` has no source basis and must become unknown until a candidate-specific compiler/executor model exists.
- Exact current progress should be read from direct Net fields/properties. Human-readable objective strings are display-only.
- No runtime executor exists for generic quest advancement; candidates must remain preview-only and blocked from ranking/training execution.

Required ordinary runtime types:

- base `Quest`, `CraftingQuest`, `ItemDeliveryQuest`, `SlayMonsterQuest`, `SocializeQuest`, `GoSomewhereQuest`, `FishingQuest`, `HaveBuildingQuest`, `ItemHarvestQuest`, `ResourceCollectionQuest`, `LostItemQuest`, `SecretLostItemQuest`.

Required special-order objective types:

- `CollectObjective`, `DeliverObjective`, `DonateObjective`, `FishObjective`, `GiftObjective`, `JKScoreObjective`, `ReachMineFloorObjective`, `ShipObjective`, `SlayObjective`.

Required reward types:

- `FriendshipReward`, `GemsReward`, `MailReward`, `MoneyReward`, `ObjectReward`, `ResetEventReward`.
