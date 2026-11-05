# Native end-of-night transaction contract

Status: issue #381 was accepted and merged by PR #384 at `2bab1dc9`.
The former candidate procedure at the end of this document is historical,
not current unfinished work. The 2026-11-05 native-control compatibility repair
preserves that acceptance contract; its focused regression evidence and limits
are recorded in [the shared-executor audit](shared-executor-compatibility-audit-20261105.md).

## Locked authority

The authority is the pinned Stardew Valley 1.6.15 Linux-host decompile whose
assembly identity is recorded in `knowledge-artifacts.lock.json` and
`docs/authoritative-knowledge-dictionary.md`. On Windows the locked source root
is `I:\StardewValleyAICompanion-decompile-linux-server-1.6.15`; the similarly
named `I:\StardewValleyAICompanion-decompile` tree is not an authority for this
gate. Relevant methods are:

- `Game1._newDayAfterFade` (`Game1.cs:8665-8708`) increments the calendar and
  `Stats.DaysPlayed` before selecting a farm event.
- `Utility.pickFarmEvent` (`Utility.cs:4849-4864`) rejects wedding nights first,
  then returns `SoundInTheNightEvent(4)` when `DaysPlayed == 31`.
- `Game1.Update` (`Game1.cs:4462-4500`) finishes the active `FarmEvent`, applies
  its location changes, returns the farmer home, and only then calls
  `showEndOfNightStuff`.
- `Game1.showEndOfNightStuff` (`Game1.cs:9990-10040`) pushes a shipping summary
  first when shipped items exist, otherwise pushes a standalone `SaveGameMenu`
  when levels exist; it then pushes `LevelUpMenu` instances in reverse list
  order so they pop in the original `newLevels` order.
- `LevelUpMenu` (`LevelUpMenu.cs:113-122,190-201,550-558`) initializes
  `timerBeforeStart` to `250ms` with `informationUp=true`, rejects input while
  that timer is positive, and decrements it only from the menu's native
  `update(GameTime)` path.
- `ShippingMenu.update` (`ShippingMenu.cs:424-493`) owns an embedded
  `SaveGameMenu` for a positive-shipping night and cannot finish until that save
  menu reports completion.
- `Game1.PollForEndOfNewDaySync` (`Game1.cs:9918-9943`) destroys the new-day
  synchronizer and calls `currentLocation.resetForPlayerEntry` while the
  terminal `SaveGameMenu` or `ShippingMenu` is still the active menu.
- `GameLocation.resetLocalState` (`GameLocation.cs:8140-8361`) calls
  `checkForEvents` only when no active menu exists and no new-day synchronizer
  was present at entry. Therefore the terminal-menu reset cannot start an
  ordinary location event. `GameLocation.checkForEvents`
  (`GameLocation.cs:17816-17950`) evaluates ordinary data events when a later
  real location entry occurs.

## Native order and legal combinations

The legal composite order is:

1. calendar/stat increment;
2. optional `FarmEvent` or its own minigame/cutscene state;
3. queued ordinary and profession `LevelUpMenu` screens in native `newLevels`
   order;
4. `ShippingMenu` when the bin has items, or a standalone `SaveGameMenu` when
   it does not;
5. real save completion;
6. event/menu-free new-day control and a fresh planned action.

Consequences:

- A farm event after a level or shipping screen is not a legal vanilla order.
- A positive-shipping night does not expose a separate top-level
  `SaveGameMenu`; saving is embedded in `ShippingMenu`.
- A no-shipment night with levels exposes a standalone `SaveGameMenu`; a
  no-shipment/no-level night creates one directly.
- Level screens precede the shipping/save terminal menu. A test must not fake a
  shipping-first profession sequence.
- A later ordinary story event, dialogue, or minigame is a distinct handoff and
  must retain its own typed executor and fail-closed question handling.

This last point is a native boundary, not a test simplification. A location
story event cannot be inserted between the terminal save and the transaction's
fresh normal action by constructing an `Event` or manually calling
`startEvent`. The end-of-night transaction first proves a saved, event-free
next-day state and one normal planned action. A separate post-save handoff run
then uses a real connector/location entry and lets `checkForEvents` select the
event from `Data/Events/*`.

