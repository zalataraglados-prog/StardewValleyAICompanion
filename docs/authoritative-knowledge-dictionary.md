# Authoritative Game Knowledge Dictionary

## Truth order

1. Runtime-loaded game content is authoritative for the exact installed game and mod set.
2. Decompiled code defines executable semantics, conditions, formulas, and side effects.
3. The Stardew Valley Wiki and strategy references are independent verification sources.

Wiki text or examples must never create a field, rule, or value that runtime content and
decompiled code do not support.

## Export coverage

`StardewAI.KnowledgeExporter` creates a versioned export directory containing:

- a SHA-256 inventory of every base-game XNB file;
- every public one-argument `StardewValley.DataLoader` asset, discovered at runtime instead
  of maintained as a hand-written allowlist;
- every base event, festival, NPC dialogue, and NPC schedule asset discovered from the XNB
  inventory and loaded through SMAPI's merged game-content API;
- one payload file per asset, including the exact loader, declared type, payload hash, and
  provenance kind;
- `progress.json` after every asset, so a crash or unsupported asset remains visible;
- `manifest.json` only after the run finishes, with `complete` or `partial` status.

The raw XNB inventory is the omission detector. A decoded export is the machine-readable
dictionary. Assets listed only in the inventory are not yet semantically decoded and must not
be reported as implemented knowledge.

## Verified 1.6.15 source set

The current immutable source run is `game-1.6.15-20260723T093543Z`. It was produced by
an isolated vanilla Linux-host exporter and independently checked against the local
installation:

- 3,550 XNB files inventoried and rehashed, with no missing or changed file;
- 585 semantic payloads exported successfully, with no failed or duplicate
  export;
- 2,288 localized variants retained in the hash inventory instead of duplicated as logic;
- all 375 `Maps/` assets runtime-classified: 259 xTile maps and 116 non-map assets such as
  textures;
- no undecoded semantic XNB blockers.

The direct runtime-data graph currently contains 13,207 nodes and 2,645 direct runtime-data
edges. Direct edges cover crops, machines, shops, buildings, special orders, cooking recipes,
crafting recipes, and Community Center bundles. Recipe ingredient categories, the seasonal
seed special matcher, Vault money payments, random recipe outputs, and legacy standard item
descriptions retain their native matching/creation semantics. A direct edge is not permission
to infer an opaque condition or custom method.

`map-topology-index.json` projects all 259 xTile maps without expanding every tile into graph
nodes:

- 1,196 layers and 805,587 non-empty tiles represented as row runs;
- 867 `Warp`/`NPCWarp` records parsed with the exact
  `GameLocation.updateWarps` five-field grammar, with zero parse failures;
- 1,102 effective `Action`/`TouchAction` properties with native tile-property precedence;
- 194,347 statically blocked tiles derived from the exact
  `GameLocation.isTilePassable` `Back`/`Buildings` rule;
- zero topology blockers.

The static passability result deliberately excludes dynamic buildings, furniture, placed
objects, characters, events, map mutations, and location-specific collision overrides. Those
remain runtime context instead of being guessed from layer occupancy.

## Transparent field join

`StardewAI.KnowledgeCompiler --snapshot-schema` joins every distinct
`OptionRegistry.RequiredStateFactors` path to an exact field envelope in the validated current
full live snapshot pointer. The current join covers 94 of 94 required fields:

- 77 readable with complete source and adapter provenance in the current farm context;
- 17 schema-present but contextually unavailable because the host was not currently fishing,
  in a mine, or in the volcano;
- zero missing fields, adapter errors, invalid envelopes, or readable fields without
  provenance.

Contextual unavailability is not accepted as runtime proof for that scene. Each contextual
field still needs a scene-specific snapshot before its executor can be accepted. Missing,
error, invalid, or provenance-free required fields block the dictionary build.

`option-governance-matrix.json` separately records the `option_spec.v2` governance contract.
The locked artifact's historical 95-option count is not the whole-game semantic action
denominator. The live source contains 97 registered options as of 2026-08-01; generated
reconciliation output, rather than this prose, owns the current count. Risk,
irreversibility, confirmation, host, ownership,
adapter, compiler/verifier binding, evidence status, autonomous-candidate policy, invocation policy, training
eligibility, and product status are explicit for every entry. Unknown policies, duplicate
IDs, missing bindings, and irreversible actions without confirmation fail registry
initialization. Compiler or Harness registration does not promote an option to runtime
verified or training eligible; those statuses require separately indexed E3 or E4 evidence.

