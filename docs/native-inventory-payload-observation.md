# Native Persisted Inventory Slot Payload Observation

## Scope And Identity

This extends the original refill move/clear observer, not an executor or a parallel
resource ledger. PR543 accepted main72dc6d1a after final65d8a0a4 and42-step
CI42496580856 (Core2594/Backend1595); accepted tree475c17f0 matched. Prior tool-input
observations remain separate and all resource/formal-training flags remain false.

Explicit --observe-native-inventory-payload requires the original tool-input,
same-frame anchor and native-output modes, full snapshots, skip-training, feedback
and singleplayer harness. Default flags/optional fields remain omitted. No ordinary
tool, movement, animation, timer, startup patch or per-frame world snapshot changes.

The original observer retains complete slot positions, including empty slots,
actual session reference IDs, CLR types, qualified identities, raw quality/stack,
selected slot/item, current tool and endpoint ticks. It serializes the LIVE instance
through the old capped TextWriter overload; no clone/getOne, construction, registry
normalization or changed bytes/hash. Persisted XML is not full transient state.

One game-free NativeInventoryPayloadTypes declaration binds producer and consumer
to the same21 vetted exact native runtime types, roots, tool flags and qualifiers.
Harness additionally requires the actual native assembly and typed graph edges.
Object/ColoredObject use bigCraftable; Clothing uses its actual clothesType scalar;
Ring/CombinedRing/SpecialItem have fixed(O), not Object's bigCraftable requirement.
MeleeWeapon/Slingshot use(W), ordinary tools(T). Chest/Furniture/Sign/Trinket and
custom/unvetted graphs remain explicit unsupported failures, never silently omitted.

The pinned E:1.6.15 DLL SHA256 is
7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee.
Native Item.GetItemTypeId/QualifiedItemId and admitted TypeDefinitionId overrides
establish prefix rules; actual native-CLR serializer fixtures establish the XML
roots and Clothing SHIRT/PANTS/unknown-enum scalar shape. Constructor-bypassed
fixtures do NOT initialize auto-property backing fields: fixed qualifiers are
source-derived, not newly executed ordinary native constructors.

## Boundaries And Shared Policy

Graph checks precede serialization: exact admitted Item types, native enchantments,
known Object/Tool/CombinedRing child edges, ancestor cycle rejection,16 levels,
256 serialized occurrences/collection entries. Repeated aliases count again because
XML expands them. DTD/external resolution are disabled; item XML is capped64KiB,
depth64 and8192 reader nodes/attributes. The existing1MiB total budget is shared by
old debris/admission payloads AND both inventory endpoints. No partial capture pass.

Both endpoints bind to the original actor/location/tick, complete aggregate inventory
denominator, contiguous slot indices, selected reference and recorded current tool.
Same physical GUID cannot alias two slots or change CLR type across endpoints.
The consumer retains immutable detached values and exact XML text/hash; malformed,
missing, unsupported, inconsistent or over-budget evidence fails closed.

CaptureWithWriter shares ONLY the previous serializer/hash mechanics. Old Object-only
debris/admission policy, nested-graph rejection, caller-owned cap writer and failure
diagnostics stay in their original caller. New inventory graph/XML/domain policy
does not broaden or tighten those old callers. The original tool-input consumer
must still validate scalar invocation evidence separately.

## Evidence And Limits

Same resident external API peerfe74fbbc (integrateddcfafcb4) supplied the helper;
Codex owns lifecycle/wire/client/consumer/integration. Mutual review found two real
consumer holes: arbitrary namespace types and matching-but-wrong qualifiers. Six
negative fixtures first failed (no rejection); the shared native catalog and exact
identity rules fix them. The same API session produced the bounded source map and
exited successfully after renewed authorization; no internal substitute was used.

Initial helper123, related Backend198 and Core36 passed. Expanded constructor-
bypassed native-CLR fixtures171 pass, including all21 roots/tool classifications,
Clothing scalars and previous cap/hash/graph/security checks. Diagnostic failures
from uninitialized fixture qualifier fields and an initially assumed clothing
wrapper were preserved; actual serializer output corrected only the new reader.
Final focused Backend200/200 and Core36/36 passed; four component Release builds
and the retained-replay build had zero warnings/errors. The final stricter consumer
again accepted both retained native records2/2, writing a separate report rather
than overwriting the earlier replay. Remote CI/publication are recorded separately.

Silent isolated payload-a retained23 actual shared-helper calls: Axe/Hoe/WateringCan/
Pickaxe/MeleeWeapon each1 cold+3 warm, Object3 warm (no cold Object observed).
Test-only probe checked both game RNG references/full supported fingerprints,
bounded item/player/reference/tick invariants, and exact old direct-serializer XML/
hash equivalence. All23 passed. It adds no shipping mod/hook or production claim.
Original clear and move records passed source/quantity/tool/payload/current-client
replay2/2;12 slots each endpoint, real Object pickup included. Source save unchanged,
owned game/backend exited. Consumer fixes reuse these records, not another game run.

This does not prove all item transients, complete metadata member census, nested
physical reference identity, whole-world conservation, producer provenance,
before-capture/passive-gap continuity, or all admitted families in a live save.
No resource credit, owner admission, native outcome or formal-training authorization
is granted. L1 remains frozen19/19 and11/11; Student21/21 is post-training.

## Required Successor

Continue this SAME refill trace with remaining transient/world effects and producer
provenance, including before-capture/passive gaps, then join the existing single
persistent owner. Harvest/native-save/day and bounded managed integration follow;
then original global calendar/budget/cross-root/probes/Provider/model/manifest gates.
Do not reopen frozen L1 or introduce another orchestrator. Raw evidence/test mods/
training artifacts stay external and local-data is never committed.
