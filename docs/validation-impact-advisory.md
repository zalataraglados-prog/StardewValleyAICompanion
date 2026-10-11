# Validation Impact Advisory

Issue #198 adds a conservative local first-feedback selector. It does not change `.github/workflows/quality.yml`, prove evidence reuse, skip a gate, or execute a native replay.

## Usage

Inspect staged and unstaged tracked working-tree files:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File ./scripts/Get-ValidationImpactAdvisory.ps1
```

Inspect a committed range:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File ./scripts/Get-ValidationImpactAdvisory.ps1 `
  -BaseRef origin/main -HeadRef HEAD `
  -Output artifacts/validation-impact/advisory.json
```

Explicit paths can be passed with `-ChangedFile` or one repository-relative path per line through `-ChangedFilesPath`.
New untracked files are intentionally not discovered by the default mode; pass them explicitly so unrelated local data is never swept into analysis.

The JSON contract reports changed files, impacted domains, recommended builds, test filters, source guards, native replay candidates, rule-level reasoning, and uncertainty. These fields are advisory only. Every output fixes:

```text
full_quality_still_required=true
evidence_reuse_authorized=false
native_replay_execution_authorized=false
```

Unknown paths, shared runtime script libraries, repository build configuration, workflows, project files, and package locks fall back to broad hosted-quality-equivalent checks. Bridge adapters and runtime executor changes recommend a representative native replay but never launch one.

Behavior tests replay representative file sets from PRs #186, #187, #192, #194, and #196, plus docs-only and unknown-path cases. Final PR admission remains the unchanged hosted `game-free-quality` workflow.

The selector overhead and all representative mappings can be measured without builds or game startup:

```powershell
Measure-Command {
  ./scripts/Test-ValidationImpactAdvisory.ps1
}
```

This test runs all scenarios in one PowerShell process so the hosted game-free suite does not pay one process startup per mapping case.
