# Knowledge Artifact Layout

Large, immutable game evidence is stored outside the source repository. The default Windows
root is:

`I:\StardewAI-KnowledgeArtifacts\game-1.6.15`

Set `STARDEWAI_KNOWLEDGE_ROOT` to override that location. The checked-in
`knowledge-artifacts.lock.json` pins every input needed to identify the current authoritative
profile without committing game assets, snapshots, binaries, or generated dictionaries.

## Layout

```text
%STARDEWAI_KNOWLEDGE_ROOT%\
  raw\game-1.6.15-20260723T093543Z\
  derived\game-1.6.15-20260723T093543Z-linux-v21\
  runtime-binaries\linux-server-1.6.15-20260719\
  snapshots\live-full-snapshot-20260719.json
  snapshots\current-live-full-snapshot.json
  snapshots\current-live-full-snapshot.metadata.json
```

The dated snapshot is immutable historical evidence. The `current-*` pair is a replaceable pointer
plus provenance record installed only through `scripts/Install-LiveSnapshotSchema.ps1`; the
installer validates every distinct registered required state factor, stages all replacement files,
then updates the pointer and checked-in snapshot/metadata hash lock. Default reconciliation fails
closed when that pointer is missing or differs from the checked-in lock. A rejected candidate writes
only the candidate validation report and cannot overwrite current evidence.

The matching decompile tree remains separate at
`%STARDEWAI_DECOMPILE_ROOT%`. Its default Windows location is
`I:\StardewValleyAICompanion-decompile-linux-server-1.6.15`.

## Integrity Rules

- Treat `raw` exports and runtime binaries as immutable evidence.
- Rebuild `derived` profiles from pinned inputs; never hand-edit generated JSON.
- Verify the lock-file hashes before accepting a copied or rebuilt profile.
- Never copy a live snapshot over the current pointer manually; use the installer and require
  `blocking_count=0`.
- Do not infer Windows runtime semantics from the Linux profile even when the game version
  string matches.
- Keep repository-local `data/knowledge` copies only as temporary migration sources. They
  are ignored by Git and are not authoritative once the external copy verifies.
