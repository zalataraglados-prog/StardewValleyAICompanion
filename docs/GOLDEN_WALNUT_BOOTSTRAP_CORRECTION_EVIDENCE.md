# Golden Walnut bootstrap correction: evidence

## Reproduced defect

Baseline `f00fbe79` rejected an exact island window before the existing
prerequisite evaluator could run:

`master_angler_full_route_timing_unavailable:future_route_target_map_inaccessible_on_date`

The peer's fresh-mainland test used all 72 species missing, valid rod/equipment,
an exact artifact window, matching date/time/weather, locked island access and an
inaccessible target map. Making only the capture-date target map accessible made
the ordinary validator pass, proving that route admission was the boundary.

## Corrected contracts

- Ordinary `TryValidate` still requires terminal=true and complete route timing.
- `TryValidateGingerIslandPrerequisiteControl` requires the explicit control
  marker, terminal=false, an island target, an exact artifact window and all
  non-route eligibility checks.
- The evaluator never falls from control mode into terminal fishing. It either
  returns the existing island prerequisite candidate or a structured block.
- The builder emits at most one stable control intent and publishes it separately
  from ordinary candidates.
- The live loop compiles only an available marked candidate through the existing
  compilers and records a non-training control selection.
- Policy-trajectory append rejects the marker even after a fully verified native
  execution.

## Native read-only evidence

- `StardewValley.Locations/BoatTunnel.cs:136-199,218-267`: repairs and ticket
  purchase require native mail/material/money state.
- `StardewValley.Locations/IslandSouth.cs:286-296,558-569`: first island entry
  schedules `Visited_Island`; Western Turtle costs 10 walnuts and requires First
  Parrot.
- `StardewValley.Locations/IslandHut.cs:353-355`: First Parrot costs 1 walnut.
- `StardewValley.Locations/IslandWest.cs:171-174`: Island House costs 20 walnuts.

The correction does not fabricate any of these states. Existing transparent
fields and native interactions remain authoritative.
