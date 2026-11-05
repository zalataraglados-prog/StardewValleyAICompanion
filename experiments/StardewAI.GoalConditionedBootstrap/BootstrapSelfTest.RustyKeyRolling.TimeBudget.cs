using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRollingDonationTimeBudget(List<string> passed)
    {
        foreach (var (time, ready) in new[] { (1000, true), (1852, true), (1853, false), (1900, false), (2550, false) })
        {
            var fixture = new RustyKeyPortfolioFixture();
            fixture.EnableHeldDonation();
            ChangeClock(fixture, clock => clock["time"]!["value"] = time);
            var result = fixture.RunRolling(() => throw new InvalidOperationException("Owned pending supply must not be reacquired."));
            Require(result.RollingStepReady == ready && (result.CompiledQueue is not null) == ready &&
                result.SelectedAcquisitionProposal is null && !result.ThresholdSatisfied && !result.FormalTrainingAuthorized,
                "Held donation must fit the original autonomous day budget: " + time);
            if (!ready && time < 1900)
                Require(result.CandidateEvaluations.Any(row => row.BlockingReasons.Contains("aggregate_time_budget_exceeded")),
                    "Rejected held donation must expose the original compiler's budget reason.");
            if (time >= 1900)
                Require(result.CandidateEvaluations.Length == 0 && result.BlockingReasons.Contains("rusty_key_current_day_recovery_not_dispatchable"),
                    "After the original recovery window starts, this fixture's missing home must block recovery, not probe donation budgets.");
            passed.Add("held_donation_day_budget_" + time);
        }
        foreach (var mode in new[] { "missing", "unreadable", "malformed", "invalid_hhmm", "conflicting_alias" })
        {
            var fixture = new RustyKeyPortfolioFixture();
            fixture.EnableHeldDonation();
            ChangeClock(fixture, clock =>
            {
                switch (mode)
                {
                    case "missing": clock.Remove("time"); break;
                    case "unreadable": clock["time"]!["status"] = "unavailable"; break;
                    case "malformed": clock["time"]!["value"] = "1000"; break;
                    case "invalid_hhmm": clock["time"]!["value"] = 1860; break;
                    case "conflicting_alias": clock["time_of_day"] = JsonSerializer.SerializeToNode(new { value = 1100, status = "available" }); break;
                }
            });
            var result = fixture.RunRolling(() => throw new InvalidOperationException("Unknown clocks must not trigger duplicate acquisition."));
            Require(!result.RollingStepReady && result.CompiledQueue is null && result.SelectedAcquisitionProposal is null &&
                result.CandidateEvaluations.Any(row => row.BlockingReasons.Contains("rusty_key_held_donation_clock_unavailable")),
                "A missing, unreadable or conflicting clock cannot grant an unlimited held-donation budget: " + mode);
            passed.Add("held_donation_clock_" + mode);
        }

        static void ChangeClock(RustyKeyPortfolioFixture fixture, Action<JsonObject> mutate)
        {
            var clock = JsonSerializer.SerializeToNode(fixture.Snapshot.State["time"])!.AsObject();
            mutate(clock);
            fixture.Snapshot.State["time"] = JsonSerializer.SerializeToElement(clock);
            fixture.Rehash();
        }
    }
}
