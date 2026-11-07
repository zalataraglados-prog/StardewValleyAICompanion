using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedTransferMarker
{
    private readonly CropTeacherSeedTransferSupport scope;
    private readonly StrategyCommitmentLedger ledger;

    internal CropTeacherSeedTransferMarker(CropTeacherSeedTransferSupport scope, StrategyCommitmentLedger ledger)
    {
        this.scope = scope;
        this.ledger = ledger;
    }

    public StrategyCommitmentLedger Ledger => CropTeacherSeedSupportSource.Copy(ledger);

    public SmallModelAction BuildAction(int standTileX, int standTileY)
    {
        scope.ValidateInputsUnchanged();
        return new SmallModelAction
        {
            ActionId = ledger.PreparedMaterialTransfer!.PortfolioId, OptionId = "executor.transfer_material",
            Parameters = CropSeedMaterialTransferBinding.ToParameters(ledger, scope.CommitmentId).Concat(new[]
            {
                new SmallModelActionParameter { Name = "stand_tile_x", Value = standTileX.ToString(CultureInfo.InvariantCulture) },
                new SmallModelActionParameter { Name = "stand_tile_y", Value = standTileY.ToString(CultureInfo.InvariantCulture) }
            }).ToArray()
        };
    }
}
