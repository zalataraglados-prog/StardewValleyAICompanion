using System;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public static class MuseumOutputCustodyTransferBinding
{
    public const string Kind = "museum_output_custody_transfer";
    public const string KindParameter = "material_transfer_control_kind";
    public const string PortfolioParameter = "material_transfer_control_portfolio_id";
    public const string LedgerRevisionParameter = "material_transfer_control_ledger_revision";
    public const string ReservationParameter = "museum_output_custody_reservation_id";
    public const string ReservationRevisionParameter = "museum_output_custody_reservation_revision";
    public const string PositionParameter = "museum_output_custody_position_sha256";

    public static bool IsControlParameter(string name) =>
        name.StartsWith("material_transfer_control_", StringComparison.Ordinal) ||
        name.StartsWith("museum_output_custody_", StringComparison.Ordinal);

    public static bool IsBound(ActionQueueItem item, StrategyCommitmentLedger ledger)
    {
        var prepared = ledger.PreparedMaterialTransfer;
        var parameters = item.NormalizedCommand.Parameters;
        if (item.OptionId != "executor.transfer_material" || item.NormalizedCommand.OptionId != item.OptionId ||
            prepared is null || parameters.Any(row => row.Name.StartsWith("acquisition_", StringComparison.Ordinal)) ||
            parameters.Where(row => IsControlParameter(row.Name)).GroupBy(row => row.Name, StringComparer.Ordinal)
                .Any(group => group.Count() != 1) ||
            Read(parameters, KindParameter) != Kind ||
            Read(parameters, LedgerRevisionParameter) != ledger.Revision.ToString(CultureInfo.InvariantCulture) ||
            !PreparedMaterialTransferContract.IsBound(ledger, Read(parameters, PreparedMaterialTransferContract.BindingParameter),
                Read(parameters, PortfolioParameter), item.NormalizedCommand.GoalId, prepared.RouteSourceDecisionId) ||
            prepared.MaterialRelocations.Length != 1 ||
            !PreparedMaterialTransferContract.CustodyPositionsBound(ledger, prepared.ReservationProjection)) return false;
        var rows = ledger.MaterialReservations.Where(row => row.ReservationId == Read(parameters, ReservationParameter)).ToArray();
        if (rows.Length != 1) return false;
        var custody = rows[0];
        var move = prepared.MaterialRelocations[0];
        return custody.GoalId == item.NormalizedCommand.GoalId && custody.SourceDecisionId == prepared.RouteSourceDecisionId &&
            Read(parameters, ReservationRevisionParameter) == custody.Revision.ToString(CultureInfo.InvariantCulture) &&
            MaterialOutputCustodyContract.TryGetPositionBinding(custody, ledger, out var position) &&
            Read(parameters, PositionParameter) == position && move.MaterialReservationId == custody.ReservationId &&
            move.SourceNodeId == custody.NodeId && move.SourceSlotIndex == custody.SlotIndex &&
            move.QualifiedItemId == custody.QualifiedItemId && move.Quantity == custody.Quantity &&
            prepared.Intent.SourceNodeId == custody.NodeId && prepared.Intent.SourceSlotIndex == custody.SlotIndex &&
            prepared.Intent.QualifiedItemId == custody.QualifiedItemId && prepared.Intent.Quantity == custody.Quantity;
    }

    internal static SmallModelActionParameter[] Parameters(StrategyCommitmentLedger ledger, MaterialReservation custody)
    {
        MaterialOutputCustodyContract.TryGetPositionBinding(custody, ledger, out var position);
        return new[]
        {
            Parameter(KindParameter, Kind), Parameter(PortfolioParameter, ledger.PreparedMaterialTransfer!.PortfolioId),
            Parameter(LedgerRevisionParameter, ledger.Revision.ToString(CultureInfo.InvariantCulture)),
            Parameter(ReservationParameter, custody.ReservationId),
            Parameter(ReservationRevisionParameter, custody.Revision.ToString(CultureInfo.InvariantCulture)),
            Parameter(PositionParameter, position),
            Parameter(PreparedMaterialTransferContract.BindingParameter, ledger.PreparedMaterialTransfer.BindingSha256)
        };
    }

    internal static string Read(SmallModelActionParameter[] parameters, string name)
    {
        var values = parameters.Where(row => row.Name == name).ToArray();
        return values.Length == 1 ? values[0].Value : string.Empty;
    }

    internal static SmallModelActionParameter Parameter(string name, string value) => new() { Name = name, Value = value };
}