For the positive handoff, locked `Data/Events/Farm` event `63` is a deterministic
native candidate: its `y 2`, `t 600 1800`, and `H` preconditions are implemented
by `Preconditions.Year`, `Preconditions.Time`, and `Preconditions.IsHost`; the
earlier Farm events in data order can be excluded by their own unmet event,
friendship, or year preconditions. The fixture may establish those documented
preconditions, but must not construct an `Event`, call `startEvent`, write
`eventUp`, or mark event `63` seen. The real Farm entry must start it.
The fixture previews eligibility on the exact wake date, Year 2 Spring 2 with
`DaysPlayed == 114`, then sleeps from Year 2 Spring 1 with
`DaysPlayed == 113`. This keeps `DaysPlayed == TotalDays + 1` on both sides of
the native increment and prevents a sleep-date-only check from standing in for
the actual post-save entry date.

For unsafe-question blocking, locked `Data/Pets` maps Cat adoption to Farm event
`1590166`. `GameLocation.checkForEvents` only requests that event after at least
20 played days, no pet, no `rejectedPet` mail, and no prior event receipt; the
locked Farm event row also requires at least `1000g` lifetime earnings, a
06:00--09:30 Wednesday/Friday entry, sunny Farm weather, Cat selection and host
ownership. In the locked 1.6.15 decompile, legacy precondition `d` aliases
`NotDayOfWeek`, so the five names in `d Mon Tue Thu Sat Sun` are exclusions.
Spring 24 is rejected because the Flower Dance would preempt the
ordinary location event. The fixture instead sleeps from Spring 25 into Friday
Spring 26 with `DaysPlayed == 25` before sleep, at least `1000g` lifetime
earnings and sunny next-day weather. The
locked event script reaches native `catQuestion`. This is suitable negative
evidence only when those preconditions are established without constructing
the event or choosing a response. The controller must stop at the question and
emit `story_event_dialogue_response_mismatch` rather than adopting or rejecting
the pet.
Before returning the sleep fixture, the harness must preview the exact Spring
26 wake state with `DaysPlayed == 26`, current Farm weather set to sunny, and
the requested Cat type. The normal Farm data iteration must identify
`1590166` as its first eligible event; merely finding the row in the asset is
not sufficient. The preview applies both `checkEventPrecondition` and vanilla
`GameLocation.IsValidLocationEvent`, matching the two gates in native data
iteration. It includes the current shipping bin's native
`sellToStorePrice(-1) * Stack` settlement in projected lifetime earnings, then
returns lifetime earnings to the pre-settlement value before sleep so the
native settlement is not applied twice. A failed preview restores the date,
pet, earnings, and weather
values modified by the preview before returning a blocked result.

The deterministic special-event fixture uses the native legal boundary before
the earthquake: Year 1 Summer 2, `TotalDays == 29`, and `DaysPlayed == 30`.
Native new-day processing advances these to Summer 3, `TotalDays == 30`, and
`DaysPlayed == 31`; `Utility.pickFarmEvent` constructs the event. The fixture
does not construct a `FarmEvent`, `LevelUpMenu`, shipping menu, or save menu and
does not write event flags. Because vanilla checks weddings before the day-31
earthquake, the fixture rejects any engagement due by `TotalDays == 30` or a
married friendship whose native wedding date is exactly that day. This includes
roommate ceremonies: they don't set `weddingToday`, but the new-day path first
marks the relationship married, so `pickFarmEvent`'s subsequent married-date
check still wins. The fixture does not erase or rewrite social state to force
the event.

## Candidate invariants

- Preserve the historical fixes when adjusting shared callers: `989fa5f7`
  prevented executor-idle pause from freezing the native one-frame handoff
  between profession selection, remaining level menus and saving;
  `d33e3c72` prevented tent callbacks from reopening an unrelated terminal
  transaction. Their originating conversation on 2026-11-02 confirms these
  purposes. Neither protection may be removed to make a newer test pass.
  Before changing an apparently incorrect shared guard, inspect its introducing
  commit and the corresponding conversation JSON, then add a regression for
  both the original failure and the proposed compatibility change.