`capability_registry.v3` is the versioned machine-readable source for operational capability
stages. `OptionRegistry`, compiler-binding checks, RuntimeTestHarness dispatch projection,
product-executor projection, daily-candidate classification, bridge capability output,
knowledge matrices, and the training allowlist consume or validate that source. The current
baseline deliberately declares zero product executors. It generates
`training-admission-manifest.json` with independent read/candidate/compile/runtime/output
gates, evidence IDs, bounded evidence scope, and typed exclusion reasons. The current allowlist
contains `mining.reach_depth`, restricted to the candidate-bound ordinary-mine rolling scope
proven by EVD-095, and the explicit player/ordinary-owned-chest scope of
`inventory.transfer_item` proven by EVD-192. It also contains `social.talk_npc` only for the
EVD-076/EVD-105 scope: a current loaded vanilla NPC reached on the same map or through rolling
resolved connectors, followed by safe ordinary dialogue closure. EVD-196 adds `social.gift_npc`
only for a current loaded vanilla NPC reached on the same map or through rolling resolved
connectors and one ordinary owned item whose final stack is observed changing from one to null.
None of these scopes implies
broader completion. `recovery.stabilize_day` has verified read/candidate/compile/runtime/output
evidence through EVD-195, but remains excluded because it is a calibration-only high-level
option.

Invocation policy is orthogonal to runtime capability. `PlayerCommandOnly` options remain in
the semantic dictionary and may retain complete compiler/runtime evidence, but are excluded
from default candidates and policy training. Only an availability request carrying
`InvocationSource=PlayerCommand` may enter their existing confirmation and safety gates.
Current examples include House Plant rotation, Singing Stone playback, building appearance,
furniture placement, and sign content changes. EVD-274 records the complete native Singing
Stone pitch distribution but deliberately leaves the exact next shared-RNG pitch unavailable;
executor evidence for that action is not policy-training evidence. By contrast, EVD-272 admits exact natural Slime Ball collection because
the model chooses only a legal target while the compiler binds all RNG, movement, interaction,
and output mechanics.
The legacy `executor_enabled`
availability field means only that the internal compiler/Harness or candidate chain is
enabled; `product_executor_supported` is the separate product claim. Harness dispatch,
product integration, runtime evidence, and training eligibility cannot promote one another.

`scripts/Install-LiveSnapshotSchema.ps1` validates every current required factor, stages all
replacement files, then updates `snapshots/current-live-full-snapshot.json`, metadata, and the
checked-in hash lock; UTF-8 snapshots with or without BOM are accepted. Default reconciliation
requires that current pointer and rejects a missing or hash-divergent lock instead of falling back
to a dated sample. Failed candidate validation is retained separately and cannot replace the
current validation report.
The 94-of-94 join is a current required-field schema/provenance result, not a whole-game action
completeness claim. Refresh and regenerate it after every required-field change before promoting
another option through the read gate.

## Action omission boundary

The authoritative dictionary cannot, by itself, prove that every playable action is
implemented. Runtime content and decompiled types establish what exists; executable
coverage additionally requires the generated native action surface inventory, a frozen
semantic action denominator, a typed candidate, compiler binding, product runtime terminal,
and verified output delta.

`quest-action-coverage-matrix.json` closes the enumeration side of that boundary for native
quests. The knowledge compiler scans the 1.6.15 `Quest` and `OrderObjective` subclasses and
joins them to `QuestActionCoverageCatalog`. A native subclass absent from the catalog is a
blocking source-validation issue. The current scan found all 12 ordinary quest runtime
types and all 9 special-order objective runtime types, with zero uncatalogued or
catalog-only types. Their 28 action stages currently contain 23 bound stages, 1 explicit
implementation gap, 3 native observation-only stages, and 1 native-unreachable compatibility stage.

This matrix guarantees that the scanned native quest type surface is not silently omitted.
It does not turn the blocked Junimo Kart stage into an executable action, cover mod-added runtime
types without rescanning the active mod set, or replace isolated runtime verification.
The type-11 weeding constant is retained explicitly rather than omitted: locked `Data/Quests`
contains no such row, the native factory has no branch, and the quest sources have no assignment.
Knowledge compilation blocks if that reachability evidence changes.

Candidate evaluation now exposes separate `read_eligible`, `binding_status`,
`compile_status`, `execution_authorization`, `runtime_evidence_status`,
`training_eligibility`, and `product_status` fields. Production snapshots apply the
per-option RequiredFact policy to field status, confidence, age, provenance, Adapter ID,
and explicitly authorized derivations. An empty parameter set is `unbound/not_evaluated`
unless the option schema explicitly declares that it takes no parameters. Only a real
ActionQueue compiler probe can produce `compile_status=ready`; a readable option is not
therefore executable, runtime verified, or training eligible.

