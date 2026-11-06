using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class CurrentMuseumCollectionIntakeTests
{
    [Fact]
    public void ReusesFullMuseumDenominatorAndOriginalCandidatesWithoutOtherCollectionRoots()
    {
        var f = Fixture();
        var before = File.ReadAllText(f.Snapshot);
        var report = Build(f);
        Assert.Equal("ready_current_state_only", report.Status);
        Assert.Equal(95, report.Items.Length);
        Assert.Equal(59, report.DonatedCount);
        Assert.Equal(36, report.MissingItemCount);
        Assert.Equal(59, report.Items.Count(row => row.Donated));
        Assert.False(report.CollectionComplete);
        Assert.False(report.NativeAchievementReceived);
        Assert.False(report.LocalTerminalFactsSatisfied);
        Assert.Equal("museum.donate_items", Assert.Single(report.ConsideredOptionIds));
        Assert.Equal("museum.donate_items", Assert.Single(report.Availability.Options).OptionId);
        Assert.NotEmpty(Assert.Single(report.Availability.Options).EventCandidates);
        Assert.Equal("original_dependency_metadata", report.OriginalDirection.GetProperty("retained_extension").GetString());
        Assert.False(report.ResourceReservationsVerified);
        Assert.False(report.TrainingLabelEligible);
        Assert.False(report.NativeOutcomeVerified);
        Assert.False(report.FullRootVerified);
        Assert.False(report.FormalProductTrainingAuthorized);
        Write(f.Intake, report);
        Assert.Equal(JsonSerializer.Serialize(report, Json),
            JsonSerializer.Serialize(CurrentMuseumCollectionIntakeBuilder.Verify(f.Intake), Json));
        Assert.Equal(before, File.ReadAllText(f.Snapshot));
    }

    [Theory]
    [InlineData(94, true, false)]
    [InlineData(95, false, false)]
    [InlineData(95, true, true)]
    public void PersistentAchievementAndCollectionCompletionAreSeparateCurrentFacts(int donated, bool achievement, bool terminal)
    {
        var report = Build(Fixture(donated, achievement));
        Assert.Equal(donated == 95, report.CollectionComplete);
        Assert.Equal(achievement, report.NativeAchievementReceived);
        Assert.Equal(terminal, report.LocalTerminalFactsSatisfied);
        Assert.False(report.NativeOutcomeVerified);
        Assert.False(report.TrainingLabelEligible);
    }

    [Fact]
    public void KeepsUnavailableDonationCandidatesInsteadOfFilteringTheDenominator()
    {
        var f = Fixture();
        Edit(f.Snapshot, root => root["state"]!["world_progress"]!["museum"]!["value"]!["donation_candidates"]![0]!
            ["action_status"] = "museum_mutex_locked", snapshot: true);
        var report = Build(f);
        Assert.Equal(95, report.Items.Length);
        var candidate = Assert.Single(Assert.Single(report.Availability.Options).EventCandidates);
        Assert.False(candidate.Available);
        Assert.NotEmpty(candidate.BlockReasons);
    }

    [Theory]
    [InlineData("missing_row")]
    [InlineData("duplicate_row")]
    [InlineData("unknown_row")]
    [InlineData("wrong_count")]
    [InlineData("wrong_missing_set")]
    [InlineData("achievement_disagreement")]
    [InlineData("unreadable_museum")]
    [InlineData("unreadable_achievements")]
    [InlineData("mistyped_achievement")]
    [InlineData("wrong_version")]
    [InlineData("wrong_hash")]
    [InlineData("foreign_actor")]
    public void RejectsInconsistentOrUnavailableCurrentFacts(string mutation)
    {
        var f = Fixture();
        Edit(f.Snapshot, root =>
        {
            var world = root["state"]!["world_progress"]!;
            var museum = world["museum"]!["value"]!;
            switch (mutation)
            {
                case "missing_row": museum["donatable_items"]!.AsArray().RemoveAt(0); break;
                case "duplicate_row": museum["donatable_items"]![1] = museum["donatable_items"]![0]!.DeepClone(); break;
                case "unknown_row": museum["donatable_items"]![0]!["item_id"] = "unknown"; break;
                case "wrong_count": museum["donated_count"] = 60; break;
                case "wrong_missing_set": museum["missing_item_ids"]!.AsArray().RemoveAt(0); break;
                case "achievement_disagreement": museum["complete_collection_achievement_received"] = true; break;
                case "unreadable_museum": world["museum"]!["status"] = "unavailable"; break;
                case "unreadable_achievements": world["achievements"]!["status"] = "unavailable"; break;
                case "mistyped_achievement": world["achievements"]!["value"]![0] = "5"; break;
                case "wrong_version": root["game_version"] = "1.6.14"; break;
                case "wrong_hash": root["state_hash"] = "wrong"; break;
                case "foreign_actor": root["player_id"]!["status"] = "unavailable"; break;
            }
        }, snapshot: true, preserveHash: mutation == "wrong_hash");
        Assert.Throws<InvalidDataException>(() => Build(f));
    }

    [Theory]
    [InlineData("wrong_goal_rule")]
    [InlineData("incomplete_authority")]
    [InlineData("duplicate_requirement")]
    [InlineData("duplicate_item")]
    [InlineData("smaller_denominator")]
    [InlineData("wrong_quantity")]
    [InlineData("wrong_criterion")]
    [InlineData("scoped_graph")]
    [InlineData("missing_option")]
    [InlineData("foreign_option")]
    [InlineData("duplicate_direction")]
    public void RejectsShrunkenAuthorityOrChangedOriginalOwnership(string mutation)
    {
        var f = Fixture();
        if (mutation == "wrong_goal_rule") Edit(f.Knowledge, root => root["grandpa_goal"]!["criteria"]![0]!["target"] = "28");
        else if (mutation is "scoped_graph" or "missing_option" or "foreign_option" or "duplicate_direction")
            Edit(f.Dependencies, root =>
            {
                if (mutation == "scoped_graph") root["marriage_local_method_view"] = new System.Text.Json.Nodes.JsonObject();
                if (mutation == "missing_option") root["directions"]![0]!["nodes"]!.AsArray().Clear();
                if (mutation == "foreign_option") root["directions"]![0]!["nodes"]![0]!["option_id"] = "social.gift_npc";
                if (mutation == "duplicate_direction") root["directions"]!.AsArray().Add(root["directions"]![0]!.DeepClone());
            });
        else Edit(f.Inventory, root =>
        {
            var set = root["requirement_sets"]![0]!;
            switch (mutation)
            {
                case "incomplete_authority": root["denominator_complete"] = false; break;
                case "duplicate_requirement": set["groups"]![1]!["requirement_id"] = set["groups"]![0]!["requirement_id"]!.DeepClone(); break;
                case "duplicate_item": set["groups"]![1]!["alternatives"]![0] = set["groups"]![0]!["alternatives"]![0]!.DeepClone(); break;
                case "smaller_denominator": set["required_group_count"] = 94; break;
                case "wrong_quantity": set["groups"]![0]!["alternatives"]![0]!["amount"] = 2; break;
                case "wrong_criterion": set["criterion_id"] = "rusty_key"; break;
            }
        });
        Assert.Throws<InvalidDataException>(() => Build(f));
    }

    [Theory]
    [InlineData("stored_flag")]
    [InlineData("stored_rows")]
    [InlineData("source_changed")]
    public void VerifyRebuildsSourcesAndRejectsHandEditedClaims(string mutation)
    {
        var f = Fixture();
        Write(f.Intake, Build(f));
        if (mutation == "source_changed") Edit(f.Dependencies, root => root["directions"]![0]!["retained_extension"] = "changed");
        else Edit(f.Intake, root =>
        {
            if (mutation == "stored_flag") root["trainingLabelEligible"] = true;
            if (mutation == "stored_rows") root["items"]!.AsArray().RemoveAt(0);
        });
        Assert.Throws<InvalidDataException>(() => CurrentMuseumCollectionIntakeBuilder.Verify(f.Intake));
    }
}
