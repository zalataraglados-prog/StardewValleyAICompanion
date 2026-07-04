# Runtime Test Harness

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
