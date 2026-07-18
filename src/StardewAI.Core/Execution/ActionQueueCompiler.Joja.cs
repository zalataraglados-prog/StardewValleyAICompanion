using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompilePurchaseJojaStep(SmallModelAction action)
    {
        var price = ReadIntParameter(action, "price");
        var after = ReadIntParameter(action, "expected_money_after");
        if (!price.HasValue || !after.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }
        var kind = action.OptionId == "executor.purchase_joja_membership"
            ? "purchase_joja_membership"
            : "purchase_joja_project";
        var target = kind == "purchase_joja_membership"
            ? "membership"
            : "project=" + ReadParameter(action, "project_id") + ":button=" + ReadParameter(action, "button_number");
        var effect = kind == "purchase_joja_membership"
            ? "player.money=" + after.Value + ";mail_received.JojaGreeting=true;mail_for_tomorrow.JojaMember=true"
            : "player.money=" + after.Value + ";mail_for_tomorrow." + ReadParameter(action, "cc_mail_id") + "=true;mail_for_tomorrow." + ReadParameter(action, "joja_mail_id") + "=true";
        return new[] { Step(kind, "joja:" + target + ":price=" + price.Value, effect, 300) };
    }

    private static string[] ValidateJojaDevelopmentPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var membership = action.OptionId == "executor.purchase_joja_membership";
        var projectPurchase = action.OptionId == "executor.purchase_joja_project";
        if (!membership && !projectPurchase)
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var actionX = ReadIntParameter(action, "join_action_tile_x");
        var actionY = ReadIntParameter(action, "join_action_tile_y");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var moneyBefore = ReadIntParameter(action, "expected_money_before");
        var price = ReadIntParameter(action, "price");
        var moneyAfter = ReadIntParameter(action, "expected_money_after");
        var expectedContract = membership
            ? "JojaMart.checkAction_JoinJoja_then_signUpForJoja_then_answerDialogue_JojaSignUp_Yes"
            : "JojaMart.checkAction_JoinJoja_then_viewJojaNote_then_JojaCDMenu.receiveLeftClick_checkbox";
        if (!actionX.HasValue || !actionY.HasValue || targetX != actionX || targetY != actionY ||
            !standX.HasValue || !standY.HasValue || Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1 ||
            !moneyBefore.HasValue || !price.HasValue || price.Value < 1 || !moneyAfter.HasValue || moneyAfter.Value != moneyBefore.Value - price.Value ||
            ReadParameter(action, "target_location") != "JojaMart" || ReadParameter(action, "join_action_raw") != "JoinJoja" ||
            ReadParameter(action, "purchase_kind") != (membership ? "membership" : "project") || ReadParameter(action, "native_contract") != expectedContract)
        {
            return new[] { "joja_development_typed_projection_required" };
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("joja_development_menu_must_be_clear");
        }
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "JojaMart", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("joja_development_target_location_mismatch");
        }

        var progress = ReadStateFieldValue(snapshot, "world_progress", "joja_development");
        if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
            ReadBool(progress.Value, "is_current_location") != true ||
            NullableReadInt(progress.Value, "join_action_tile_x") != actionX || NullableReadInt(progress.Value, "join_action_tile_y") != actionY ||
            ReadString(progress.Value, "join_action_raw") != "JoinJoja" || ReadInt(progress.Value, "money") != moneyBefore.Value)
        {
            reasons.Add("joja_development_projection_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        if (membership)
        {
            var greetingBeforeValid = TryBoolParameter(action, "expected_greeting_before", out var greetingBefore);
            var greetingAfterValid = TryBoolParameter(action, "expected_greeting_after", out var greetingAfter);
            if (ReadString(progress.Value, "host_route_state") != "undecided" ||
                ReadBool(progress.Value, "actor_membership_received") == true || ReadBool(progress.Value, "actor_membership_pending") == true ||
                !greetingBeforeValid || !greetingAfterValid || greetingAfter != true || ReadBool(progress.Value, "actor_greeting_received") != greetingBefore ||
                ReadBool(progress.Value, "actor_membership_event_seen") != true ||
                ReadString(progress.Value, "membership_action_status") != "ready" || ReadInt(progress.Value, "membership_price") != price.Value ||
                price.Value != 5000 || ReadParameter(action, "expected_mail_for_tomorrow") != "JojaMember" || ReadParameter(action, "required_event_id") != "611439")
            {
                reasons.Add("joja_membership_projection_drifted");
            }
        }
        else
        {
            var button = ReadIntParameter(action, "button_number");
            if (!button.HasValue || button.Value is < 0 or > 4 ||
                ReadString(progress.Value, "host_route_state") != "joja_locked" || ReadBool(progress.Value, "actor_membership_received") != true ||
                ReadBool(progress.Value, "actor_membership_pending") == true || ReadBool(progress.Value, "completion_ceremony_event_seen") == true ||
                ReadBool(progress.Value, "project_order_pending") == true ||
                !TryFindJojaProject(progress.Value, ReadParameter(action, "project_id"), button.Value, out var project) ||
                ReadString(project, "action_status") != "ready" || ReadBool(project, "complete_or_pending") == true ||
                ReadInt(project, "price") != price.Value || ReadString(project, "cc_mail_id") != ReadParameter(action, "cc_mail_id") ||
                ReadString(project, "joja_mail_id") != ReadParameter(action, "joja_mail_id") ||
                !JojaProjectParametersExact(button.Value, ReadParameter(action, "project_id"), ReadParameter(action, "cc_mail_id"), ReadParameter(action, "joja_mail_id"), price.Value))
            {
                reasons.Add("joja_project_projection_drifted");
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool TryFindJojaProject(JsonElement progress, string? projectId, int button, out JsonElement project)
    {
        project = default;
        if (!progress.TryGetProperty("projects", out var projects) || projects.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var row in projects.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadString(row, "project_id") == projectId && ReadInt(row, "button_number") == button)
            {
                project = row;
                return true;
            }
        }
        return false;
    }

    private static bool JojaProjectParametersExact(int button, string? projectId, string? ccMail, string? jojaMail, int price) => button switch
    {
        0 => projectId == "vault" && ccMail == "ccVault" && jojaMail == "jojaVault" && price == 40000,
        1 => projectId == "boiler_room" && ccMail == "ccBoilerRoom" && jojaMail == "jojaBoilerRoom" && price == 15000,
        2 => projectId == "crafts_room" && ccMail == "ccCraftsRoom" && jojaMail == "jojaCraftsRoom" && price == 25000,
        3 => projectId == "pantry" && ccMail == "ccPantry" && jojaMail == "jojaPantry" && price == 35000,
        4 => projectId == "fish_tank" && ccMail == "ccFishTank" && jojaMail == "jojaFishTank" && price == 20000,
        _ => false
    };
}
