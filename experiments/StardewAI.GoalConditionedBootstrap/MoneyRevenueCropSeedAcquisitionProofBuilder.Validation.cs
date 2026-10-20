using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSeedAcquisitionProofBuilder
{
    private static readonly HashSet<string> PurchaseQueueOptions = new(
        new[]
        {
            "executor.wait_ticks",
            "executor.buy_shop_item",
            "executor.close_menu"
        },
        StringComparer.Ordinal);

    private static ChainVerification VerifyExecutionChain(
        MoneyRevenueCropProductionObligationPlan plan,
        MoneyRevenueCropProductionCommitmentPlan commitment,
        MoneyRevenueCropSeedAcquisitionExecutionManifest manifest,
        LoadedTransition[] transitions,
        SnapshotEnvelope? priorBoundary)
    {
        Require(transitions.Length > 0 &&
                transitions.Select(value => value.QueuePath)
                    .Distinct(PathComparer).Count() == transitions.Length &&
                transitions.Select(value => value.ReceiptPath)
                    .Distinct(PathComparer).Count() == transitions.Length &&
                transitions.All(value =>
                    value.Request.RunId == manifest.RunId),
            "Money Revenue crop seed-acquisition transition identities are not unique and contiguous.");

        var source = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            RequiredFile(
                plan.SnapshotPath,
                "Money Revenue crop seed-acquisition source snapshot"),
            "Money Revenue crop seed-acquisition source snapshot");
        ValidateSnapshot(source, "source");
        Require(source.StateHash == plan.SourceStateHash,
            "Money Revenue crop seed-acquisition plan source hash drifted.");
        var expectedSourceStateHash = priorBoundary?.StateHash ??
            transitions[0].Before.StateHash;
        Require(manifest.SourceStateHash == expectedSourceStateHash,
            "Money Revenue crop seed-acquisition manifest source boundary drifted.");

        PurchaseBinding? activeBinding = null;
        var purchaseSlices = 0;
        var verifiedIncrease = 0;
        var proofs = new List<MoneyRevenueCropSeedAcquisitionTransitionProof>();
        var totalDay = ReadInt(transitions[0].Before, "time", "total_days");
        for (var index = 0; index < transitions.Length; index++)
        {
            var transition = transitions[index];
            ValidateSnapshot(transition.Before, "before");
            ValidateSnapshot(transition.After, "after");
            Require(SameSnapshotIdentity(source, transition.Before) &&
                    SameSnapshotIdentity(source, transition.After),
                "Money Revenue crop seed-acquisition snapshot identity drifted at transition " +
                index + ".");
            Require(index == 0
                    ? transition.Before.StateHash == expectedSourceStateHash &&
                        (priorBoundary is null
                            ? SameRuntimeSourceBoundary(
                                source,
                                transition.Before)
                            : BootstrapArtifactGuard.EqualJson(
                                  priorBoundary,
                                  transition.Before) &&
                              priorBoundary.GameTick ==
                                  transition.Before.GameTick)
                    : BootstrapArtifactGuard.EqualJson(
                            transitions[index - 1].After,
                            transition.Before) &&
                        transitions[index - 1].After.StateHash ==
                            transition.Before.StateHash &&
                        transitions[index - 1].After.GameTick ==
                            transition.Before.GameTick,
                "Money Revenue crop seed-acquisition state chain is discontinuous at transition " +
                index + ".");

            var beforeDay = ReadInt(
                transition.Before,
                "time",
                "total_days");
            var afterDay = ReadInt(
                transition.After,
                "time",
                "total_days");
            Require(beforeDay == totalDay &&
                    afterDay == totalDay &&
                    transition.Request.TotalDay == totalDay &&
                    totalDay <= commitment.SeedSupplyDeadlineTotalDay,
                "Money Revenue crop seed-acquisition crossed its same-day deadline boundary.");

            var receiptReasons = QueueExecutionReceiptValidator
                .ValidateContinuationSlice(
                transition.Queue,
                transition.Before,
                transition.Receipt,
                transition.After,
                transition.ReceiptPath,
                transition.Request.RunId,
                transition.Request.ExecutorVersion,
                transition.Request.SelectedCandidateId,
                transition.Before.StateHash,
                out var effectiveItems);
            Require(receiptReasons.Length == 0,
                "Money Revenue crop seed-acquisition queue receipt was rejected at transition " +
                index + ": " +
                string.Join(",", receiptReasons));

            var binding = ReadPurchaseBinding(
                transition.Queue,
                commitment.SeedQualifiedItemId);
            if (activeBinding is not null)
            {
                Require(binding == activeBinding,
                    "Money Revenue crop seed-acquisition continuation identity drifted before purchase.");
            }
            activeBinding ??= binding;
            var effectiveBinding = ReadPurchaseBinding(
                new ActionQueueEnvelope
                {
                    Items = effectiveItems
                },
                commitment.SeedQualifiedItemId);
            Require(effectiveBinding == binding,
                "Money Revenue crop seed-acquisition effective continuation binding drifted.");

            var optionIds = effectiveItems
                .Select(value => value.OptionId)
                .ToArray();
            var hasPurchase = optionIds.Contains(
                "executor.buy_shop_item",
                StringComparer.Ordinal);
            var beforeQuantity = ReadSeedQuantity(
                transition.Before,
                commitment.SeedQualifiedItemId);
            var afterQuantity = ReadSeedQuantity(
                transition.After,
                commitment.SeedQualifiedItemId);
            var beforeMoney = ReadInt(
                transition.Before,
                "player",
                "money");
            var afterMoney = ReadInt(
                transition.After,
                "player",
                "money");
            int? expectedUnitPrice = null;
            var stage = "supporting_route_or_shop";

            if (hasPurchase)
            {
                expectedUnitPrice = VerifyPurchaseSlice(
                    transition,
                    binding,
                    effectiveItems,
                    beforeQuantity,
                    afterQuantity,
                    beforeMoney,
                    afterMoney,
                    index);
                verifiedIncrease = checked(
                    verifiedIncrease + afterQuantity - beforeQuantity);
                purchaseSlices++;
                activeBinding = null;
                stage = "purchase_slice";
            }
            else
            {
                VerifySupportingStage(
                    transition,
                    optionIds,
                    beforeQuantity,
                    afterQuantity,
                    beforeMoney,
                    afterMoney,
                    index);
            }

            proofs.Add(new MoneyRevenueCropSeedAcquisitionTransitionProof(
                index,
                stage,
                transition.QueuePath,
                Hash(transition.QueuePath),
                transition.BeforePath,
                Hash(transition.BeforePath),
                transition.ReceiptPath,
                Hash(transition.ReceiptPath),
                transition.AfterPath,
                Hash(transition.AfterPath),
                transition.Queue.QueueId,
                transition.Before.StateHash,
                transition.After.StateHash,
                transition.Before.GameTick,
                transition.After.GameTick,
                optionIds,
                binding.ShopId,
                binding.StockId,
                binding.OutputStack,
                binding.OutputQuality,
                binding.MaxUnitPrice,
                expectedUnitPrice,
                beforeQuantity,
                afterQuantity,
                afterQuantity - beforeQuantity,
                beforeMoney,
                afterMoney,
                beforeMoney - afterMoney,
                true));
        }

        var initialQuantity = ReadSeedQuantity(
            transitions[0].Before,
            commitment.SeedQualifiedItemId);
        var finalQuantity = ReadSeedQuantity(
            transitions[^1].After,
            commitment.SeedQualifiedItemId);
        Require(activeBinding is null &&
                purchaseSlices > 0 &&
                verifiedIncrease == commitment.SeedQuantityShortfall &&
                finalQuantity - initialQuantity ==
                    commitment.SeedQuantityShortfall,
            "Money Revenue crop seed-acquisition did not prove its exact shortfall.");
        return new ChainVerification(
            initialQuantity,
            finalQuantity,
            verifiedIncrease,
            totalDay,
            proofs.ToArray());
    }


    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
