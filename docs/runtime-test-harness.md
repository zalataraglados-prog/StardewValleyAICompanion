# Runtime Test Harness

## Mod isolation

Runtime evidence is valid only when the launched SMAPI process loads the
explicit mods required by the scenario. A separate test client in the normal
`Mods` directory may patch game mechanics even when its HTTP API is unused.
For example, `JunimoTestClient.GodTool` changes tool power and clump health.

New smoke scripts must set `SMAPI_MODS_PATH` to a run-specific directory,
copy only their declared allowlist, and record that path and allowlist in the
summary. Evidence from a process that loaded undeclared gameplay patches is
rejected rather than calibrated into the executor.

`StardewAI.RuntimeTestHarness` is a test-only SMAPI mod for runtime acceptance. It is not part of `StardewAI.TransparentBridge`, does not appear in Bridge capabilities, and must not be used by the AI action compiler.

Purpose:

- Redirect `StardewValley.Program.GetSavesFolder()` to an isolated directory such as `E:\StardewValleyAICompanion-runtime\saves`.
- Optionally call `SaveGame.Load("<slot>")` after the title screen starts so runtime tests can enter a copied test save without keyboard or mouse input.

Environment overrides:

- `STARDEWAI_TEST_SAVES`: absolute isolated saves directory.
- `STARDEWAI_TEST_SLOT`: save folder name to load, e.g. `自动化_442159967`.

Safety boundary:

- This harness changes runtime state by loading a test save. That is acceptable only as an external test driver.
- `StardewAI.TransparentBridge` remains observer-only with `can_write_game_state=false` and `can_execute_commands=false`.
- Do not install this harness into a normal play profile.
