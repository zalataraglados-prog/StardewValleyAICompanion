# Native Output Same-Frame Snapshot Anchor

## Parent And Original Intent

PR541 accepted at mainfed58419, tested960ba359/tree43fd17e5, after
CI42484660012 all42: Core2552/Backend1529, zero failures/skips. Owned branch refs
were removed after equal-tree verification. No automated review submission arrived
at merge time; both engineers' changed code was reviewed by the integration owner.

The original6774d22b observer intentionally described its bounded capture interval,
not a later snapshot or complete resource provenance. This explicit default-off
extension closes the demonstrated after-capture gap without rewriting tools,
animation, movement, default capture, source gates or previously accepted evidence.

## One Existing Snapshot/Cache Path

Use --observe-native-output-snapshot-anchor only together with the existing
--observe-native-outputs, --skip-training, singleplayer harness/feedback and full
execution snapshots. The original request builder enables it only for observed
move/clear items. Old wire omits the false flag and null post-snapshot fields.

Bridge exports a public non-nested read-only SMAPI interface/wrapper. Entry records
the game thread, but startup/GetApi does not collect. An explicit read requires
that thread, ready singleplayer world, existing collector, training-mode1 and
run/isolation identity. It reuses RefreshSnapshotCache(full,false), checks the same
tick and original snapshot/environment identities, then returns metadata only.
Failures are unavailable with diagnostics, never invented hashes/ticks. The bridge
now references the existing lightweight RuntimePrimitives project; lock changes
only describe that dependency, not package upgrades.

Harness invokes this read WHILE its original observer is active, then finishes
capture on the same tick. Only an observed matching end tick publishes the existing
product_after_state_hash/game_tick pair. The bounded parser requires all13 metadata
fields, exact actor/run/isolation/version/profile/current tick, empty diagnostics,
false authorization flags, no duplicates/unknown fields and at most16KiB/depth8.

The loop's single shared after-snapshot implementation retains its old default
entry. Explicit expectation comes from the original DISPATCHED REQUEST flag, not
response field presence. The stronger path independently requires receipt end tick
equal to the cache pair, reads the existing exact full-cache protocol once, and
rejects missing/malformed receipt, unavailable pair or different cached frame.
It never falls back to a later frame, fabricates an admission or adds settle waits.
No second collector/cache/orchestrator, per-frame scan or gameplay control is added.

## Verification

- Peer API focused8/8 after controller resolved the missing project reference;
  no guard was weakened. Integration Core39/39, four native Release builds with
  zero warnings/errors, and client35/35 passed.
- One mutual peer review found two valid client P2 gaps: missing independent end-
  tick comparison and response-inferred mandatory mode. Both were corrected using
  actual request context/shared implementation; expanded client38/38 passed.
- Initial client9 failures were typed JsonValue<int> fixtures where the existing
  ReadLong expects long. Only new fixture literals were corrected, not old parsing.
- Silent isolated snapshot-anchor-tree-native-a completed original tree chopping
  and pickup with applied/observed results; movement exercised17 collections.
  Original source save hash stayed unchanged; owned game/backend processes exited.
- Both actual records pass the original native source reader and stronger snapshot
  inventory quantity gate. Current hardened client replays the same retained frames
  via exactly one expected-hash/tick read each. Capture/snapshot end ticks are
  2140/2140 and3038/3038. Native run predates client-only hardening; its unchanged
  records are reused, not recollected. Old2295 wood16 ->2297 wood17 failure remains
  preserved/rejected, not promoted or edited.
- Initial-head remote CI42489147413 passed all42 steps. Subsequent automated
  review4506531085 correctly found that the standard bridge deployment omitted
  its new RuntimePrimitives dependency; the native calibration's whole-directory
  copy had not exercised that sparse deployment list. The original4801b0aa stale-
  output guard remains intact: the dependency is now copied, independently checked
  for freshness and included among bridge build inputs. A regression failed before
  the fix; the four focused deployment source guards then passed.
- External snapshot-anchor-bridge-deploy-a exercises the actual deployment script
  with isolated source/output timestamps and actual built binaries: dry-run copies
  nothing, all five deployed hashes match, existing config survives and the public
  interop type loads. Newer dependency source and stale dependency DLL both reject
  before changing target files. This is hermetic deployment evidence, not a new
  game run or proof of arbitrary installed-mod compatibility. Final-head CI and
  resolution of the initial review are recorded separately at publication.

Raw reports/source hashes/binaries and API transcripts remain external. These are
calibration and protocol results, not all-probe purity, full slot metadata, world
mutation, producer origin or resource/training credit. Native RNG equivalence is
not claimed. Final-head CI/review are recorded at publication.

## Required Successor

Continue the SAME refill trace with actual native tool-call inputs, complete item
metadata/world effects and producer provenance, including before-capture/passive
gaps, then join the existing single persistent owner. Harvest/native save/day and
bounded managed integration follow, then original global calendar/budgets/cross-root,
remaining probes, Provider/model and manifest. L1 stays19/19 and11/11; formal=false;
Student fresh-save21/21 is post-training. No local-data or raw evidence is committed.
