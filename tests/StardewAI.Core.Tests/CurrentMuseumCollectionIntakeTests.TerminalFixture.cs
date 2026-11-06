using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class CurrentMuseumCollectionIntakeTests
{
    private static (IntakeFiles Files, string Request, string Ledger) TerminalFixture()
    {
        var f = Fixture(94);
        Edit(f.Snapshot, root =>
        {
            var donor = root["state"]!["world_progress"]!["museum"]!["value"]!["donation_candidates"]![0]!;
            donor["donated_count_before"] = 94;
            donor["donated_count_after"] = 95;
            donor["completes_collection"] = true;
            donor["expected_complete_collection_achievement_after"] = true;
            donor["reaches_rusty_key_threshold"] = false;
            donor["pending_reward_ids_after"] = new JsonArray("museumComplete");
            donor["newly_pending_reward_ids"] = new JsonArray("museumComplete");
            donor["auto_applied_reward_ids"] = new JsonArray();
            donor["auto_applied_reward_actions"] = new JsonArray();
        }, snapshot: true);
        Write(f.Intake, Build(f));
        var directory = Path.GetDirectoryName(f.Snapshot)!;
        var request = Path.Combine(directory, "terminal-comparison-request.json");
        var ledger = Path.Combine(directory, "ledger.json");
        Write(request, new MuseumTerminalTeacherComparisonRequest { IntakePath = f.Intake, StrategyLedgerPath = ledger });
        return (f, request, ledger);
    }

    private static void RefreshIntake(IntakeFiles f) => Write(f.Intake, Build(f));
}
