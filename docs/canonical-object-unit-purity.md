# Canonical Ordinary Object Unit Purity

## Existing Contract, Not A New Identity

The old unit multiset contract came from f3133e722f3653b440306329189c740215a88a4d.
6f21e240829c663c63d5a619bbe527fceddd438d deliberately normalizes Object.Flipped;
e847e04f additionally preserves Tool.swingTicker and inventory-receipt history.
These meanings,unit stack1,serializer byte format,quantity and acceptance gates
remain unchanged. The original executor is not replaced or weakened.

Fresh E decompilation of Item,Object,ObjectDataDefinition and ParsedItemData under
DLL SHA2567f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee
shows that ordinary Object.getOne creates a new Object and draws live NextBool
for Flipped,even for wood/stone IDs where the resulting flag is unused. Clearing
Flipped afterwards stabilizes identity but does not undo the RNG draw.

## One Shared Native Factory

src/SharedNative/CanonicalVanillaObjectUnit.cs is linked into both native modules,
not copied into two implementations or exposed as a new Mod/controller. It builds
a detached exact ordinary Object using the pinned deterministic constructor defaults
and native CopyFieldsFrom,omitting only the discarded random flip. A parameterless
Object plus CopyFieldsFrom alone would be wrong:placement/data/fragility/stone
duration defaults also matter. Both old hash callers keep their existing remaining
normalization,serialization and native fallback.

The same factory handles eligible ordinary FromStandard cold-cache construction.
It checks the exact binary,exact Object type,non-big-craftable data and native
ObjectDataDefinition. Native clone/constructor/copy and introduced setter methods
must be unpatched;registry Create/GetData/GetDataOrErrorItem and the native
ObjectDataDefinition factory are checked too. Patch state is checked live,not
cached as permanent permission. An empty retained Harmony patch-info record is
not an active patch:eligibility returns after the last owner is unpatched.
The bridge uses the same existing Harmony2.3.3 dependency as the harness,excluding
its runtime asset so SMAPI remains the runtime owner.

Torch,Ring,CombinedRing,ColoredObject,other subclasses,big craftables,unknown
definitions/binaries or patched paths retain their original behavior. This is not
a claim that those legacy paths are pure or that issue29 is globally resolved.
No RNG reference is swapped,reset,advanced or restored by the new factory.

## Native And Pure Verification

The isolated external calibration uses a copied E ProofFarm save,two production
Mods plus one explicitly allowlisted test-only calibration Mod. The third Mod
is not shipped or used as gameplay/Teacher evidence. It captures native legacy
expected bytes before the purity window,then tests both actual original hash
callers,inventory-receipt variants and standard creation against those bytes.

807 current ObjectData rows comprise773 ordinary factory inputs and34 special
factory types. With19 enriched source variants,792 inputs matched byte-for-byte.
Enriched cases cover stack,quality,recipe/history/flip,name/price/edibility,position,
scale,quest/owner/preserve/order/modData,nested held/input items and constructor
defaults that native getOne does not copy. All source payloads remain unchanged.
Malformed-XML serialization keeps the old InvalidOperationException behavior.

The test-only read-only RNG sensor lives in tests/Shared,not the shipped runtime
library. It reads the bounded complete primitive field graph of the actual BCL
Random,including base/private fields;it invokes no virtual random methods and
writes no state. Unknown runtime/layouts are unavailable. Four pure .NET8 tests
check stable/change detection,subsequent sequence equality and foreign subclasses;
the native calibration checks the game's actual .NET6 RNG reference/state.

Successful native A/B and final C runs preserve the RNG reference/state and serialized player/
current-location state plus tracked menu/location/scalars within the tested window.
This scoped world check is not an audit of every map/team/audio/mod side effect.
A deliberate GetOneNew patch makes both factories decline and both old callers
invoke the legacy hook. B also verifies a patched public CopyFieldsFrom wrapper is
declined rather than newly invoked. Owned patches alone are removed afterwards.
Final C follows relocation of the unchanged diagnostic algorithm into tests/Shared;
it uses the final native caller artifacts and test-only sensor. Final focused47
passed with zero failures/skips;native Bridge/Harness and calibration builds have
zero warnings/errors. A targeted lookup-patch D run rejected the patched query and
kept legacy bytes but exposed retained empty patch-info after unpatch. The final
guard checks actual owners;targeted E passes with six legacy query callbacks,
unchanged legacy hashes and restored eligibility after unpatch. Only this new
branch was rerun,not the792-case matrix. D is retained as a failed lifecycle test.
Initial build API/language mismatches were corrected before
native runs;they are not runtime regressions or grounds to change old contracts.
Original save hash68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d
stays unchanged;owned game processes exit. Raw matrices,source copies,failures and
reports stay external. Final local/CI/release evidence is recorded in closeout.

## Remaining Responsibility

This removes the specific ordinary canonical-unit/cold-standard RNG draw,not all
constructor reads in the transparent bridge. ReadCurrentLocationDebris still creates
registry projections for chunk-only drops (original purpose792902669),other read
adapters construct sale/recipe projections,and Panning retains other clone/probe
paths. Special types and broader issue29 probe policy remain explicit debt.

Next address the original ordinary registry-backed drop projection relevant to
the pinned refill repair/pickup boundary,with its existing summary meaning kept;
then exact resource/provenance consumption in the single persistent owner,harvest/
native save/day and bounded managed integration. Existing global coordination,
remaining probe policy,Provider/model and manifest follow. This does not reopen L1,
create a new criterion-local full-game gate or grant production/training credit.
L1 remains19/19 and11/11;formal=false;Student21/21 remains post-training.
