using System.Text.Json;
using StardewAI.Contracts.Mail;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MailProcessingMainlineTests
{
    [Fact]
    public void OwnedMailboxAndOpenLetterCompileThroughExistingInteractAndClosePrimitives()
    {
        var mailbox = Snapshot(MailboxState());
        var mailboxAvailability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            mailbox, new[] { "mail.process_letter" }, true);
        var openCandidate = Assert.Single(mailboxAvailability.Options.Single().EventCandidates);
        Assert.True(openCandidate.Available, string.Join(";", openCandidate.BlockReasons));
        Assert.Equal("open_mailbox_letter", openCandidate.Kind);

        var openPlan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new(), mailboxAvailability), mailbox.StateHash);
        var openStep = Assert.Single(openPlan.Steps);
        Assert.Equal("interact", openStep.Kind);
        var openQueue = new ActionQueueCompiler().Compile(openPlan, mailbox);
        var openItem = Assert.Single(openQueue.Items);
        Assert.Equal("executor.interact", openItem.OptionId);
        Assert.Empty(openItem.BlockingReasons);
        Assert.Contains(openItem.NormalizedCommand.Parameters, row => row.Name == "expected_action_type" && row.Value == "Mailbox");

        var letter = Snapshot(LetterState());
        var letterAvailability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            letter, new[] { "mail.process_letter" }, true);
        var processCandidate = Assert.Single(letterAvailability.Options.Single().EventCandidates);
        Assert.True(processCandidate.Available, string.Join(";", processCandidate.BlockReasons));
        Assert.Equal("process_open_letter", processCandidate.Kind);

        var closePlan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new(), letterAvailability), letter.StateHash);
        var closeStep = Assert.Single(closePlan.Steps);
        Assert.Equal("close_menu", closeStep.Kind);
        var closeQueue = new ActionQueueCompiler().Compile(closePlan, letter);
        var closeItem = Assert.Single(closeQueue.Items);
        Assert.Equal("executor.close_menu", closeItem.OptionId);
        Assert.Empty(closeItem.BlockingReasons);
        Assert.Contains(closeItem.NormalizedCommand.Parameters, row => row.Name == "target_runtime_identity" && row.Value == "mail-fixture");
        Assert.Contains(closeItem.NormalizedCommand.Parameters, row => row.Name == "mail_attachment_slots_required" && row.Value == "1");
    }

    [Fact]
    public void MailDirectiveParserCoversEveryNativeCommandAndQuotedActions()
    {
        var text = "%action AddMail Current \"quoted value\"; AddQuest 10 %%" +
            string.Join(string.Empty, MailDirectiveParser.KnownItemCommands.Select((command, index) =>
                command switch
                {
                    "id" => "%item id (O)388 1 %%",
                    "object" => "%item object (O)388 1 %%",
                    "tools" => "%item tools Axe %%",
                    "bigobject" => "%item bigobject 130 %%",
                    "furniture" => "%item furniture 1308 %%",
                    "money" => "%item money 100 %%",
                    "conversationtopic" => "%item conversationtopic test 2 %%",
                    "cookingrecipe" => "%item cookingrecipe Fried_Egg %%",
                    "craftingrecipe" => "%item craftingrecipe Chest %%",
                    "itemrecovery" => "%item itemrecovery %%",
                    "quest" => "%item quest 10 %%",
                    "specialorder" => "%item specialorder Robin2 true %%",
                    _ => throw new InvalidOperationException(command + index)
                }));

        var parsed = MailDirectiveParser.Parse(text);
        Assert.Equal(13, parsed.Length);
        Assert.Equal(MailDirectiveParser.KnownItemCommands.OrderBy(value => value),
            parsed.Where(row => row.Kind == "item").Select(row => row.Command).OrderBy(value => value));
        Assert.All(parsed, row => Assert.Empty(row.Errors));
        Assert.Contains(parsed[0].Arguments, value => value.StartsWith("quoted value", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeMailPathUsesOnlyNativeMenuAndMailboxEntryPoints()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Mail.cs"));
        var interact = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Interact.cs"));

        Assert.Contains("LetterViewerMenu", runtime, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.Contains("ItemGrabMenu { source: 4 }", runtime, StringComparison.Ordinal);
        Assert.Contains("Game1.currentLocation.checkAction", interact, StringComparison.Ordinal);
        Assert.DoesNotContain("mailbox.Remove", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Money +=", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("craftingRecipes", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("cookingRecipes", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("questLog.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSpecialOrder", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("maxStamina.Value +=", runtime, StringComparison.Ordinal);
    }

    private static string MailboxState() => """
    {
      "player": {
        "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_x":{"value":6,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_y":{"value":12,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "facing_direction":{"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "inventory_capacity":{"value":{"empty_slots":12},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "quests": {
        "mailbox":{"value":["mail-fixture"],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "mail_received":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "mailbox_processing":{"value":{"available":true,"queue_count":1,"queue_mail_ids_native_order":["mail-fixture"],"pending_mail_id":"mail-fixture","mail_data_found":true,"mail_data_sha256":"abc","directives":[],"constructor_effect_classes":[],"attachment_slot_upper_bound":0,"inventory_empty_slots":12,"attachment_capacity_sufficient":true,"mailbox_location_id":"Farm","mailbox_action_tile_x":6,"mailbox_action_tile_y":11,"stand_tile_x":6,"stand_tile_y":12,"menu_clear":true,"status":"ready","blocked_diagnostics":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "locations": {
        "route_graph":{"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "route_action_branch_coverage":{"value":{"rows":[{"tile_x":6,"tile_y":11,"branch":"Mailbox","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "current_location": {
        "route_context":{"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "menus": {
        "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "menu_specific_state":{"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "time":{"time":{"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
    }
    """;

    private static string LetterState() => """
    {
      "player": {
        "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_x":{"value":6,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_y":{"value":12,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "facing_direction":{"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "inventory_capacity":{"value":{"empty_slots":2},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "quests": {
        "mailbox":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "mail_received":{"value":["mail-fixture"],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "mailbox_processing":{"value":{"available":false,"queue_count":0,"blocked_diagnostics":["mailbox_queue_empty"]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "locations": {
        "route_graph":{"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "current_location": {
        "route_context":{"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "menus": {
        "active_menu":{"value":{"is_open":true,"type":"LetterViewerMenu"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "sleep_prompt_context":{"value":{"prompt_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "menu_specific_state":{"value":{"kind":"letter_viewer","mail_title":"mail-fixture","is_mail":true,"is_from_collection":false,"page":0,"page_count":2,"message_pages":["one","two"],"scale":1,"can_receive_input":true,"ready_to_close":true,"has_interactable":true,"should_show_interactable":false,"items_left_to_grab":true,"attachment_count":2,"attachments":[{"index":0,"present":true,"visible":false,"qualified_item_id":"(O)388","item_id":"388","display_name":"Wood","stack":50,"quality":0},{"index":1,"present":true,"visible":false,"qualified_item_id":"(O)434","item_id":"434","display_name":"Stardrop","stack":1,"quality":0}],"quest_id":"113","special_order_id":null,"has_quest_or_special_order":true,"money_included":0,"learned_recipe":"","cooking_or_crafting":"","destroy":false,"menu_identity_sha256":"identity"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "time":{"time":{"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
    }
    """;

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-11T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