- Native control ownership ends once the exact saved, event/menu-free next-day
  ready state has been observed. The pending fresh-work proof must not block
  another home/tent sleep or disable the configured executor-idle clock pause.
  Both entry points and idle-pause ownership use the same RuntimePrimitives
  policy; a profession/menu/save transition still holds native ownership.
- The composite transaction remains incomplete until its existing registered,
  same-run fresh-work receipt is verified. If another valid sleep starts first,
  persist the previous proof as blocked with
  `end_of_night_followup_not_observed_before_next_sleep` before starting the new
  transaction. This is an unfulfilled acceptance proof, not a failed native sleep
  and never a synthetic PASS. Legacy callers need no new planning marker merely
  to retain their original sleep and idle-pause behavior.
- One transaction ID begins with the sleep operation, starts timeout accounting
  only after native sleep confirmation, survives the planned profession yield,
  and is attached through the terminal execution result exactly once. Later
  unrelated results do not inherit the completed or blocked transaction.
- The existing `skills.choose_profession` candidate, daily-plan compiler, action
  compiler, and native executor remain the only profession path. Stage One
  mandatory-menu preflight selects that explicit candidate when a profession
  chooser is present. Generic `recovery_close_menu` is unavailable on a
  profession chooser and carries no profession ID; it remains valid only for
  ordinary level screens. The one transient exception is the decompile-confirmed
  native `LevelUpMenu.timerBeforeStart` gate: when the transparent snapshot
  proves an active profession chooser with `information_up=true`, exactly two
  choice identities, complete reflection fields, `can_receive_input=false`,
  and a remaining timer in the
  native `1..250ms` range, mandatory recovery may compile one bounded
  `executor.wait_ticks` action of at most 30 ticks. That wait is control-only,
  does not append a policy trajectory or increment the verified-action target,
  preserves the exact skill/level/choice identity, and must obtain a fresh
  snapshot before the profession candidate can be selected. It never chooses a
  profession and cannot be replaced by wall-clock polling while executor-idle
  time is frozen. If the caller binds an exact profession candidate identity,
  mandatory preflight must preserve it across the wait and may not substitute
  the baseline-preferred offered choice. On every other explicitly observed
  profession chooser, an unavailable or incomplete profession candidate fails
  closed and cannot fall back to generic `recovery_close_menu` selection or the
  ordinary daily-planning path. An ordinary `LevelUpMenu` may expose an unavailable profession
  placeholder, which does not suppress its existing generic recovery. The
  preflight returns an explicit blocked queue build result and records its
  reason when no required mandatory candidate is dispatchable.
- Per-transition budgets distinguish farm/story/minigame, shipping/save,
  level/dialogue, unknown menu, and controller states; an overall bound remains.
  Active Sleep reuses the transaction's 36000-tick overall policy after native
  confirmation and adds only `path.Count * 90` for the pre-confirmation route,
  rather than maintaining a shorter competing magic limit.
- Completion requires exact date `+1`, a real save commit, no active event,
  minigame, menu, dialogue, or tool lock, and one verified fresh action.
  `debug.*`, `recovery.*`, profession selection, story/minigame control, sleep,
  wait, facing, safe-slot selection, dialogue response, and menu-close
  operations cannot satisfy that final action. The visual smoke compares the
  transaction against both the selected plan-level
  `exploration.visit_location` identity propagated by the LiveTrainingLoop and
  the actual verified movement primitive emitted by the compiler
  (`executor.move_to_tile` or `executor.traverse_connector`). A recovery or
  debug plan cannot complete the transaction merely because one of its
  mechanical steps is a valid movement primitive. For a multi-primitive queue,
  the smoke binds the transaction to the unique terminal entry in
  `step_results`; it never substitutes the aggregate's last queue item.
