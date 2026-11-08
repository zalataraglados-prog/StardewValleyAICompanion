# Typed Native Inventory Transient Observation

## Parent And Scope

PR544 finalec4e3669 passed42-step CI42505475775 (Core2595/Backend1662), merged
as62f7bfe1 with equal treeec46012a. Its proposed universal currentTool/selected-slot
rule was disproved by seven actual native getter cases; the reply and three
regressions are retained. Only its owned feature refs were removed after tree checks.

This is the next component of the SAME original refill observation, not a second
executor, collector, ledger or training gate. --observe-native-inventory-transients
requires the original inventory-payload/tool-input/anchor/output chain, isolated
singleplayer harness, full snapshots, feedback and skip-training. New fields are
omitted by default; ordinary gameplay and snapshot cadence are unchanged.

## Native State And Raw Semantics

Actual E:1.6.15 DLL SHA256 remains
7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee.
The same external API session refreshed bounded Item/Tool/Axe/Pickaxe C# sources,
classified fields, then supplied two helper files eebbc53d (integrated806ec63f;
rebasing the private feature onto accepted main preserved its exact tested tree).
Their supported item domain is the existing exact21-type catalog/native assembly,
not an arbitrary namespace or reflective object graph.

The helper reads Item.SetFlagOnPickup and raw tempData, qualified-ID/tag-cache
fields, Tool.lastUser/PlayUseSounds/description/display caches, and all three signed
private Pickaxe boulder counters. Nine cached FieldInfo declarations require exact
declaring class, native field type and nonstatic layout. Unreadable is not zero/null.
No Item/Tool construction, clone/getOne, serializer, registry or cache-filling item
getter is used. Cache classification is not permission to hide persisted identity
changes as cache fills or to assume every transient is resource-neutral.

Null strings, empty strings, absent containers, allocated-empty containers, false
and zero remain distinct. Nullable-text wrappers explicitly mark observed null;
missing wrappers/availability reject. Dictionary order is retained and entries are
detached immutable null/bool/string variants. Other value types reject explicitly,
without user getters/ToString or fabricated defaults. Native call-site search found
FromStarterGiftBox/IsBossFish booleans and __Inventory_ReduceId_Remove string; the
codec does not infer meanings or restrict key names. Raw null HashSet tag elements
are preserved separately from absent/empty sets, not normalized away.

The JSON-string transport requires well-formed UTF-16. A lone surrogate otherwise
silently becomes U+FFFD during System.Text.Json serialization, changing the observed
native value. One shared pure code-unit check now rejects malformed raw flags,
keys/values/tags/caches at the producer and consumer, rather than recording normalized
success. Valid control characters, CJK and supplementary pairs remain exact. This
is an explicit unsupported transport case, not a claim of arbitrary CLR text/mod
compatibility. The guard changes no fields, encoder settings or gameplay methods.

Containers have256-entry bounds, keys/tags256 characters, other strings4096.
Each item's raw transient strings total at most64Ki characters, sharing the
original1Mi total across old admission/debris XML, both slot XML arrays AND both
transient endpoints. Fixed kind tags, scalar numbers/bools and bounded reference
headers are not counted as raw text. A missing per-item consumer cap was first
reproduced with a failing negative, then fixed; it cannot borrow other slots' caps.

The original adapter captures raw caches BEFORE virtual item headers and XML,
using the same remaining counter. A null backing itemId is rejected before a
mutating legacy migration could occur; no observation silently repairs that field.
Endpoint/reference/actor/index/capacity/tick checks remain in the original scope.

## Selection And Physical Attribution

Selection retains actual Farmer._itemStowed, TemporaryItem, CurrentItem and
CurrentTool references. Native precedence is temporary item first, otherwise
stowed=>null, otherwise selected slot. The consumer checks this exact branch and
the resulting tool cast rather than universal selected-tool equality.

