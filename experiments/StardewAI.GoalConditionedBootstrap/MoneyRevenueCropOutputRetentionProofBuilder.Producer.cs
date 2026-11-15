using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropOutputRetentionProofBuilder
{
    private static int ResolveProducerRetentionBoundary(MoneyRevenueCropNativeLifecycleProof lifecycle,
        string boundaryPath, int lastHarvestIndex, int harvestDay)
    {
        var matches = Enumerable.Range(0, lifecycle.Transitions.Length)
            .Where(index => SamePath(lifecycle.Transitions[index].AfterSnapshotPath, boundaryPath)).ToArray();
        Require(matches.Length == 1 && matches[0] >= lastHarvestIndex,
            "Crop producer retention boundary must uniquely follow the complete owned harvest wave in the original lifecycle.");
        var last = matches[0];
        Require(lifecycle.Transitions[lastHarvestIndex..(last + 1)].All(row => row.TotalDay == harvestDay),
            "Crop producer retention boundary crosses the original harvest day.");
        return last;
    }

    private static MoneyRevenueCropProducerRetention BuildProducerRetention(MoneyRevenueCropNativeLifecycleProof lifecycle,
        MoneyRevenueCropNativeLifecycleTransitionProof[] harvests, int first, int last, string itemId, int minimum, int tileCount)
        => BuildProducerRetentionCore(lifecycle, harvests, first, last, itemId, minimum, tileCount, null, null);

    private static MoneyRevenueCropProducerRetention BuildReservedProducerRetention(MoneyRevenueCropNativeLifecycleProof lifecycle,
        MoneyRevenueCropNativeLifecycleTransitionProof[] harvests, int first, int last, string itemId, int minimum, int tileCount,
        string commitmentId, string waveKey) =>
        BuildProducerRetentionCore(lifecycle, harvests, first, last, itemId, minimum, tileCount, commitmentId, waveKey);

    private static MoneyRevenueCropProducerRetention BuildTransferredProducerRetention(MoneyRevenueCropNativeLifecycleProof lifecycle,
        MoneyRevenueCropNativeLifecycleTransitionProof[] harvests, int first, int last, string itemId, int minimum, int tileCount,
        string commitmentId, string waveKey) =>
        BuildProducerRetentionCore(lifecycle, harvests, first, last, itemId, minimum, tileCount, commitmentId, waveKey, transferredStock: true);

    private static MoneyRevenueCropProducerRetention BuildSharedInventoryProducerRetention(MoneyRevenueCropNativeLifecycleProof lifecycle,
        MoneyRevenueCropNativeLifecycleTransitionProof[] harvests, int first, int last, string itemId, int minimum, int tileCount,
        string commitmentId, string waveKey) =>
        BuildProducerRetentionCore(lifecycle, harvests, first, last, itemId, minimum, tileCount, commitmentId, waveKey,
            transferredStock: true, inventoryEffects: true);

    private static MoneyRevenueCropProducerRetention BuildProducerRetentionCore(MoneyRevenueCropNativeLifecycleProof lifecycle,
        MoneyRevenueCropNativeLifecycleTransitionProof[] harvests, int first, int last, string itemId, int minimum, int tileCount,
        string? commitmentId, string? waveKey, bool transferredStock = false, bool inventoryEffects = false)
    {
        Require(minimum % tileCount == 0, "Crop producer retention has no exact original per-tile minimum.");
        var original = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropNativeLifecycleProofRequest>(lifecycle.RequestPath,
            "producer-retention original lifecycle request");
        Require(original.Transitions.Length == lifecycle.Transitions.Length,
            "Crop producer retention lifecycle request denominator changed.");
        var account = new MoneyRevenueCropRetentionAccount(Read(lifecycle.Transitions[first].BeforeSnapshotPath), itemId);
        var stock = transferredStock ? new CropTeacherTransferredSaleStock(commitmentId!, waveKey!, itemId) : null;
        var segments = new List<MoneyRevenueCropRetentionSegment>();
        var guards = new List<Action>();
        for (var index = first; index <= last; index++)
        {
            var row = lifecycle.Transitions[index]; var request = original.Transitions[index];
            var before = Read(RequiredBoundFile(row.BeforeSnapshotPath, row.BeforeSnapshotSha256, "producer-retention before"));
            var after = Read(RequiredBoundFile(row.AfterSnapshotPath, row.AfterSnapshotSha256, "producer-retention after"));
            RequiredBoundFile(row.QueuePath, row.QueueSha256, "producer-retention queue");
            RequiredBoundFile(row.ExecutionReceiptPath, row.ExecutionReceiptSha256, "producer-retention receipt");
            Require(row.BeforeStateHash == before.StateHash && row.AfterStateHash == after.StateHash,
                "Crop producer retention state changed.");
            var owned = harvests.Contains(row);
            var delta = checked(Count(after) - Count(before));
            var frameCount = 1;
            if (row.TransitionKind == "shared_source_return")
            {
                Require(!owned && request.NativeSharedSource is not null,
                    "Crop producer retention cannot credit an unbound foreign transition.");
                if (stock is not null) frameCount = inventoryEffects ? stock.ObserveSharedInventoryEffects(account, request) : stock.ObserveShared(account, request);
                else
                {
                    var source = NativeExecutionActionSourceReader.ReadSelectedNativeActionQueue(row.QueuePath, row.BeforeSnapshotPath,
                        row.ExecutionReceiptPath, row.AfterSnapshotPath, request.RunId, request.SelectedCandidateId);
                    frameCount = source.Actions.Length;
                    var preserve = commitmentId is null ? null : CropTeacherSaleStockInventory.BindPreserved(source.Before,
                        CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(request.NativeSharedSource!.LedgerPath,
                            "reserved producer-retention original source ledger"), commitmentId, waveKey!, itemId);
                    foreach (var frame in source.Actions)
                        if (preserve is null) account.Observe(frame.Before, frame.After, ownedHarvest: false);
                        else account.ObserveReservedForeign(frame.Before, frame.After, preserve(frame.After));
                }
                guards.Add(MoneyRevenueCropNativeSharedSourceVerifier.RetainInputs(request.NativeSharedSource!));
            }
            else
            {
                Require(row.TransitionKind is "crop_actions" or "supporting_route_to_crop" or "supporting_route_to_sleep" &&
                    request.NativeSharedSource is null,
                    "Crop producer retention crosses an unsupported or day-rollover boundary.");
                if (owned) Require(delta >= checked(row.ItemCount * (minimum / tileCount)),
                    "Crop producer retention owned harvest batch did not retain its original minimum yield.");
                if (stock is null) account.Observe(before, after, owned);
                else stock.Observe(account, before, after, owned);
            }
            segments.Add(new(row.QueueId, owned, frameCount, delta, account.Quantity));
        }
        foreach (var guard in guards) guard();
        Require(account.Quantity >= minimum,
            "Crop producer retention is insufficient after intervening consumption; foreign stock cannot replace owned output.");
        return new(account.Quantity, account.Variants, segments.ToArray());

        int Count(SnapshotEnvelope snapshot) => ExactInventoryReceiptVerifier.InventoryCount(snapshot, itemId, 0) ??
            throw new InvalidDataException("Crop producer retention inventory is unavailable.");
        static SnapshotEnvelope Read(string path) => CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(path, "crop producer retention snapshot");
    }

    public static bool HasProducerRetention(MoneyRevenueCropOutputRetentionProof proof) =>
        (proof.SchemaVersion == "money_revenue_crop_output_retention_proof.v2" || HasReservedProducerRetention(proof)) && proof.ProducerRetention is not null;

    public static bool HasReservedProducerRetention(MoneyRevenueCropOutputRetentionProof proof) =>
        proof.SchemaVersion is "money_revenue_crop_output_retention_proof.v3" or "money_revenue_crop_output_retention_proof.v4" or "money_revenue_crop_output_retention_proof.v5" &&
        proof.ProducerRetention is not null;

    // The caller already binds/rebuilds the immutable certificate. This limits the selected native units,
    // including partial-stack plans, rather than reinterpreting the legacy aggregate inventory delta.
    public static void VerifyProducerTerminalTarget(MoneyRevenueCropOutputRetentionProof proof,
        CropTeacherTerminalShippingTarget target, SnapshotEnvelope initial)
    {
        if (proof.SchemaVersion == "money_revenue_crop_output_retention_proof.v1" && proof.ProducerRetention is null) return;
        Require(!HasReservedProducerRetention(proof) || target.RequireSaleStockReservations,
            "Reserved producer retention requires the explicit sale-stock terminal consumer.");
        Require(HasProducerRetention(proof) && target.NativeVariants is not null && proof.MinimumUnits > 0 &&
            target.RequiredQuantity == proof.MinimumUnits && target.QualifiedItemId == proof.HarvestItemQualifiedId,
            "Producer retention requires the explicit native variant terminal consumer.");
        var retained = proof.ProducerRetention!;
        Require(retained.Variants.All(row => row.RetainedQuantity > 0 && !string.IsNullOrWhiteSpace(row.InventoryIdentitySha256)) &&
            retained.Variants.Select(row => row.InventoryIdentitySha256).Distinct(StringComparer.Ordinal).Count() == retained.Variants.Length &&
            retained.Variants.Sum(row => row.RetainedQuantity) == retained.RetainedQuantity && retained.RetainedQuantity >= proof.MinimumUnits,
            "Producer retention variant denominator is invalid.");
        target.VerifyNativeVariantSource(initial, 0, proof.MinimumGrossRevenue);
        var inventory = initial.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray().ToArray();
        var selected = target.ReadNativeVariants().Select(variant => new
        {
            Key = MoneyRevenueCropRetentionAccount.InventoryIdentity(inventory.Single(row => row.GetProperty("slot_index").GetInt32() == variant.SlotIndex)),
            Quantity = variant.PlannedQuantity
        });
        foreach (var group in selected.GroupBy(row => row.Key, StringComparer.Ordinal))
            Require(group.Sum(row => row.Quantity) <= (retained.Variants.SingleOrDefault(row => row.InventoryIdentitySha256 == group.Key)?.RetainedQuantity ?? 0),
                "Native terminal selects foreign or already-consumed crop stock.");
    }
}
