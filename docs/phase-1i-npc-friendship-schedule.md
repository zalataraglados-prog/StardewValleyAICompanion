# Phase 1I NPC Friendship and Schedule Read Slice

Scope: deepen the NPC transparent read slice for friendship and schedule feasibility.

Implemented adapter: `StardewAI.TransparentBridge.Adapters.NpcReadAdapter`.

## Supported fields

| field | status | source | notes |
| --- | --- | --- | --- |
| `npcs.positions` | `available` when world ready | `Game1.currentLocation.characters` plus `Character` read properties | Existing current-location NPC position slice. |
| `npcs.friendships` | `available` when world ready and `Game1.player` is available | `Game1.player.friendshipData.Pairs` plus `Friendship` read properties | Includes NPC key, points, derived heart level, gift counters, talked flag, relationship status flags, proposal/roommate flags, date total-day values, and proposer. |
| `npcs.schedules` | `unavailable` | `StardewValley.NPC.Schedule` | Schedule remains unavailable. Complete reliable schedule reading is not proven read-only because vanilla schedule loading and checking paths mutate NPC schedule/pathing state. |

`npcs.positions` and `npcs.friendships` are `unavailable` when `Context.IsWorldReady` is false or the required game object is null.

## Decompiled Evidence

| field/event name | decompiled path | line or search pattern | member path | source kind | runtime null/readiness condition |
| --- | --- | --- | --- | --- | --- |
| `npcs.friendships` collection | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs` | `Farmer.cs:771 [XmlElement("friendshipData")]`; `Farmer.cs:772 public readonly NetStringDictionary<Friendship, NetRef<Friendship>> friendshipData`; `Farmer.cs:2143 AddField(friendshipData, "friendshipData")` | `Game1.player.friendshipData.Pairs` | public game object / NetField | `Context.IsWorldReady`; `Game1.player != null`; pair key not blank; pair value not null |
| `npcs.friendships.points` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Friendship.cs` | `Friendship.cs:34 public int Points`; `Friendship.cs:204 AddField(points, "points")` | `Friendship.Points` | public game object / NetField | friendship entry not null |
| `npcs.friendships.heart_level` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs`; `...\Friendship.cs` | `NPC.cs:52 public const int friendshipPointsPerHeartLevel = 250`; `Friendship.cs:34 public int Points` | `Friendship.Points / NPC.friendshipPointsPerHeartLevel` | deterministic derived value | friendship entry not null |
| `npcs.friendships.gifts_this_week/gifts_today` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Friendship.cs` | `Friendship.cs:46 public int GiftsThisWeek`; `Friendship.cs:58 public int GiftsToday`; `Friendship.cs:204-205 AddField(giftsThisWeek/giftsToday, ...)` | `Friendship.GiftsThisWeek`; `Friendship.GiftsToday` | public game object / NetField | friendship entry not null |
| `npcs.friendships.talked_to_today` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Friendship.cs` | `Friendship.cs:82 public bool TalkedToToday`; `Friendship.cs:207 AddField(talkedToToday, "talkedToToday")` | `Friendship.TalkedToToday` | public game object / NetField | friendship entry not null |
| `npcs.friendships.status` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Friendship.cs` | `Friendship.cs:130 public FriendshipStatus Status`; `Friendship.cs:211 AddField(status, "status")` | `Friendship.Status.ToString()` | public game object / NetField | friendship entry not null |
| `npcs.friendships.relationship_flags` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Friendship.cs` | `Friendship.cs:237 IsDating`; `Friendship.cs:246 IsEngaged`; `Friendship.cs:251 IsMarried`; `Friendship.cs:256 IsDivorced`; `Friendship.cs:261 IsRoommate` | `Friendship.IsDating/IsEngaged/IsMarried/IsDivorced/IsRoommate()` | public game object methods reading `Status`/`roommateMarriage` | friendship entry not null |
| `npcs.friendships.proposal_rejected/roommate_marriage/proposer` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Friendship.cs` | `Friendship.cs:94 public bool ProposalRejected`; `Friendship.cs:142 public long Proposer`; `Friendship.cs:154 public bool RoommateMarriage`; `Friendship.cs:208/212/213 AddField(...)` | `Friendship.ProposalRejected`; `Friendship.Proposer`; `Friendship.RoommateMarriage` | public game object / NetField | friendship entry not null |
| `npcs.friendships.last_gift/wedding/next_birthing_date_total_days` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Friendship.cs` | `Friendship.cs:70 public WorldDate LastGiftDate`; `Friendship.cs:106 public WorldDate WeddingDate`; `Friendship.cs:118 public WorldDate NextBirthingDate`; `Friendship.cs:206/209/210 AddField(...)` | `Friendship.LastGiftDate?.TotalDays`; `Friendship.WeddingDate?.TotalDays`; `Friendship.NextBirthingDate?.TotalDays` | public game object / NetField | date refs may be null |
| `npcs.schedules` unavailable | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs` | `NPC.cs:533 public Dictionary<int, SchedulePathDescription> Schedule { get; private set; }`; `NPC.cs:531 remarks set schedule using TryLoadSchedule` | unavailable only; no schedule member read | unavailable | Always unavailable in this slice |
| schedule loading mutates schedule state | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs` | `NPC.cs:5754 TryLoadSchedule()`; `NPC.cs:5950 TryLoadSchedule(string key, Dictionary<int, SchedulePathDescription> schedule)`; `NPC.cs:5957 Schedule = schedule`; `NPC.cs:5960 dayScheduleName.Value = key`; `NPC.cs:5962 followSchedule = true`; `NPC.cs:5967 ClearSchedule()`; `NPC.cs:5969 Schedule = null`; `NPC.cs:5972 dayScheduleName.Value = null`; `NPC.cs:5974 followSchedule = false` | forbidden for transparent read | mutation path | Not used |
| schedule parsing/loading uses content and pathing | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs` | `NPC.cs:5377/5382/5402 PathFindController.findPathForNPCSchedules(...)`; `NPC.cs:5989 _hasLoadedMasterScheduleData`; `NPC.cs:5991 _hasLoadedMasterScheduleData = true`; `NPC.cs:6001 Game1.content.Load<Dictionary<string, string>>(...)`; `NPC.cs:6002 _masterScheduleData = new Dictionary...` | forbidden for transparent read | content load/cache mutation/pathing dependency | Not used |
| schedule checking mutates route state | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs` | `NPC.cs:4092 checkSchedule`; `NPC.cs:4113 lastAttemptedSchedule = timeOfDay`; `NPC.cs:4117 queuedSchedulePaths.Add(value)`; `NPC.cs:4140 queuedSchedulePaths.RemoveAt(0)` | forbidden for transparent read | mutation/pathing state | Not used |

## Read-Only Allowlist

Allowed source files:

- `src/StardewAI.TransparentBridge/Adapters/NpcReadAdapter.cs`
- `docs/phase-1i-npc-friendship-schedule.md`
- `tests/StardewAI.Backend.Tests/NpcSnapshotPayloadTests.cs`

Allowed Stardew/SMAPI member paths:

- `Context.IsWorldReady`
- `Game1.currentLocation`
- `GameLocation.NameOrUniqueName`
- `GameLocation.characters`
- `NPC.Name`
- `NPC.displayName`
- `NPC.TilePoint`
- `NPC.FacingDirection`
- `NPC.currentLocation`
- `NPC.IsVillager`
- `NPC.IsMonster`
- `NPC.friendshipPointsPerHeartLevel`
- `Utility.isOnScreen(Point, int, GameLocation)`
- `Game1.player`
- `Farmer.friendshipData.Pairs`
- `Friendship.Points`
- `Friendship.GiftsThisWeek`
- `Friendship.GiftsToday`
- `Friendship.TalkedToToday`
- `Friendship.Status`
- `Friendship.IsDating()`
- `Friendship.IsEngaged()`
- `Friendship.IsMarried()`
- `Friendship.IsDivorced()`
- `Friendship.IsRoommate()`
- `Friendship.ProposalRejected`
- `Friendship.RoommateMarriage`
- `Friendship.LastGiftDate?.TotalDays`
- `Friendship.WeddingDate?.TotalDays`
- `Friendship.NextBirthingDate?.TotalDays`
- `Friendship.Proposer`

Allowed event subscriptions: none.

Forbidden domains for this slice:

- schedule loading, schedule parsing, `TryLoadSchedule`, `ClearSchedule`, `checkSchedule`
- pathing and `PathFindController.findPathForNPCSchedules`
- dialogue, gift taste, quest, mail, farm, inventory, movement, input, save, or game-state mutation
- friendship mutation, including `changeFriendship`, direct `Friendship` setters, or APIs that create missing friendship entries

## Runtime Validation

Live SMAPI validation status: `not_executed`.

