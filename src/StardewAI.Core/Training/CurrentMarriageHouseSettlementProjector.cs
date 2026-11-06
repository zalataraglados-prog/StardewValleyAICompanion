using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training;

public sealed partial class CurrentMarriageHouseSettlementProjector
{
    private static readonly string[] Seasons = { "spring", "summer", "fall", "winter" };

    public CurrentMarriageHouseSettlementProjection Build(SnapshotEnvelope snapshot)
    {
        var current = new CurrentMarriageHouseStateBuilder().Build(snapshot);
        if (current.Status != "ready_current_state_only") return Blocked(current.BlockingReasons);
        var absoluteDay = (current.Year!.Value - 1L) * 112 + Array.IndexOf(Seasons, current.Season) * 28 + current.Day!.Value - 1;
        if (absoluteDay > int.MaxValue - 115) return Blocked("marriage_house_calendar_overflow");
        var total = (int)absoluteDay;
        if (!TryCalendar(snapshot, current, total, out var blocked, out var error)) return Blocked(error);
        var report = new CurrentMarriageHouseSettlementProjection { SourceStateHash = snapshot.StateHash, CurrentTotalDay = total };
        int? NextAllowed(int start, List<int>? skipped = null)
        {
            if (start > int.MaxValue - 112) return null;
            for (var offset = 0; offset <= 112; offset++)
            {
                var day = start + offset;
                // A proposal may cross New Year into spring, but no current-year green-rain claim covers another summer.
                if (day / 112 + 1 != current.Year && Seasons[day % 112 / 28] == "summer") return null;
                if (!blocked.Contains(DateKey(day))) return day;
                skipped?.Add(day);
            }
            return null;
        }
        var skipped = new List<int>();
        report.ConditionalProposalWeddingTotalDay = NextAllowed(total + 3, skipped);
        report.SkippedProposalWeddingDays = skipped.ToArray();
        report.ConditionalProposalBeforeEvaluation = report.ConditionalProposalWeddingTotalDay.HasValue
            ? report.ConditionalProposalWeddingTotalDay.Value < report.DeadlineTotalDayExclusive : null;
        report.ExistingHouseSettlementTotalDay = current.FarmhouseUpgradeLevel >= 2 ? total :
            current.FarmhouseUpgradeLevel == 1 && current.DaysUntilFarmhouseUpgrade > 0
                ? total + current.DaysUntilFarmhouseUpgrade.Value : null;
        if (current.FarmhouseUpgradeLevel == 1 && current.DaysUntilFarmhouseUpgrade == -1 && current.HouseUpgradeQuote.HasValue)
            // This is conditional on an original legal native purchase, not permission from an unverified quote.
            report.ConditionalNextHousePurchaseSettlementTotalDay = total + 3;
        if (current.MarriedOrRoommate == true) report.ExistingPartnershipSettlementTotalDay = total;
        else if (current.Engaged == true)
        {
            if (!TryExistingWedding(snapshot, current, out var wedding))
                return Blocked("marriage_house_pending_wedding_date_or_native_identity_unavailable");
            if (wedding <= total) return Blocked("marriage_house_due_wedding_requires_native_day_phase_evidence");
            report.ExistingPartnershipSettlementTotalDay = NextAllowed(wedding);
        }
        if (report.ExistingHouseSettlementTotalDay.HasValue && report.ExistingPartnershipSettlementTotalDay.HasValue)
            report.ExistingJointSettlementTotalDay = Math.Max(report.ExistingHouseSettlementTotalDay.Value, report.ExistingPartnershipSettlementTotalDay.Value);
        report.ExistingJointBeforeEvaluation = report.ExistingJointSettlementTotalDay.HasValue
            ? report.ExistingJointSettlementTotalDay.Value < report.DeadlineTotalDayExclusive : null;
        report.Status = "ready_conditional_settlement_projection";
        return report;
    }

    private static bool TryExistingWedding(SnapshotEnvelope snapshot, CurrentMarriageHouseState current, out int wedding)
    {
        wedding = 0;
        var spouse = current.Spouse!;
        if (string.IsNullOrWhiteSpace(spouse) || !ReadableStatus(ReadStateFieldStatus(snapshot, "npcs", "friendships"))) return false;
        var rows = ReadStateFieldValue(snapshot, "npcs", "friendships");
        if (!rows.HasValue || rows.Value.ValueKind != JsonValueKind.Array) return false;
        var matches = rows.Value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty("npc_name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() == spouse).ToArray();
        return matches.Length == 1 && matches[0].TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String && status.GetString() == "Engaged" &&
            matches[0].TryGetProperty("is_engaged", out var engaged) && engaged.ValueKind == JsonValueKind.True &&
            matches[0].TryGetProperty("roommate_marriage", out var roommate) && roommate.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            roommate.GetBoolean() == current.HasCurrentOrPendingRoommate &&
            matches[0].TryGetProperty("wedding_date_total_days", out var date) && date.ValueKind == JsonValueKind.Number &&
            date.TryGetInt32(out wedding) && wedding >= 0;
    }

    private static string DateKey(int totalDay) => Seasons[totalDay % 112 / 28] + (totalDay % 28 + 1).ToString(CultureInfo.InvariantCulture);
    private static CurrentMarriageHouseSettlementProjection Blocked(params string[] errors) => new() { BlockingReasons = errors };
}
