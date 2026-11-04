using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.Core.Strategy;

public sealed partial class MuseumDonationRouteScheduleBuilder
{
    public MuseumWithdrawalDonationRouteSchedule BuildWithdrawal(PolicyEventCandidatePrediction selectedStep,
        string chestLocation, int chestX, int chestY, int quantity)
    {
        MuseumWithdrawalDonationRouteSchedule Block(string[] reasons) =>
            new(null, null, null, null, null, null, reasons);
        if (errors.Length > 0) return Block(errors);
        if (string.IsNullOrWhiteSpace(chestLocation) || chestX < 0 || chestY < 0 || quantity <= 0)
            return Block(new[] { "museum_withdrawal_target_invalid" });
        int? Stand(string name)
        {
            var rows = selectedStep.Parameters.Where(row => row.Name == name).ToArray();
            return rows.Length == 1 && int.TryParse(rows[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }
        var local = selectedStep.Kind == "transfer_inventory_item";
        TransparentRouteEdge? first = null;
        var targetStandX = local ? Stand("stand_tile_x") : null;
        var targetStandY = local ? Stand("stand_tile_y") : null;
        if (local)
        {
            if (startLocation != chestLocation || selectedStep.LocationId != chestLocation || selectedStep.TileX != chestX ||
                selectedStep.TileY != chestY || !targetStandX.HasValue || !targetStandY.HasValue ||
                Math.Abs((long)targetStandX.Value - chestX) + Math.Abs((long)targetStandY.Value - chestY) != 1)
                return Block(new[] { "museum_withdrawal_compiled_stand_mismatch" });
        }
        else if (selectedStep.Kind != "route_connector_tile")
            return Block(new[] { "museum_withdrawal_navigation_timing_unavailable" });
        else if ((first = ReadFirstConnector(selectedStep)) is null)
            return Block(new[] { "museum_withdrawal_first_connector_incomplete" });
        var approach = new FutureRouteDateEvidenceProducer().Produce(context!, new FutureRouteDateEvidenceRequest
        {
            TotalDays = day, StartLocation = startLocation, StartTileX = startX, StartTileY = startY,
            EarliestDepartureTime = startTime, TargetLocation = chestLocation,
            TargetTileX = targetStandX ?? chestX, TargetTileY = targetStandY ?? chestY,
            RequireExactTargetTile = local, RequiredFirstEdge = first
        }, timing!);
        var stands = approach.Scenario?.ApproachEvidence;
        if (approach.Status != FutureRouteDateEvidenceProductionStatus.Produced ||
            !approach.GuaranteedArrivalByTime.HasValue || stands is not { Length: 1 } ||
            !stands[0].StandTileX.HasValue || !stands[0].StandTileY.HasValue ||
            Math.Abs((long)stands[0].StandTileX!.Value - chestX) + Math.Abs((long)stands[0].StandTileY!.Value - chestY) != 1)
            return Block(approach.BlockingReasons.Length > 0 ? approach.BlockingReasons : new[] { "museum_withdrawal_approach_incomplete" });
        var interactionTicks = MaterialTransferBudgetPolicy.InteractionTicks(quantity);
        var completion = GameClockBudgetPolicy.AddClockMinutes(approach.GuaranteedArrivalByTime.Value,
            GameClockBudgetPolicy.TicksToGameMinutes(interactionTicks));
        var donation = BuildContinuation(day, chestLocation, stands[0].StandTileX!.Value, stands[0].StandTileY!.Value, completion);
        return new(approach.Scenario, approach.GuaranteedArrivalByTime, completion,
            stands[0].StandTileX, stands[0].StandTileY, donation, donation.BlockingReasons);
    }
}

public sealed record MuseumWithdrawalDonationRouteSchedule(
    [property: JsonPropertyName("chest_approach_scenario")] FutureRouteAccessScenario? ChestApproachScenario,
    [property: JsonPropertyName("chest_arrival_by_time")] int? ChestArrivalByTime,
    [property: JsonPropertyName("withdrawal_completion_by_time")] int? WithdrawalCompletionByTime,
    [property: JsonPropertyName("withdrawal_stand_tile_x")] int? WithdrawalStandTileX,
    [property: JsonPropertyName("withdrawal_stand_tile_y")] int? WithdrawalStandTileY,
    [property: JsonPropertyName("donation_route_schedule")] MuseumDonationRouteSchedule? DonationRouteSchedule,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons)
{
    [JsonPropertyName("current_day_withdrawal_donation_route_ready")]
    public bool CurrentDayWithdrawalDonationRouteReady => ChestApproachScenario is not null &&
        WithdrawalCompletionByTime.HasValue && DonationRouteSchedule is { CurrentDayRouteReady: true } && BlockingReasons.Length == 0;
    [JsonPropertyName("requires_fresh_transfer_preflight")]
    public bool RequiresFreshTransferPreflight => true;
    [JsonPropertyName("native_withdrawal_verified")]
    public bool NativeWithdrawalVerified => false;
}
