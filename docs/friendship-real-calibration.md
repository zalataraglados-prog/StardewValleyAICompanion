# Friendship Real Calibration

PR #455 passed all 38 CI steps at final head
`76054c4d84f34391c9c9331cdd6eea67020b50c9`, merged as
`a699a3e902c293d7b8920bc41dd3f43baf08394f`; its branch is removed.
The independent comparison/dispatch consumer is not being rebuilt. The sole
primary remains `raise_friendships`, with the full final scope unchanged.

## Original Intent And Narrow Repairs

The calibration script originated in `55dc054fee8603f885e830c0abe74a6f870dd90d`.
A bounded retained-JSON search recovered the September 8 commit/convergence
report, not the detailed calibration discussion. Its original code explicitly
measures action milliseconds and conservative route/connector bounds. On this
PowerShell, decoded timestamps are DateTime objects; stringifying before parsing
lost fractional seconds: a diagnostic 864.1976 ms interval became 0 ms. Direct
DateTimeOffset conversion preserves 864.1976 ms for decoded DateTime, original
ISO strings and DateTimeOffset inputs. Both movement and connector measurements
now retain ticks and reject nonpositive durations. The original 1-minute/tile
and 2-minute/connector bounds and native actions are unchanged.

The first actual attempt `friendship-real-calibration-20261106-a` loaded the
correct Farm 66,19 but stopped before measurements: the old entry required raw
`route_timing_ready_now=true`, while the actual executor-owned idle pause made
that field false. This is retained as a preflight refusal, not PASS.

The existing owned-pause predicate was introduced in `84b866ba`; its intent and
interop correction are documented in [the original pause contract](executor-owned-idle-pause.md).
The bounded JSON search did not recover that original conversation; do not claim
otherwise. The predicate itself is unchanged. A new public read-only forwarding
method and Bootstrap `audit-current-owned-planning-pause` reuse it after the
snapshot version/tick/hash checks. That audit confirms only the existing release
certificate, never current native readiness or successful calibration. The
calibration entry retains its compatibility/theoretical-bound checks and accepts
the original pause certificate only for its planning preflight. No bridge,
executor, pause-release, movement or control gate was relaxed or duplicated.

The entry also supports `-NoBuild` for explicitly prebuilt/fresh DLLs, propagates
the caller's RuntimeRoot/GamePath to deployment, and uses the original silent,
hidden, copied-save/two-mod setup. Current SMAPI was freshly decompiled: its
existing `SMAPI_MODS_PATH` support is valid, so that old isolation mechanism was
kept, not treated as a bug. SMAPI DLL SHA256:
`5e4c51bb3cd6616f5adcb804769dbb7c6872b2478fab786fb4ca00c72c7456bf`.

## Real Native Result

`friendship-real-calibration-20261106-b` passed with the E isolated
`自动化_442159967` source cloned under the run directory; original source slots
and normal playing saves were not used for execution. Three movement samples
covered 66 tiles. Both native building-door entry and step-warp exit passed.

- Game 1.6.15; SMAPI 4.5.2; exactly the bridge and harness mods loaded.
- Maximum observed movement cost: 0.290585 game minutes/tile, below bound 1.
- Maximum connector cost: 1.039970 game minutes, below bound 2.
- Movement ticks/duration: 260/4658.934 ms, 279/5072.028 ms and
  201/3661.368 ms. About 54.9-55.8 update ticks/second including action timing
  overhead is a cadence diagnostic, **not** rendered FPS or a visual acceptance.
- Source fingerprint before/after and after game shutdown:
  `4164c3aee9901fedbdd64ac63982e662daad82472401c602507feec265734205`.
  Original fingerprint function was reused from its parsed function AST for
  the shutdown recheck, without executing the script again.
- Original public `validate-route-timing` accepts the real summary against its
  original raw snapshot. The earlier real paused snapshot also passes the new
  certificate-only audit without rewriting its native pause/readiness fields.
- Owned game process exited; no model training, worker/API, server or cleanup
  operation occurred. Native measurements were sequential; no game was run
  alongside heavy local regression suites.

The complete 9-file run, 13.25 MiB, is copied and hash-verified outside Git at
`I:\StardewAITraining\friendship-local-20261106\real-movement-calibration-20261106-b`.
Its original artifact directory is retained too; no files were deleted. Directory
content hash is `677562185731e42e1d354443070f1eca83628fcf9419a67248d6629547923a91`.
Summary paths still identify the original run; the retained directory has the
same relative files and identical bytes.

| Original/retained file | SHA256 |
| --- | --- |
| `summary.json` | `e0c0e82fe173ab8ad9b9dbcd6c707c8ff11f3797fa07af2a00b81962b15aea3b` |
| `initial-social-future-snapshot.json` | `a2078c13ddfeb44670d868f9df956f1fde170a021f0a69cec396f9ef8c490f62` |
| `movement-results.json` | `27d0831e9cf009a67d6e31798a784506782f34faf559457cd5ac581c9950922d` |

Original-loader validation and the actual friendship read/intake are retained
beside it as `real-calibration-b-validation.json`,
`real-calibration-b-friendship-deficit.json` and
`real-calibration-b-friendship-intake.json` in the parent directory. These do not
claim a native NPC action. The real source has 40 native villager instances,
one qualifier, and current five/ten deficits 4/9.

## Evidence-Driven Next Step

The read-only independent intake of this actual snapshot with the real
calibration is correctly blocked: gift source count 64, legacy limit reached;
talk source count 42 and ranked count 30. No candidate was deleted and no NPC
action dispatched to obtain a misleading successful comparison.

**Next, add an explicit full-source offline social comparison intake while
preserving the original runtime's default 64-candidate budget.** Re-probe this
retained actual snapshot first, without relaunching the game or reacquiring the
calibration. Do not remove gifts, clear inventories or narrow the option pool to
evade this boundary. Then proceed with the new comparison/original compiler and
isolated native execution for same-run five/ten thresholds, original trajectory,
criterion corpus and coverage/reconciliation. Further real resource/route
tradeoffs remain visible, not fabricated preferences.

Core 1803, Backend 597 and the focused 35 ownership/timing checks passed with
zero skips; timestamp checks, explicit-E bridge/harness builds, PowerShell AST,
EVD-338 checkpoint and whitespace checks passed. Final CI results belong in the
PR closing record. Accepted union remains 9/19 executable,
9/19 split-complete, 9/19 ready and 7/11 roots, formal=false: real movement
calibration is not a friendship criterion or training sample. No raw evidence or
local-data is committed. R2, full new-save Teacher21/21, Student, perfection and
human-adaptation responsibilities remain unchanged.
