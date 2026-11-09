# Native Debris State Observation

## Scope

`--observe-native-debris-state` is opt-in and requires the original full
inventory-graph/tool-input/snapshot-anchor observation chain. It adds a raw
`native_debris_state.v1` sidecar to the SAME before/after/collection records.
The default path, original executor, movement, native collection/admission and
old unsupported-held-item guards are unchanged. It is not an action registry,
resource owner, producer inference or formal-training authorization.

The native image is Stardew Valley 1.6.15, SHA256
`7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee`,
module `aa1f513a-94f0-4ef6-a35f-2d5b4a3721bf`. The seven Debris enum values and
43 nonabstract Character runtime types come from that image's metadata, not
documentation examples. Unknown images/layouts/subclasses fail explicitly.

## Raw Boundary

- Identity: enum/variant, nullable item ID, quality, opaque held Item reference,
  dropped-by player ID, independently recorded target-defined and stored target
  UID, and opaque hover Character reference.
- Motion/visual: actual private/public scalar fields, counters, flags, packed
  colors, nullable message/sheet strings and texture-backing presence. Every
  floating-point value is persisted as its original signed 32-bit pattern.
- Logical Chunks: physical reference, velocities, bounce/wall/resting state,
  sink/random-offset counters, sprite coordinates, rotation, scale and alpha.
- Position: physical reference, underlying NetVector2 bits, private paused flag,
  moving/extrapolation/interpolation-wait flags, ordered incoming/outgoing pause
  records with physical references, bool arguments and uint timestamps, plus
  invocation-list counts without invoking or interpreting delegates.

`NetPosition.Value/Get/Paused` may poll queued events or cancel interpolation.
The reader uses the actual `Field.Value` and private paused storage instead.
`NetFarmerRef.UID` hides a stored UID when undefined; the reader preserves both
fields independently without actor lookup. No native Debris/Chunk construction,
RNG calls, ItemRegistry, serializers, clone, texture loads or callbacks occur in
the production raw reader. Chunk constructors consume RNG and are forbidden.

## Reuse And Limits

Private-field/type/text checks reuse `NativeLiveTransientFieldCodec`, extracted
from the original Item reader with its original private forwarding entrypoints
and exceptions retained. Consumer parsing reuses the original graph/XML/anchor
chain and physical-reference map, not a second Item graph walker.

One original scope shares its character, logical-chunk and pause-occurrence
budgets across both endpoints AND collection callbacks. Text remains at most
4096 UTF-16 units per field and 1 MiB aggregate together with original graph/XML
payloads. Chunks remain at most 4096 across the original scope. Every queue
POSITION counts, including repeated event/position/chunk aliases: at most 256
per queue and 4096 aggregate occurrences; each handler count is at most 256.

The helper separately bounds physical backing storage to 4096 entries. Native
shrink-list holes are skipped in native logical order and do not become fake
chunks or logical-budget charges. Larger historical sparse capacity is outside
this declared bounded domain and is rejected, not called complete coverage.
The raw helper can preserve repeated physical chunks, but the ORIGINAL receipt
consumer still rejects repeated Chunk IDs. This older admission boundary is not
relaxed for the new sidecar; its narrower accepted domain stays explicit.

Returned vectors/scalars are detached and immutable, while physical references
remain observation-scoped. Missing is not false/zero/null. The consumer binds
old identity/chunk order/held availability and checks aliases and budgets before
returning its explicitly limited summary. No sorting or normalization is used.

## Verification And Remaining Work

The original API peer supplied three helper files in `efa57fbf`, integrated as
`24ec3414`. Its one focused external native-CLR fixture passed 95 checks on its
first build/run, including a queued native Paused-getter trap, alias/order/null
and exact-budget cases. The trap deliberately calls the forbidden getter only
AFTER capture to show that it consumes the queue unlike the raw reader. This is
controlled CLR evidence, not live-world or ambient-RNG proof.

The controller owns the original adapter, consumer/CLI and native calibration.
Related Backend378/Core10 tests passed. The hidden/silent original tree/move
calibration produced 48/48 unchanged raw samples over 107 Chunk occurrences,
including 17 native collection callbacks in move. Current strict source/quantity/
tool/XML/anchor/graph/raw replay passed 2/2; the original save was unchanged and
both owned runtime processes exited. Actual pause queues were empty; nonempty
queued-state behavior is CLR-only evidence. Final CI/publication is recorded in
`CURRENT_WORK_CN.md`;
an existing old binary/report is not freshness proof. Live calibration must
produce nonempty actual Debris/Chunk samples and pass the current strict replay.

Network transport/interpolation internals, GPU contents, delegate closures,
held/offered Item raw graphs, other subtype families, producer ownership,
before-capture/passive intervals and the whole world remain explicitly unproved.
`full-state`, `nativeoutcome`, `resource` and `formal` stay false. Frozen L1 is
19/19 criteria over 11 roots; Student 21/21 remains a post-training objective.
Raw native sources, fixtures, saves and reports stay outside Git/local-data.