The join found and removed the obsolete `player.skills` option dependency. All affected
options now use the canonical transparent field `player.skills_detail`; no compatibility alias
or duplicate read path was added.

## Platform-bound binary and decompile evidence

Assembly version text is not a sufficient identity. The Windows client and Linux dedicated
host both report Stardew Valley `1.6.15`, but their `Stardew Valley.dll` MVIDs and SHA-256
hashes differ. Runtime parser evidence may only bind to an assembly with the same assembly
name and MVID; runtime-semantics v3 additionally verifies byte length and SHA-256. A mismatch
blocks the build before IL closure generation.

The current authoritative derived profile is
`%STARDEWAI_KNOWLEDGE_ROOT%/derived/game-1.6.15-20260723T093543Z-linux-v24`, with
`I:\StardewAI-KnowledgeArtifacts\game-1.6.15` as the default Windows artifact root. The
checked-in `knowledge-artifacts.lock.json` pins its manifests and binary hashes. It binds the
Linux host runtime export to binaries copied from that same host and to their separate
decompile tree:

- assembly version `1.6.15.24356` for both binaries;
- `Stardew Valley.dll` MVID `46c95350-5805-4442-8e93-61092d55e101`, SHA-256
  `f3e97f01d3fd2b1e6094fc8d2b59950aa6cb9d6cd1bf1b39d72d58edda8aad12`;
- `StardewValley.GameData.dll` MVID `ac7f210d-13f7-4425-b6cf-92d4d21093a8`, SHA-256
  `352e3b9189cdee588f88b1f956db368c56caf89e45258b0f75377f2225dcf311`;
- 1,116 hashed C# files from the matching Linux-host decompile;
- 2,057 metadata types and 18,906 methods;
- metadata token, signature hash, and IL hash per method body;
- zero invalid IL bodies;
- 824 native-query condition strings, 258 event scripts, and 17 data-referenced methods
  inventoried from the exported payloads;
- all 17 method references resolved to installed assembly evidence, with zero unresolved or
  ambiguous references.

The subsequent quest-objective execution slice is documented in
`docs/quest-objective-execution-coverage.md`. It replaces the blanket quest executor
block with typed bindings for exact fishing, routing, NPC delivery/report, native
drop-box donation, shipping, and mine-depth objectives. Objective kinds listed as remaining blockers in that
document are still fail-closed.

The exporter also runs the game's own parsers without evaluating conditions or executing
commands. The current runtime registry contains 385 canonical/alias rows: 116 game-state-query
rows, 102 event-precondition rows, 141 event-command rows, and 26 trigger-action rows. All 824
query strings parse, all preconditions and commands in all 258 event scripts resolve, and all
39 actions in the 31 `Data/TriggerActions` entries parse through
`TriggerActionManager.ParseAction`; the unresolved and parse-error counts are zero.

`handler-operation-rules.json` now contains 223 exact-platform handler/data-method rules with
transitive field reads, field writes, property reads, property writes, calls, reflection
boundaries, dynamic dispatch boundaries, and random sources. It uses shared string catalogs;
consumers resolve each `*Ids` array against the corresponding catalog instead of receiving
duplicated strings.

`executable-rule-index.json` binds every exported executable token to those operation rules:

- 824 conditions and 959 parsed condition clauses;
- 258 event scripts, 656 event preconditions, and 14,755 event commands;
- 31 trigger-action entries and 39 native-parsed action instances;
- 17 data-referenced methods;
- zero unresolved bindings and zero IL decode failures.

`progression-dependency-index.json` losslessly joins progression-bearing mail, trigger, and
event instructions:

- 179 mail entries and all 107 `%item`/`%action` directives, interpreted with the exact
  `LetterViewerMenu` command branches;
- all 31 trigger-action entries, including native conditions, permanent-skip conditions,
  trigger names, action tokens, and exact handler identities;
- all 258 event scripts with their native-parsed preconditions and commands;
- 244 explicit mail, event, quest, item, recipe, friendship, world-state, and special-order
  references whose argument roles were verified against the 1.6.15 handlers;
- zero malformed directives, unknown command kinds, unresolved handlers, or blocking issues.

Commands without a verified progression argument role remain preserved as exact tokens and
handler identities. They are not guessed into dependency edges.

`access-constraint-index.json` joins static interaction locations to shops, door windows, and
NPC schedules:

- 77 shops, 89 owner records, and 897 stock rows;
- 154 exact `LockedDoorWarp` access windows;
- 49 shop interaction endpoints, of which 42 directly identify a shop and seven deliberately
  delegate festival/context resolution to the native location handler;