- The plan identity must exist in the authoritative capability registry, expose
  a declared or partially-blocked-family candidate, require no player
  confirmation, and explicitly allow policy/autonomous invocation. The
  primitive must also be present in the product
  executor capability catalog; an arbitrary `executor.*` string cannot complete
  the transaction.
- The game-thread observer captures the safe next-day calendar/time state before
  controller handoff. The HTTP request thread may confirm the verified fresh
  action and persist transaction-only data, but must not read `Game1` or
  `Context`.
- The logical transaction ID retains the originating run lineage, while the
  on-disk evidence filename is sanitized through the harness's shared filename
  policy before it is combined with the configured evidence directory.
- A smoke can pass only when that evidence file exists under the isolated
  training-output root and its schema, run ID, transaction ID, and terminal
  status match the live tracker. The summary records the verified file path and
  SHA-256; a log-only persistence failure is not acceptable evidence.
- The positive scenario must observe exactly two ordinary level screens, one
  profession screen, one `SoundInTheNightEvent`, one shipping terminal menu,
  exact delayed income and Full Shipment completion. Expected income is read
  from the post-fixture transparent shipping-bin
  `total_sell_to_store_price`, after profession fixture state is finalized;
  the earlier inventory price is not an authoritative settlement value.
- A queued fixture level is valid only when the player's native base skill and
  experience threshold match that level. Ordinary fixture levels must use
  skills distinct from the pending profession skill, so the composite cannot
  create two incompatible histories for one skill. The fixture may prepare
  native player state and `newLevels`, but it must not construct the end-of-night
  `LevelUpMenu`; vanilla remains responsible for creating and ordering menus.
  Combat-level fixtures also run vanilla health revalidation after profession
  membership is finalized so immediate profession effects are not omitted or
  applied twice.
- Hidden matrix scenarios may independently combine empty/positive shipping,
  zero to two ordinary level screens, one planned profession choice and the
  native day-31 earthquake fixture. They all reuse the same sleep, menu
  recovery, save observation and safe-next-day action path. The owner-visible
  `-OwnerComposite` mode fails before launch unless it has positive shipping,
  exactly two ordinary level screens, one planned profession choice, the
  native day-31 earthquake fixture and a visible game window.
- `Invoke-RuntimeEndOfNightStoryHandoffMatrix.ps1` is a thin two-profile caller
  over the same Full Shipment smoke. Both profiles must bind the exact
  `FarmHouse -> Farm` connector generated from the fresh transparent route
  projection. Event `63` must then complete through the existing
  `story.advance_event` chain. Event `1590166` must remain active at its typed
  question after a deliberately response-free low-level request is rejected.
  The two profiles must share Git, script, entry-DLL and deployed-bundle
  identities. Neither profile is training-label eligible or an owner PASS.
  In owner-visible mode only, the Cat profile holds the already-recorded native
  `catQuestion` for five seconds before cleanup; Kent and all hidden runs keep
  zero additional delay.
- `Invoke-RuntimeEndOfNightNegativeMatrix.ps1` is a thin two-profile caller
  over the same no-shipment native sleep and save path. After the normal sleep
  executor has completed and the transaction has observed a real save plus the
  safe next-day handoff, a debug-only fixture opens a real `QuestLog`. The
  `unknown_menu_transition_timeout` profile applies a shortened transition
  budget; `overall_timeout` applies a shortened overall budget. The production
  budgets remain unchanged, neither profile adds a menu executor, and neither
  profile is training-label eligible. The menu constructor and its safe-control
  predicates match the locked 1.6.15 `Game1` quest-log input branch at
  `Game1.cs:15059-15064`; this is a native menu shape, not a synthetic stand-in.
  The armed profile is bound to the fixture `RunId`; every other successful
  native sleep fixture clears it before preparing its path, so a stale profile
  cannot leak into another run or story fixture.
  Hidden runs terminate immediately after evidence is persisted. Owner-visible
  runs hold the already-blocked native menu for five seconds before isolated
  process cleanup, so the failure shape is observable without changing either
  timeout budget.
