# Native Inventory Graph Observation

Status: controlled native calibration and current-consumer replay passed; final
regression/publication recorded in current work. This does not admit full-state
observation, resource credit, or formal training.

## Scope And Reuse

`--observe-native-inventory-graph` is opt-in. It requires the existing native
inventory-transient/persisted-payload/tool-input/snapshot-anchor/output chain,
full snapshots, skip-training, the runtime harness and isolated single-player
execution. Old requests omit the new flag and retain their existing behavior.

The implementation extends the ORIGINAL `NativeLiveItemPayloadCapture.GraphCheck`
with optional callbacks. It does not add another production native walker,
executor, reference-ID namespace, or XML parser. The default no-observer legacy
admission and diagnostics remain intact. The existing raw Item helper, field
codec, Object subtype sidecar, capped writer and physical reference IDs are reused.

The admitted domain is the existing 21 Item types and the exact 44 concrete native
enchantment types in shared contracts. These counts are NOT action progress or a
claim that all vanilla/mod item families and subtype fields have been captured.

## Read Order And Wire

Within the original bounded inventory scope:

1. Copy the actual root references and capture ALL admitted root/nested raw Item
   states and enchantments before virtual headers or XML serialization.
2. Capture the actor's actual ordered enchantment membership, including reference
   aliases and null positions. Membership-only enchantments are nodes too.
3. Read headers and serialize the original roots through the existing writer.
4. Map all roots, nodes, child edges and actor members through the SAME physical
   reference-ID map. No native `Equals`, `GetHashCode` or inferred ownership.

Root raw Item state stays in the original inventory slots; graph root nodes MUST
omit duplicate `ItemState`. Nested-only Item state is carried once per unique
node. Enchantments retain raw `_applied`, nullable `_displayName`, and signed NetInt
`level`. No display-name/Level/Apply/Equip callback is used for observation.
`_applied` is not proof that an enchantment belongs to this actor; actual actor
membership is a separate vector. Caches, nulls and signed values are not repaired.

Edges preserve physical parent/target references, role, ordinal and explicit null
availability. Actual native child families are Object heldObject/lastInputItem,
Tool attachments/enchantments, CombinedRing children and Hat enchantments.
Hat's persisted list exists even without vanilla usage call sites; observing it
does not make a Hat a Tool or grant it Tool raw state. Its new traversal branch
runs ONLY with the graph observer, leaving legacy behavior unchanged.

## Bounds And Validation

The BEFORE and AFTER endpoints SHARE total limits: 4096 non-null node occurrences,
8192 edge occurrences and the original 1 MiB raw-text/XML budget. Unique tables
do not discount repeated aliases or serialized visits. Null edge/membership
positions count as edges. Existing per-root depth16/occurrences256 and collection
size256 remain; roots are bounded512. No truncation or partial-success claim.

The consumer retains parsed XML from the original inventory read, then validates
role/type/scalar/header/raw-state binding, exact ordinals/counts, null structure,
reachability, cycles and recomputed occurrence totals. It rejects orphan/duplicate
nodes, fabricated reference availability and incompatible role types. Parsed XML
and root state are neither parsed nor budget-charged a second time per alias.

Native XML details matter: null single held/input children are omitted; attachment
and ring arrays preserve explicit nil children; repeated enchantment elements
OMIT null native positions. The graph preserves those positions independently.
Actor-only membership has no fabricated XML occurrence. The native source/schema
checks and pinned 1.6.15 DLL, not illustrative documentation, establish this mapping.

## Evidence Boundary

- Original API helper commit `b8f637aa` was integrated as `1337e05b`; contracts are
  `d5f12bd3`. Its fixture failure was a missing NotSupportedException rejection
  filter, corrected by the controller without changing production helper behavior.
- 83 focused native-CLR checks passed. Actual metadata exposed Hat.enchantments;
  a separate six-check native-CLR Hat delta passed. They are not one live-game run.
- Initial77 boundary/client checks, final331 related Backend checks and six
  source-guard checks passed; current normal E
  harness build had zero warnings/errors. One same-session API integration review
  found no actionable new coupling issue; a review is not native runtime proof.
- External helper-only probes plus an explicitly opted-in ONE SaveLoaded fixture
  ran in a copied save. The original API peer supplied the one-file native fixture,
  one successful compile and source notes; Codex reviewed, wired and ran it. No
  observer/executor behavior was replaced. Fixture artifacts are NOT earned state.
- `output-client-native-inventory-graph-a` completed the ORIGINAL tree/move chain.
  Four native graph samples and four native EfficientToolEnchantment raw/return
  samples passed. Graph nodes were13/13 then14/14; unique edges14/14 then16/16.
  Actual repeated visits were19/19 then20/20 nodes,18/18 then20/20 edges; all six
  child families, actual bait/null attachment, repeated Hat/null slot, held/input
  alias, ring alias and actual two-position actor membership were exercised.
- Both records passed the CURRENT strict graph/Object/tool/quantity/XML/anchor
  reader and single-cache-read client replay (`inventory-graph-snapshot-replay-a`).
  The controlled BEFORE pattern was required, so zero/empty graph coverage could
  not be promoted. Each endpoint had three serialized enchantment occurrences.
- Existing XML and raw Item/selection probes each passed54 samples, zero rejected;
  measured native references/raw state and bounded RNG/header fingerprints stayed
  unchanged. This is bounded observed purity, not a universal purity/performance
  proof. Producer/interval/owner gaps are not filled by these counts.
- Original save SHA stayed
  `68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d`;
  game/backend exited. Native DLL SHA was
  `7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee`.
  The run used one logical processor, hidden rendering and dummy/null audio.
  External raw logs, save fixtures and replay artifacts remain outside Git.

`InventoryGraphSubsetVerified` describes only this validated graph boundary.
FullTransientStateVerified/NativeOutcomeVerified/ResourceCreditAuthorized/
FormalProductTrainingAuthorized stay false. Raw/header/XML interval atomicity,
all subtype fields, arbitrary private state, unsupported families, whole Farmer/
world state, producer provenance and before-capture/passive gaps remain open.

After this block: preserve the existing world/producer/interval work order, then
the single resource owner, harvest/save-day continuity and managed native samples,
global coordination and remaining Provider/model/manifest gates. L1 remains frozen
19/19 criteria and 11 roots. No new model training or `local-data` submission.