- 33 NPC schedule assets, 421 schedule entries, and 1,714 parsed schedule segments;
- zero malformed access records or blocking issues.

Static endpoints do not assert that an NPC owner is currently present or that an event has not
mutated the map. Those are live planning conditions.

`goal-dependency-index.json` validates collection/economy grammars and the exact target goal:

- 31 Community Center bundles and 135 ingredient/payment requirements;
- 81 cooking recipes and 150 crafting recipes, with 552 native-matched inputs and 231 outputs;
- all recipe unlock strings classified using the native `default`, `null`, friendship, skill,
  and television/level token grammar;
- 19 Grandpa scoring criteria totaling exactly 21 points;
- exact IL/source identities for `Utility.getGrandpaScore` and
  `Utility.getGrandpaCandlesFromScore`;
- all ten distinct live score-input envelopes readable with provenance;
- zero goal-dependency blockers.

The policy target is all 21 available points. The four-candle threshold at 12 points is
recorded as an intermediate milestone, not substituted for the target.

`handler-semantic-surfaces.json` normalizes those operation rules into may-read, may-write,
external side-effect, random-source, and runtime-boundary records. The current profile has
109 predicate surfaces, 101 command surfaces, and 11 unique data-method surfaces. These
counts overlap when one native method serves more than one handler family.

`authoritative-dependency-graph.json` joins direct runtime-data dependencies, map topology,
access constraints, terminal goals, and native rule bindings. The current graph has 35,335
nodes and 41,262 edges. Conditions, event preconditions, event commands, data methods, maps,
map warps, map interactions, shop endpoints, NPC schedules, recipe/bundle relationships, and
Grandpa score criteria are graph members rather than detached audit lists.

Every referenced item, category, context tag, currency, and dynamic method has an explicit
typed reference node. The graph contains zero duplicate node IDs, duplicate edge identities,
or dangling edge endpoints; graph construction now fails immediately if closure regresses.

`knowledge-completeness-ledger.json` keeps three claims separate:

- authoritative identity graph: complete, with zero blockers;
- native runtime executability: complete for all exported condition/event/data-method tokens;
- predictive semantic closure: pending 235 context evidence records.

The 235 pending records are explicit and non-overlapping by category: 18 scene-specific field
observations and 217 native operation rules with runtime dispatch, reflection, or random-state
boundaries. Map projection has zero pending records. The remaining records are not reported
as missing fields, and they are not silently promoted to static formulas.

Resolution proves exact identity and static operation surfaces, not branch meaning. Runtime
values, virtual dispatch targets, random outcomes, formula return semantics, and side effects
hidden behind reflection remain context-bound until a semantic record or runtime observation
closes that boundary.

## Downstream capability join

The locked 1.6.15 native action denominator is now represented by three generated evidence
layers:

- `native-action-surface-inventory.json` identifies 320 native input surfaces by full method
  signature, source span, and body hash, including overloaded methods as distinct surfaces;
- `native-action-branch-inventory.json` expands all 60 broad surfaces into 428 source-backed
  branches, with zero missing surfaces, semantic-review branches, or missing registrations;
- `native-map-interaction-coverage.json` joins 1,102 effective runtime-projected map
  interactions into 150 Action/TouchAction tokens. 142 tokens map to native branches and
  semantic actions; eight are source-proven no-player-semantic, inactive, or legacy static
  tokens.

The semantic catalog contains 165 actions: 96 existing `OptionSpec` implementations and 69
explicit `catalogued_blocked` capabilities. This is a provisional source-denominator closure,
not an end-to-end execution claim. Existing action code remains the implementation baseline;
the blocked entries prevent known native capabilities from disappearing while their
read/candidate/compile/product-runtime/verifier/E3 gates are completed.

`StardewValley.Tools.Raft` is intentionally excluded from that semantic denominator. The locked
1.6.15 assembly retains the class and downstream `Farmer.isRafting` compatibility branches, but
the 37-row runtime `Data/Tools` asset has no Raft entry and the decompiled sources contain no
acquisition, factory, event, or external construction path. The native surface inventory keeps
the source artifact as `legacy_unreachable`; it is not a player action, candidate, or training
target unless a future locked game version supplies a reachable vanilla entry point.

The earlier `downstream-capability-matrix.json` audit joined the then-registered 89-option
baseline to executable downstream surfaces. The current registered count is 96 and is
governed by `action-implementation-reconciliation.json`; the figures below describe that
earlier downstream audit until the matrix is regenerated:

