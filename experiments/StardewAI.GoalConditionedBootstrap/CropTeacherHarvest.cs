using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherHarvest
{
    private readonly Preparation preparation;
    private readonly NativeBatch native;
    private readonly CropTeacherSupportingRebind rebind;

    private CropTeacherHarvest(NativeBatch native)
    {
        this.native = native;
        preparation = native.Prepared;
        AppliedTileCount = native.AppliedTileCount;
        CompleteBatch = AppliedTileCount == preparation.Tiles.Count;
        RetainedQuantityIncrease = native.HeldIncrease;
        AppliedTiles = Array.AsReadOnly(preparation.Tiles.Take(AppliedTileCount).ToArray());
        AfterSnapshotPath = native.AfterSnapshotPath;
        rebind = new(native.After, preparation.BaseLedger, native.StepId, ValidateInputsUnchanged, "Crop harvest",
            preparation.RetainSaleStock ? BuildSaleStockClaims(preparation, native.Before, native.After, AppliedTileCount, native.StepId) : null);
        ValidateInputsUnchanged();
    }

    public int AppliedTileCount { get; }
    public bool CompleteBatch { get; }
    public long RetainedQuantityIncrease { get; }
    public IReadOnlyList<MoneyRevenueCropPlantingTile> AppliedTiles { get; }
    public string AfterSnapshotPath { get; }
    public string? PickupSettlementPath => native.PickupSettlementPath;
    internal int PickupElapsedTicks => native.PickupElapsedTicks;
    internal StardewAI.Contracts.State.SnapshotEnvelope SettledSnapshot => native.After;
    public int WaveIndex => preparation.WaveIndex;
    public string OutputRetentionKey => preparation.OutputRetentionKey;
    // An exact batch is not the original whole-wave output-retention/lifecycle proof.
    public bool WholeWaveOutputRetentionVerified => false;
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => native.InputHashes;
    public void ValidateInputsUnchanged() => native.ValidateInputsUnchanged();
}
