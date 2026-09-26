using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private bool holdCommunityCenterLifecycleEventForExecution;

    private const string CommunityCenterInitialUnlockFixture =
        "community_center_initial_unlock_event";
    private const string CommunityCenterJunimoTextFixture =
        "community_center_junimo_text_event";
    private const string CommunityCenterFinalCeremonyFixture =
        "community_center_final_ceremony_event";

    private static bool IsCommunityCenterLifecycleEventFixture(string profile) =>
        profile is CommunityCenterInitialUnlockFixture or
            CommunityCenterJunimoTextFixture or
            CommunityCenterFinalCeremonyFixture;

    private TrainingExecutionResult ExecuteSetupCommunityCenterLifecycleFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_community_center_lifecycle",
                "community_center.lifecycle_fixture=ready",
                "community_center.lifecycle_fixture=blocked",
                reasons.ToArray());
        }
        if (Game1.getLocationFromName("CommunityCenter") is not CommunityCenter communityCenter)
        {
            return CommunityCenterLifecycleFixtureBlocked(
                request,
                "community_center_lifecycle_location_missing");
        }

        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        StopAllMovement();
        Point target;
        GameLocation location;
        switch (request.CommunityCenterFixtureCase)
        {
            case "first_note_location":
                if (!Game1.player.mailReceived.Contains("ccDoorUnlock") ||
                    Game1.player.hasOrWillReceiveMail("seenJunimoNote") ||
                    Game1.player.hasOrWillReceiveMail("canReadJunimoText") ||
                    Game1.MasterPlayer.hasOrWillReceiveMail("JojaMember"))
                {
                    return CommunityCenterLifecycleFixtureBlocked(
                        request,
                        "community_center_first_note_lifecycle_not_ready");
                }
                if (!communityCenter.isJunimoNoteAtArea(1))
                {
                    communityCenter.addJunimoNote(1);
                }
                var note = CommunityCenterNoteTileRuntime(communityCenter, 1);
                var interaction = CommunityCenterInteractionTileRuntime(
                    communityCenter,
                    1,
                    note);
                var stand = interaction.HasValue
                    ? CommunityCenterFixtureStandTile(
                        communityCenter,
                        interaction.Value)
                    : null;
                if (!note.HasValue || !interaction.HasValue || !stand.HasValue ||
                    !communityCenter.shouldNoteAppearInArea(1))
                {
                    return CommunityCenterLifecycleFixtureBlocked(
                        request,
                        "community_center_first_note_endpoint_unavailable");
                }
                location = communityCenter;
                target = stand.Value;
                break;
            case "sleep_location":
                if (!communityCenter.areAllAreasComplete() ||
                    !Enumerable.Range(0, 6)
                        .Select(RuntimeCommunityCenterAreaCompletionMailId)
                        .Any(mailId => HasPendingCommunityCenterMail(
                            Game1.MasterPlayer,
                            mailId)))
                {
                    return CommunityCenterLifecycleFixtureBlocked(
                        request,
                        "community_center_room_mail_settlement_not_pending");
                }
                if (Utility.getHomeOfFarmer(Game1.player) is not FarmHouse farmHouse)
                {
                    return CommunityCenterLifecycleFixtureBlocked(
                        request,
                        "community_center_lifecycle_home_missing");
                }
                var bed = farmHouse.GetPlayerBedSpot();
                var bedStand = new[]
                {
                    new Point(bed.X - 1, bed.Y),
                    new Point(bed.X + 1, bed.Y),
                    new Point(bed.X, bed.Y + 1),
                    new Point(bed.X, bed.Y - 1)
                }.FirstOrDefault(tile => IsTileWalkable(farmHouse, tile));
                if (bedStand == default)
                {
                    return CommunityCenterLifecycleFixtureBlocked(
                        request,
                        "community_center_lifecycle_bed_stand_unavailable");
                }
                location = farmHouse;
                target = bedStand;
                break;
            default:
                return CommunityCenterLifecycleFixtureBlocked(
                    request,
                    "community_center_lifecycle_fixture_case_invalid");
        }

        Game1.currentLocation = location;
        Game1.player.currentLocation = location;
        Game1.player.Position = target.ToVector2() * Game1.tileSize;
        Game1.player.forceCanMove();
        var verified = ReferenceEquals(Game1.currentLocation, location) &&
            ReferenceEquals(Game1.player.currentLocation, location) &&
            Game1.player.TilePoint == target;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_community_center_lifecycle",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "fixture_relocated_actor_without_completing_lifecycle_transition",
                    "fixture_transition_is_not_training_eligible"
                }
                : new[] { "community_center_lifecycle_fixture_post_state_mismatch" },
            RequestedEffect = "community_center.lifecycle_fixture=" +
                request.CommunityCenterFixtureCase,
            ObservedEffect = "location=" + location.NameOrUniqueName +
                ";tile=" + target.X + "," + target.Y,
            TargetLocation = location.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "community_center_lifecycle_fixture_post_state_mismatch" }
        };
    }

    private TrainingExecutionResult ExecuteSetupCommunityCenterLifecycleEventFixture(
        TrainingExecutionRequest request,
        string profile)
    {
        var spec = CommunityCenterLifecycleEventFixtureSpec(profile);
        if (spec is null)
        {
            return StoryEventFixtureBlocked(
                request,
                "community_center_lifecycle_event_fixture_profile_invalid");
        }
        if (Game1.getLocationFromName("CommunityCenter") is not CommunityCenter communityCenter ||
            Game1.getLocationFromName(spec.LocationId) is not GameLocation location)
        {
            return StoryEventFixtureBlocked(
                request,
                "community_center_lifecycle_event_fixture_location_missing");
        }

        if (profile == CommunityCenterInitialUnlockFixture)
        {
            ResetCommunityCenterLifecycleFixtureBaseline(communityCenter);
        }
        else if (profile == CommunityCenterJunimoTextFixture)
        {
            if (!Game1.player.mailReceived.Contains("seenJunimoNote"))
            {
                return StoryEventFixtureBlocked(
                    request,
                    "community_center_first_junimo_note_required");
            }
            Game1.player.mailReceived.Remove("canReadJunimoText");
            RemovePendingCommunityCenterMail(Game1.player, "canReadJunimoText");
            Game1.player.eventsSeen.Remove(spec.EventId);
        }
        else
        {
            var allAreaMails = Enumerable.Range(0, 6)
                .Select(RuntimeCommunityCenterAreaCompletionMailId)
                .All(Game1.MasterPlayer.mailReceived.Contains);
            if (!communityCenter.areAllAreasComplete() ||
                !allAreaMails ||
                !Game1.MasterPlayer.hasCompletedCommunityCenter())
            {
                return StoryEventFixtureBlocked(
                    request,
                    "community_center_final_ceremony_native_completion_required");
            }
            Game1.player.eventsSeen.Remove(spec.EventId);
            if (!ReferenceEquals(Game1.player, Game1.MasterPlayer))
            {
                Game1.MasterPlayer.eventsSeen.Remove(spec.EventId);
            }
        }

        Dictionary<string, string> events;
        try
        {
            events = Game1.content.Load<Dictionary<string, string>>(
                spec.AssetName);
        }
        catch
        {
            return StoryEventFixtureBlocked(
                request,
                "community_center_lifecycle_event_asset_unavailable");
        }
        if (!events.TryGetValue(spec.EventKey, out var script) ||
            string.IsNullOrWhiteSpace(script))
        {
            return StoryEventFixtureBlocked(
                request,
                "community_center_lifecycle_event_key_missing_or_drifted");
        }

        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        StopAllMovement();
        Game1.currentLocation = location;
        Game1.player.currentLocation = location;
        var triggerTile = profile == CommunityCenterInitialUnlockFixture
            ? new Vector2(0, 54)
            : new Vector2(8, 8);
        Game1.player.Position = triggerTile * Game1.tileSize;
        Game1.player.forceCanMove();
        Game1.timeOfDay = 900;
        Game1.isRaining = false;
        Game1.isSnowing = false;
        Game1.isLightning = false;
        location.checkForEvents();
        var nativeEvent = Game1.CurrentEvent;
        if (nativeEvent is null ||
            !string.Equals(nativeEvent.id, spec.EventId, StringComparison.Ordinal))
        {
            return StoryEventFixtureBlocked(
                request,
                "community_center_lifecycle_native_event_dispatch_mismatch");
        }
        Game1.drawObjectDialogue("Runtime lifecycle fixture hold.");
        holdCommunityCenterLifecycleEventForExecution = true;

        var verified = Game1.eventUp &&
            string.Equals(nativeEvent.id, spec.EventId, StringComparison.Ordinal) &&
            string.Equals(
                nativeEvent.fromAssetName,
                spec.AssetName,
                StringComparison.Ordinal) &&
            nativeEvent.eventCommands.Length == script.Split('/').Length &&
            !Game1.player.eventsSeen.Contains(spec.EventId) &&
            Game1.activeClickableMenu is DialogueBox;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_story_event",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "fixture_loaded_current_runtime_Data_Events_row",
                    "native_GameLocation_checkForEvents_owns_event_dispatch_and_seen_lifecycle",
                    "fixture_uses_native_dialogue_boundary_to_hold_event_before_target_transition",
                    "fixture_transition_is_not_training_eligible"
                }
                : new[] { "community_center_lifecycle_event_fixture_post_state_mismatch" },
            RequestedEffect = "community_center_lifecycle_event_fixture=" + profile,
            ObservedEffect = "event_id=" + (Game1.CurrentEvent?.id ?? "none") +
                ";location=" + location.NameOrUniqueName +
                ";event_up=" + Game1.eventUp.ToString().ToLowerInvariant(),
            TargetLocation = location.NameOrUniqueName,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "community_center_lifecycle_event_fixture_post_state_mismatch" }
        };
    }

    private static void ResetCommunityCenterLifecycleFixtureBaseline(
        CommunityCenter communityCenter)
    {
        foreach (var pair in communityCenter.bundles.Pairs)
        {
            for (var index = 0; index < pair.Value.Length; index++)
            {
                communityCenter.bundles.FieldDict[pair.Key][index] = false;
            }
            communityCenter.bundleRewards[pair.Key] = false;
        }
        for (var area = 0; area < communityCenter.areasComplete.Count; area++)
        {
            communityCenter.areasComplete[area] = false;
        }

        var progressionMails = new[]
        {
            "ccDoorUnlock",
            "seenJunimoNote",
            "wizardJunimoNote",
            "canReadJunimoText",
            "ccIsComplete",
            "ccPantry",
            "ccCraftsRoom",
            "ccFishTank",
            "ccBoilerRoom",
            "ccVault",
            "ccBulletin",
            "ccBulletinThankYou",
            "JojaMember",
            "jojaPantry",
            "jojaCraftsRoom",
            "jojaFishTank",
            "jojaBoilerRoom",
            "jojaVault"
        };
        foreach (var mailId in progressionMails)
        {
            Game1.player.mailReceived.Remove(mailId);
            Game1.MasterPlayer.mailReceived.Remove(mailId);
            RemovePendingCommunityCenterMail(Game1.player, mailId);
            if (!ReferenceEquals(Game1.player, Game1.MasterPlayer))
            {
                RemovePendingCommunityCenterMail(Game1.MasterPlayer, mailId);
            }
        }
        foreach (var eventId in new[] { "611439", "112", "191393" })
        {
            Game1.player.eventsSeen.Remove(eventId);
            Game1.MasterPlayer.eventsSeen.Remove(eventId);
        }
    }

    private static CommunityCenterLifecycleFixtureSpec?
        CommunityCenterLifecycleEventFixtureSpec(string profile) =>
        profile switch
        {
            CommunityCenterInitialUnlockFixture => new(
                "611439",
                "Data\\Events\\Town",
                "Town",
                "611439/j 4/t 800 1300/w sunny/a 0 54/H"),
            CommunityCenterJunimoTextFixture => new(
                "112",
                "Data\\Events\\WizardHouse",
                "WizardHouse",
                "112/n seenJunimoNote"),
            CommunityCenterFinalCeremonyFixture => new(
                "191393",
                "Data\\Events\\Town",
                "Town",
                "191393/Hn ccFishTank/Hn ccBulletin/Hn ccPantry/Hn ccVault/Hn ccBoilerRoom/Hn ccCraftsRoom/Hl jojaFishTank/Hl jojaPantry/Hl jojaVault/Hl jojaBoilerRoom/Hl jojaCraftsRoom/Hl JojaMember/w sunny/H"),
            _ => null
        };

    private sealed record CommunityCenterLifecycleFixtureSpec(
        string EventId,
        string AssetName,
        string LocationId,
        string EventKey);

    private static TrainingExecutionResult CommunityCenterLifecycleFixtureBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) =>
        BlockedWithPrimitive(
            request,
            "debug_setup_community_center_lifecycle",
            "community_center.lifecycle_fixture=ready",
            "community_center.lifecycle_fixture=blocked",
            reasons);
}
