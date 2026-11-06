using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class CurrentMuseumCollectionIntakeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void NativeJoinTransitionKeepsAll95RowsAndConsumesExactlyOneSelectedCopy(int initialStack)
    {
        var transition = ValidateNativeTransition(NativeTransition(initialStack));
        Assert.Equal(initialStack, transition.GetProperty("stackBefore").GetInt32());
        Assert.Equal(initialStack - 1, transition.GetProperty("stackAfter").GetInt32());
        var rows = transition.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(95, rows.Length);
        var changed = Assert.Single(rows.Where(row => row.GetProperty("donated_before").GetBoolean() != row.GetProperty("donated_after").GetBoolean()));
        Assert.Equal("(O)96", changed.GetProperty("qualified_item_id").GetString());
        Assert.All(rows, row => Assert.True(row.GetProperty("donated_after").GetBoolean()));
    }

    [Theory]
    [InlineData("missing_row")]
    [InlineData("duplicate_row")]
    [InlineData("unknown_row")]
    [InlineData("count_only_completion")]
    [InlineData("wrong_missing_set")]
    [InlineData("no_achievement")]
    [InlineData("achievement_disagreement")]
    [InlineData("wrong_selected_delta")]
    [InlineData("wrong_quality")]
    [InlineData("wrong_item")]
    [InlineData("missing_slot")]
    [InlineData("duplicate_slot")]
    [InlineData("spurious_total_copy")]
    [InlineData("menu_open")]
    public void NativeJoinTransitionRejectsIncompleteDenominatorsOrUnobservedEffects(string mutation)
    {
        var fixture = NativeTransition();
        EditAfter(fixture, state =>
        {
            var museum = state["world_progress"]!["museum"]!["value"]!;
            var slot = state["player"]!["inventory"]!["value"]![0]!;
            switch (mutation)
            {
                case "missing_row": museum["donatable_items"]!.AsArray().RemoveAt(0); break;
                case "duplicate_row": museum["donatable_items"]![1] = museum["donatable_items"]![0]!.DeepClone(); break;
                case "unknown_row": museum["donatable_items"]![0]!["qualified_item_id"] = "(O)unknown"; break;
                case "count_only_completion": museum["donatable_items"]![94]!["donated"] = false; break;
                case "wrong_missing_set": museum["missing_item_ids"]!.AsArray().Add("96"); break;
                case "no_achievement":
                    museum["complete_collection_achievement_received"] = false;
                    state["world_progress"]!["achievements"]!["value"] = new JsonArray(28); break;
                case "achievement_disagreement": state["world_progress"]!["achievements"]!["value"] = new JsonArray(28); break;
                case "wrong_selected_delta": slot["stack"] = 2; break;
                case "wrong_quality": slot["quality"] = 1; break;
                case "wrong_item": slot["qualified_item_id"] = "(O)97"; break;
                case "missing_slot": state["player"]!["inventory"]!["value"]!.AsArray().Clear(); break;
                case "duplicate_slot": state["player"]!["inventory"]!["value"]!.AsArray().Add(slot.DeepClone()); break;
                case "spurious_total_copy":
                    var added = slot.DeepClone();added["slot_index"] = 2;
                    state["player"]!["inventory"]!["value"]!.AsArray().Add(added); break;
                case "menu_open": state["menus"]!["active_menu"]!["value"]!["is_open"] = true; break;
            }
        });
        Assert.Throws<InvalidDataException>(() => ValidateNativeTransition(fixture));
    }

    [Theory]
    [InlineData("missing_before_row")]
    [InlineData("wrong_before_item")]
    [InlineData("before_already_earned")]
    [InlineData("wrong_source_hash")]
    public void NativeJoinCannotShrinkOrBorrowOriginalIntake(string mutation)
    {
        var fixture = NativeTransition();
        var intake = fixture.Intake;
        if (mutation == "missing_before_row") intake = intake with { Items = intake.Items.Skip(1).ToArray() };
        if (mutation == "wrong_before_item") intake = intake with { Items = intake.Items.Select(row => row.Donated ? row : row with { QualifiedItemId = "(O)97" }).ToArray() };
        if (mutation == "before_already_earned") intake = intake with { NativeAchievementReceived = true };
        if (mutation == "wrong_source_hash") intake = intake with { SourceStateHash = "wrong" };
        Assert.Throws<InvalidDataException>(() => ValidateNativeTransition(fixture with { Intake = intake }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-candidate")]
    public void NativeJoinPublicEntryRejectsMissingRunOrPreferredIdentity(string mutation)
    {
        var (files, _, _) = TerminalFixture();
        var path = Path.Combine(Path.GetDirectoryName(files.Snapshot)!, "join-request.json");
        Write(path, new MuseumTerminalNativeJoinRequest("museum_terminal_native_join_request.v1", mutation == "" ? "" : "run",
            "", "", "", "", "", mutation == "missing-candidate" ? "" : "candidate"));
        Assert.Throws<InvalidDataException>(() => MuseumTerminalNativeJoinBuilder.Build(path));
    }
}