The original ReferenceIds namespace binds item, actor and lastUser physical
identity. Same reference cannot switch CLR type across either endpoint, including
Farmer/item collisions; lastUser's IsActor relation must match physical identity,
and when true its numeric actor ID must match the original scope actor too.
Textual actor ID alone never replaces reference binding. Tool and Pickaxe fields
are required only for their actual types and forbidden on inapplicable rows.

This reader validates observations, not every native transition. Tool.lastUser may
legitimately change during DoFunction, pickup flags can affect offered/merged items
and mail, caches can fill, and private boulder counters are action-local. No blanket
before/after equality, swing estimate, counter clamp or resource credit is added.
Separate original tool-debit validation remains mandatory; request flags are not
proof that another consumer ran. Out-of-inventory temporary-item payload coverage
is not enlarged by recording its branch reference; old inventory scope remains.

## Verification And Limits

Peer's exact two-file helper passed178 bounded native-CLR fixture checks and one
normal E harness build. These constructor-bypassed fixtures cover all21 types'
NEW subset/applicability, private layout/limits/raw variants/detachment and nine
selection scenarios; they are not full subtype/live-game or world proof.
Same-peer mutual source integration review found no new actionable coupling issue.
Final focused Backend267/267 and Core37/37 pass, including the two additional
physical-reference regressions. All four normal Release components
build with zero warnings/errors. The per-item cap's failing regression is retained.

One hidden/silent copied-save calibration, native-inventory-transients-a, completed
the original clear and move. Test-only probe (never shipped) measured22 item helper
and4 selection helper calls: all26 passed both RNG-reference/full-supported-state,
bounded header/reference/tick and raw cache/temp-data/selection/private-field
unchanged checks. No gameplay method/input was patched; only test-target helper
prefix/postfix/void-finalizers observe without replacing results/exceptions.
Source save SHA68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d
was unchanged and owned game/backend processes exited.

Both retained records pass original source/quantity/tool/payload/NEW transient
consumer and one-read anchor-client replay2/2 (report-b). All endpoints retain12
slots. Actual tool actor-users changed1->2 in clear, then2->2 during move; observed
Pickaxe counters were0/0/0, not synthesized absent fields. Actual selections were
unstowed/no temporary override; rare branches remain controlled-fixture evidence.
Both scopes recorded0 collection callbacks: this calibration does NOT establish
pickup/production attribution or close the inter-action passive gap.

The first external replay was accidentally launched while its build handle was
live, used an older binary, and caused one copy-retry warning. Feature-marker checks
rejected that result; after terminal build, report-b explicitly exercised the new
reader2/2. Attempts/logs are preserved; no game was rerun. Future consumers must
wait for terminal build handles and verify feature presence, not a stale pass count.

Remaining subtype-specific fields, offered-item/mail/world effects, exact producer
origin, before-capture/passive gaps and full resource conservation are unproved.
All full-transient/native-outcome/resource/formal-training flags remain false.
No model training, server deployment, frozen L1 rerun or local-data commit.

## Required Successor

PR545 is accepted as main87e1f1ef after final head1d1faa0a passed42-step
CI42509339649 (Core2596/Backend1726), equal treec024778f. A bounded metadata-only
query of the pinned DLL now indexes21 roots/44 enchantment types/67 selected types/
477 unique declared fields (243 static/234 instance).477 is not an unimplemented
field count; wrapper/persistence/resource meanings remain explicit unknowns.
The initial resolver facade collision and corrected separate index are preserved
externally, with no native constructors/getters/values or game run involved.

Text guard verification: all65536 single UTF-16 code units compared with actual JSON
round trip, valid controls/CJK/pairs, seven malformed consumer field families and40
targeted native-CLR producer checks pass. Related Backend275/275, four Release
components and replay build pass; current stricter reader reuses retained native
records2/2. No old178 matrix or native game calibration was repeated.

Complete remaining relevant subtype transients and world/producer/interval evidence
on this SAME trace before joining the existing single persistent owner. Then harvest/
native save/day and bounded managed integration; original global calendar/budget/
cross-root/probes/Provider/model/manifest gates follow. L1 stays19/19 and11/11;
Student21/21 is post-training, not a local acceptance prerequisite.
