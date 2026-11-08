# Ordinary Drop Projection Read Purity

## Parent And Preserved Meaning

PR537 accepted at main086a24e1,tree61e7b7f7,final CI42471318373 all42,
Core2486/Backend1420 zero failures/skips. Its792-case canonical-unit proof is
not repeated here. The final lookup/empty-patch-info increment was controller/
native-tested separately from initial-head review6720202460.

The original792902669fc84189b2d9ff43fbc765a4d2f38e82 change intentionally supplied
registry metadata for chunk-only drops so quest candidates retained item identity.
It must not be removed just because its ordinary constructor consumes RNG.
Live held items remain live,including customized name,stack,quality and metadata.
All existing row/chunk fields,qualified-ID match check and exact_live_item /
exact_item_registry_projection / item_registry_projection_unavailable statuses stay.
SummarizeItem does not expose the random Flipped animation field.

## Same Factory, Native Quality Rules

The existing shared pinned factory adds TryCreateProjection for this read only.
It reuses ordinary standard construction and invokes the actual native Quality
setter then FixQuality only when quality!=0,as current ItemRegistry.Create does.
Native FixQuality clamps0..4 and maps3->4;the original raw debris.item_quality is
not overwritten by the synthesized item's fixed quality. Quantity remains1 and
does not introduce a stack fixup that the original amount1 call skipped.

Nonzero quality requires the setter and virtual fix method to be unpatched.
Default zero does not call either,matching the original skip behavior. Existing
binary/type/definition/registry/constructor guards remain;unknown/special/patched
creation takes the original registry path,not an invented empty item or a purity
claim. There is no new executor,candidate,drop engine,API,default flag or world scan.

Harmony guards canonicalize inherited reflected methods to their declaring type
before patch lookup. Native patch-B reproduced a missed Item.set_Quality patch
when looking it up through Object;the same correction protects inherited
initializer setters in both modules. This tightens fallback eligibility only,
without changing ordinary unpatched construction or serialized payloads.

## Validation And Successor

Source/contract tests and explicit-E native builds accompany a bounded copied-save
calibration using the two production Mods plus one external test-only Mod. The
calibration compares actual original adapter JSON with the same adapter's legacy
registry fallback,then checks actual BCL RNG state/reference and scoped player/
current-location/menu/scalars. It covers raw-vs-fixed qualities,live held items,
chunks,special/error fallback and quality setter/fix patches. Validation/release
results are recorded in closeout;source guards alone are not native evidence.

Related48/48 passed;both production modules and the external fixture built against
explicit E with zero warnings/errors. Native A passed73 ordinary/mixed byte
comparisons over eight IDs and nine qualities plus a live-item case,with RNG,
scoped world and live-item identity/state unchanged. Three special/error cases
kept legacy JSON. A then stopped on an external inherited-method patch target
error;it was not a full pass. B exposed the real inherited patch-lookup defect.
After the declaring-method fix,C/D isolated a fixture overclaim:the warmed native
registry setter callback count is0 even in the forced-legacy control,although its
direct reflection callback is active. Expected and actual JSON hashes matched.
E passed setter/FixQuality direct-patch checks,equal old/new JSON and callback
counts(0/0 setter,1/1 fix),zero-quality skip and post-unpatch restoration. It also
passed patched ItemId setter rejection/restoration for standard and clone in both
native modules. Failed reports remain;the accepted73/792 matrices were not rerun
for a patch-predicate-only correction. All isolated runs stopped their own process
and retained the original save hash unchanged. This is not production outcome
credit or a blanket claim that Harmony can intercept already-inlined native calls.

Raw matrices,module/source hashes,copies and logs remain external. This is not
global probe purity,production attribution or formal training. Other registry
sale/recipe constructors,Panning probes and special types remain issue29 debt.

After this original drop-projection boundary,consume the pinned original refill
repair/pickup resource/provenance evidence in the single persistent owner,then
harvest/native save/day and bounded managed integration. Existing global
coordination/probe policy/Provider/model/manifest remain later,not new local root
Day-0 gates. L1 stays19/19 and11/11,formal=false,Student21/21 post-training.
