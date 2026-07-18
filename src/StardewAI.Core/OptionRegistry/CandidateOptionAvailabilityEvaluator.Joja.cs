using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] JojaDevelopmentCandidates(SnapshotEnvelope snapshot)
    {
        var progress = ReadStateFieldValue(snapshot, "world_progress", "joja_development");
        if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }

        var row = progress.Value;
        var actionX = NullableReadInt(row, "join_action_tile_x");
        var actionY = NullableReadInt(row, "join_action_tile_y");
        var stand = actionX.HasValue && actionY.HasValue
            ? FindBestStandTile(snapshot, actionX.Value, actionY.Value)
            : null;
        var result = new List<EventCandidate>();

        if (ReadBool(row, "actor_membership_received") != true &&
            ReadBool(row, "actor_membership_pending") != true)
        {
            result.Add(JojaMembershipCandidate(snapshot, row, actionX, actionY, stand));
        }

        if (row.TryGetProperty("projects", out var projects) && projects.ValueKind == JsonValueKind.Array)
        {
            result.AddRange(projects.EnumerateArray()
                .Where(project => project.ValueKind == JsonValueKind.Object && ReadBool(project, "complete_or_pending") != true)
                .Select(project => JojaProjectCandidate(snapshot, row, project, actionX, actionY, stand)));
        }
        return result.ToArray();
    }

    private static EventCandidate JojaMembershipCandidate(
        SnapshotEnvelope snapshot,
        JsonElement progress,
        int? actionX,
        int? actionY,
        CandidateTile? stand)
    {
        var reasons = JojaCommonReasons(progress, actionX, actionY, stand);
        var status = ReadString(progress, "membership_action_status");
        if (status != "ready")
        {
            reasons.Add(string.IsNullOrWhiteSpace(status) ? "joja_membership_projection_unavailable" : status);
        }
        var price = ReadInt(progress, "membership_price");
        var money = ReadInt(progress, "money");
        if (price != 5000 || money < price)
        {
            reasons.Add("joja_membership_price_or_money_projection_invalid");
        }
        var parameters = stand is null || !actionX.HasValue || !actionY.HasValue
            ? Array.Empty<SmallModelActionParameter>()
            : new[]
            {
                Parameter("target_location", "JojaMart"),
                Parameter("stand_tile_x", stand.X.ToString()),
                Parameter("stand_tile_y", stand.Y.ToString()),
                Parameter("join_action_tile_x", actionX.Value.ToString()),
                Parameter("join_action_tile_y", actionY.Value.ToString()),
                Parameter("join_action_raw", ReadString(progress, "join_action_raw")),
                Parameter("purchase_kind", "membership"),
                Parameter("expected_money_before", money.ToString()),
                Parameter("price", price.ToString()),
                Parameter("expected_money_after", (money - price).ToString()),
                Parameter("expected_mail_for_tomorrow", "JojaMember"),
                Parameter("expected_greeting_before", ReadBool(progress, "actor_greeting_received") == true ? "true" : "false"),
                Parameter("expected_greeting_after", "true"),
                Parameter("required_event_id", "611439"),
                Parameter("native_contract", "JojaMart.checkAction_JoinJoja_then_signUpForJoja_then_answerDialogue_JojaSignUp_Yes")
            };
        return JojaCandidate(snapshot, "joja-membership", "purchase_joja_membership", actionX, actionY, stand,
            "mail_received.JojaGreeting=true;mail_for_tomorrow.JojaMember=true;player.money=" + (money - price), reasons, parameters);
    }

    private static EventCandidate JojaProjectCandidate(
        SnapshotEnvelope snapshot,
        JsonElement progress,
        JsonElement project,
        int? actionX,
        int? actionY,
        CandidateTile? stand)
    {
        var reasons = JojaCommonReasons(progress, actionX, actionY, stand);
        var status = ReadString(project, "action_status");
        if (status != "ready")
        {
            reasons.Add(string.IsNullOrWhiteSpace(status) ? "joja_project_projection_unavailable" : status);
        }
        var button = ReadInt(project, "button_number");
        var price = ReadInt(project, "price");
        var money = ReadInt(progress, "money");
        var projectId = ReadString(project, "project_id");
        var ccMail = ReadString(project, "cc_mail_id");
        var jojaMail = ReadString(project, "joja_mail_id");
        if (!JojaProjectProjectionExact(button, projectId, ccMail, jojaMail, price) || money < price)
        {
            reasons.Add("joja_project_typed_projection_invalid");
        }
        var parameters = stand is null || !actionX.HasValue || !actionY.HasValue
            ? Array.Empty<SmallModelActionParameter>()
            : new[]
            {
                Parameter("target_location", "JojaMart"),
                Parameter("stand_tile_x", stand.X.ToString()),
                Parameter("stand_tile_y", stand.Y.ToString()),
                Parameter("join_action_tile_x", actionX.Value.ToString()),
                Parameter("join_action_tile_y", actionY.Value.ToString()),
                Parameter("join_action_raw", ReadString(progress, "join_action_raw")),
                Parameter("purchase_kind", "project"),
                Parameter("project_id", projectId),
                Parameter("button_number", button.ToString()),
                Parameter("cc_mail_id", ccMail),
                Parameter("joja_mail_id", jojaMail),
                Parameter("expected_money_before", money.ToString()),
                Parameter("price", price.ToString()),
                Parameter("expected_money_after", (money - price).ToString()),
                Parameter("native_contract", "JojaMart.checkAction_JoinJoja_then_viewJojaNote_then_JojaCDMenu.receiveLeftClick_checkbox")
            };
        return JojaCandidate(snapshot, "joja-project:" + projectId, "purchase_joja_project", actionX, actionY, stand,
            "mail_for_tomorrow." + ccMail + "=true;mail_for_tomorrow." + jojaMail + "=true;player.money=" + (money - price), reasons, parameters);
    }

    private static List<string> JojaCommonReasons(JsonElement progress, int? actionX, int? actionY, CandidateTile? stand)
    {
        var reasons = new List<string>();
        if (ReadBool(progress, "location_accessible") != true)
        {
            reasons.Add("joja_mart_not_accessible");
        }
        if (!actionX.HasValue || !actionY.HasValue || ReadString(progress, "join_action_raw") != "JoinJoja")
        {
            reasons.Add("join_joja_action_tile_unavailable");
        }
        if (stand is null)
        {
            reasons.Add("joja_mart_no_reachable_counter_stand_tile");
        }
        return reasons;
    }

    private static EventCandidate JojaCandidate(
        SnapshotEnvelope snapshot,
        string id,
        string kind,
        int? actionX,
        int? actionY,
        CandidateTile? stand,
        string expectedEffect,
        List<string> reasons,
        SmallModelActionParameter[] parameters)
    {
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
        return new EventCandidate
        {
            CandidateId = id,
            Kind = kind,
            Available = reasons.Count == 0,
            LocationId = "JojaMart",
            TileX = actionX,
            TileY = actionY,
            ExpectedEffect = expectedEffect,
            Quantity = 1,
            EstimatedTicks = Math.Max(300, distance * 60 + 300),
            AvailabilityClass = "transparent_native_joja_development",
            AllowedNow = reasons.Count == 0,
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static bool JojaProjectProjectionExact(int button, string projectId, string ccMail, string jojaMail, int price) => button switch
    {
        0 => projectId == "vault" && ccMail == "ccVault" && jojaMail == "jojaVault" && price == 40000,
        1 => projectId == "boiler_room" && ccMail == "ccBoilerRoom" && jojaMail == "jojaBoilerRoom" && price == 15000,
        2 => projectId == "crafts_room" && ccMail == "ccCraftsRoom" && jojaMail == "jojaCraftsRoom" && price == 25000,
        3 => projectId == "pantry" && ccMail == "ccPantry" && jojaMail == "jojaPantry" && price == 35000,
        4 => projectId == "fish_tank" && ccMail == "ccFishTank" && jojaMail == "jojaFishTank" && price == 20000,
        _ => false
    };
}