- The no-shipment scenario must observe the same planned level handling, no
  shipping summary, one standalone save menu, no income change, and no Full
  Shipment completion.
- Unknown menus, unsafe questions, and transition/overall timeouts remain
  bounded failures with diagnostics. The negative fixture can only arm through
  `debug.prepare_full_shipment_terminal_sleep`, activates only after the
  ordinary sleep executor has relinquished control, and records the real menu
  type before the selected timeout blocks the transaction.

## Historical Acceptance Procedure

The following records the original pre-merge procedure, not a request to repeat
the accepted owner visual gate. Historical pre-commit evidence was intentionally
non-promotional. The affected-file
test set passed `191/191`, the Release solution build completed with zero
warnings and errors, story handoff matrix
`runtime-end-of-night-story-handoff-matrix-20261102-135601` passed `2/2`, and
negative matrix `runtime-end-of-night-negative-matrix-20261102-135910` passed
`2/2`. The immediately preceding positive matrix passed `7/7`, but its smoke
script identity predates the completed-story-interrupt receipt fix and cannot be
combined with the newer runs. The final hidden gates therefore start again from
one committed clean-worktree identity:

1. Commit the reviewed candidate and make no source, dependency, configuration,
   or documentation changes while the hidden and visible identity gates run.
2. Run `Invoke-RuntimeEndOfNightPositiveMatrix.ps1` for hidden isolated native
   matrix evidence covering: positive shipping and exact income;
   empty-bin save without a shipping summary; more than one ordinary level
   screen; a level-5 profession plus all remaining menus; positive shipping plus
   ordinary leveling; positive shipping plus a legal level-10 profession; and the full legal
   day-31 FarmEvent plus ordinary levels plus profession plus shipping
   composite. Each run must finish with a real save and a fresh normal action.
3. Run `Invoke-RuntimeEndOfNightStoryHandoffMatrix.ps1` as a distinct native
   post-save story-event/cutscene handoff. It
   must first close a real save, then traverse a real connector into Farm and
   let `checkForEvents` select locked event `63`; the existing
   `story.advance_event` planner/compiler/executor must own progress. A separate
   locked pet-adoption run must reach event `1590166` and block at
   `catQuestion` without choosing a response. The existing synthetic
   `debug.setup_story_event` smoke remains component calibration and cannot
   satisfy either native gate. Neither story run may masquerade as the fresh
   normal action that completed the end-of-night transaction.
4. Run `Invoke-RuntimeEndOfNightNegativeMatrix.ps1` for native evidence that a
   real unknown `QuestLog` blocks through the per-transition budget and that
   the same real menu is bounded by the overall budget in the separate profile.
   The Cat handoff supplies the unsafe dialogue/question negative. Every case
   must block with a typed diagnostic and finite isolated-process cleanup.
5. Run the positive matrix in visible owner-gate mode so the repository owner
   can watch the empty-bin, positive-shipping, ordinary-level, profession,
   shipping-plus-level/profession and full day-31 composite categories without
   edited or synthetic screen sequences. Present the distinct native
   overnight special-event/CG and post-save story/cutscene handoff, plus the
   relevant blocked negative shapes, in the same candidate identity. Any
   minigame actually encountered must remain separately classified and bounded;
   a synthetic minigame is not an acceptance substitute. The owner must answer
   the exact approval question, **"Does the owner visually approve this fix
   candidate for PR submission?"**, with an explicit PASS before a PR can be
   opened. Summaries record the loaded
   Bridge, Harness, Contracts, RuntimePrimitives, Backend, LiveTrainingLoop and
   Bootstrap entry-DLL hashes, complete deployed/runtime bundle hashes, script
   hashes, Git HEAD and dirty-worktree state. Any subsequent code, dependency,
   configuration or binary change invalidates that visual PASS.
6. Only after that explicit PASS: submit the PR, run normal review/CI, merge,
   attach the accepted receipts, and close #381. Until then the candidate is not
   accepted and no matrix subset may be used as a substitute.