- 61 full-action options have explicit action-step compilers;
- 59 runtime option IDs have explicit production dispatcher branches;
- every compiled `executor.*` primitive is present in the runtime capability catalog;
- unknown runtime option IDs fail closed instead of falling back to a crop-maintenance no-op;
- zero catalog-level downstream blockers remain.

The compiler also blocks any full-action option that produces an empty step list. Mine
primitives, bomb placement, food recovery, ladder/shaft descent, mine exit, and shipping-bin
execution require their typed target/identity/preview parameters before entering the runtime
queue.

Daily-plan candidate capability is tracked separately from the option catalog. The current
catalog classifies 51 known candidate kinds: 49 compile into typed daily-plan steps and two
remain explicit implementation blockers. Supported quest objectives are converted upstream
to existing executable kinds, `quest_npc_interaction`, or `quest_drop_box_donation`; fallback `quest_candidate` and
`special_order_candidate` rows identify objective kinds whose binding is still absent. The
mining and volcano rolling-horizon envelopes compile through daily-plan primitives into
their corresponding runtime option IDs.

`sell_shop_item` now uses the native `ShopMenu.receiveLeftClick` inventory-slot branch. The
candidate gate reproduces native category-or-all-tags acceptance and applies the live
`sellPercentage`. Compiler and runtime gates recheck shop identity, item identity, complete
stack size, protection state, currency, custom callbacks, storage-shop mode, and exact unit
price before input. Money and inventory deltas are verified after the native menu action;
the executor never deletes inventory or credits money directly.

The material inventory graph now exposes each observed item's native maximum stack size.
`MaterialTransferProjector` accepts a typed source node, source slot, destination node, item
identity, quality, quantity, and expected source stack. It currently supports only one
player inventory and one uniquely observed, unlocked, ordinary placed chest in the same
location. Its projection follows the decompiled 1.6.15 native insertion order: fill compatible
stacks by slot order, then allocate empty slots by slot order. It reproduces
`Chest.clearNulls()` compaction for chest destinations while preserving sparse player
inventory slot indices. Source drift, lock state,
unsupported chest families, cross-location access, and insufficient destination capacity
fail closed.

This transfer projector is an executor prerequisite, not an executable capability. No
`executor.transfer_inventory_item` option, runtime dispatcher branch, mutex/menu state
machine, or runtime verification claim exists yet.

The zero option-catalog blocker statement therefore does not claim complete end-to-end
playability. Candidate generation, daily-plan compilation, action compilation, runtime
dispatch, and postcondition verification are separate gates.

## Transparent runtime boundary

`StardewAI.TransparentBridge` is a read-only observer. It does not apply controller settings,
simulate input, mutate `Game1.options`, or execute action commands. Recommended controller
settings remain observable metadata owned by the runtime controller.

The bridge capability manifest is generated from the adapters actually registered by the
state collector. Game and SMAPI assembly names, versions, module IDs, byte lengths, and
SHA-256 hashes are observed when available. This establishes binary identity only:
`identity_observed_unverified` is not promoted to compatibility verification without indexed
evidence for those exact hashes.

`event_stream.v2` and `event.v2` bind events to real snapshots:

- ordinary change events carry `observed_snapshot_hash` and
  `snapshot_relation=observed_after_snapshot`;
- `SnapshotPublished` carries the prior observed hash plus `published_snapshot_hash` and
  `snapshot_relation=snapshot_published`;
- the bridge never fabricates before/after hashes by hashing event labels.

The backend rejects legacy event schemas, unknown snapshot hashes, invalid snapshot
relations, duplicate capability IDs, and enabled command execution claims from the observer
manifest.

## Wiki verification

`wiki-verification-registry.json` pins reviewed Stardew Valley Wiki revisions for Grandpa's
evaluation, shop schedules, shops, buildings, events, festivals, recipes, and game-state
queries. Wiki evidence is secondary. Pages that postdate 1.6.15 or describe later versions
are format corroboration only; only handlers present in the indexed 1.6.15 assembly are
admissible.

## Remaining derivation stages

The exporter output remains immutable source material. The remaining compiler stages are:

- classify branch predicates, formula return values, mutation meaning, random branches, and
  context-dependent virtual targets for the already bound operation surfaces;
- generate a matching Windows runtime-semantics profile before using the Windows client DLL
  as training/execution evidence; never reuse the Linux handler tokens by version string;
- extend the same verified argument-role extraction used for progression instructions to any
  remaining non-progression command roles needed by planning, without guessing opaque rules;
- bind ordinary-quest and special-order objectives to existing domain candidates, then add
  only the terminal native interactions that cannot be expressed by existing executors;
- acquire scene-specific snapshots for the 18 contextually unavailable fields and fail closed
  on any new modded handler or asset that lacks evidence.
