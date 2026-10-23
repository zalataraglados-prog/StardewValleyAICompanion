using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using StardewAI.Contracts.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static GoldenWalnutSourceCatalogRef? ReadGoldenWalnutSources(
        Farmer? master,
        Farmer? actor,
        NetWorldState? world)
    {
        var team = actor?.team ?? master?.team;
        if (master is null || team is null || world is null)
            return null;

        var sources = GoldenWalnutNativeSourceCatalog.Sources
            .Select(definition => ProjectGoldenWalnutSource(
                definition,
                master,
                actor ?? master,
                team,
                world))
            .OrderBy(source => source.SourceKey, StringComparer.Ordinal)
            .ToArray();
        var collected = sources.Sum(source => source.CollectedQuantity);
        return new GoldenWalnutSourceCatalogRef
        {
            SchemaVersion = GoldenWalnutNativeSourceCatalog.SchemaVersion,
            ProjectionStatus = GoldenWalnutNativeSourceCatalog.ProjectionStatus,
            NativeSourceKeyCount = GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount,
            NativeWalnutTotal = GoldenWalnutNativeSourceCatalog.NativeWalnutTotal,
            TrackedCollectedQuantity = collected,
            TrackedRemainingQuantity = sources.Sum(source => source.RemainingQuantity),
            PreResortRemainingGrossQuantity = sources
                .Where(source => source.PreResortEligible)
                .Sum(source => source.RemainingQuantity),
            PreResortSpendSafeRemainingQuantity = sources
                .Where(source => source.SpendSafeBeforeResort)
                .Sum(source => source.RemainingQuantity),
            WorldGoldenWalnutsFound = world.GoldenWalnutsFound,
            TrackerToWorldFoundDelta = collected - world.GoldenWalnutsFound,
            Sources = sources
        };
    }

    private static GoldenWalnutSourceProgressRef ProjectGoldenWalnutSource(
        GoldenWalnutNativeSourceDefinition definition,
        Farmer master,
        Farmer actor,
        FarmerTeam team,
        NetWorldState world)
    {
        var collected = definition.TrackingKind switch
        {
            "tracker" => team.collectedNutTracker.Contains(definition.SourceKey)
                ? definition.NativeQuantity
                : 0,
            "limited_drop" => Math.Clamp(
                team.GetDroppedLimitedNutCount(definition.SourceKey),
                0,
                definition.NativeQuantity),
            "world_flag" when definition.SourceKey == "GoldenCoconutCracked" =>
                world.GoldenCoconutCracked ? definition.NativeQuantity : 0,
            "master_mail" => master.hasOrWillReceiveMail(definition.SourceKey)
                ? definition.NativeQuantity
                : 0,
            _ => 0
        };
        var requiredMailSatisfied = string.IsNullOrWhiteSpace(definition.RequiredMailKey) ||
            master.hasOrWillReceiveMail(definition.RequiredMailKey);
        var remainingUnlockCost = requiredMailSatisfied
            ? 0
            : definition.OptionalUnlockWalnutCost;
        var preResortEligible = !definition.ResortOnly;

        return new GoldenWalnutSourceProgressRef
        {
            SourceKey = definition.SourceKey,
            TrackingKind = definition.TrackingKind,
            LocationId = definition.LocationId,
            TileX = definition.TileX,
            TileY = definition.TileY,
            NativeQuantity = definition.NativeQuantity,
            CollectedQuantity = collected,
            RemainingQuantity = definition.NativeQuantity - collected,
            AcquisitionOptionId = definition.AcquisitionOptionId,
            ActionBindingStatus = GoldenWalnutActionBindingStatus(definition.AcquisitionOptionId),
            AccessPhase = definition.AccessPhase,
            RequiredMailKey = definition.RequiredMailKey,
            RequiredMailSatisfied = requiredMailSatisfied,
            ResortOnly = definition.ResortOnly,
            PreResortEligible = preResortEligible,
            OptionalUnlockWalnutCost = definition.OptionalUnlockWalnutCost,
            RemainingOptionalUnlockWalnutCost = remainingUnlockCost,
            SpendSafeBeforeResort = preResortEligible && remainingUnlockCost == 0,
            ProbabilityStatus = definition.ProbabilityStatus,
            RuntimeSourceValidationStatus = GoldenWalnutRuntimeSourceValidationStatus(definition, actor),
            RequiredSecretNoteId = definition.RequiredSecretNoteId,
            RequiredSecretNoteSatisfied = !definition.RequiredSecretNoteId.HasValue ||
                actor.secretNotesSeen.Contains(definition.RequiredSecretNoteId.Value)
        };
    }

    private static string GoldenWalnutRuntimeSourceValidationStatus(
        GoldenWalnutNativeSourceDefinition definition,
        Farmer actor)
    {
        if (!string.Equals(
                definition.AcquisitionOptionId,
                "executor.till_soil",
                StringComparison.Ordinal))
        {
            return "not_required_for_action_family";
        }

        if (!definition.TileX.HasValue || !definition.TileY.HasValue)
            return "blocked_catalog_coordinate_missing";

        var location = Game1.getLocationFromName(definition.LocationId) as IslandLocation;
        if (location is null)
            return "blocked_runtime_island_location_unavailable";

        if (definition.RequiredSecretNoteId.HasValue)
        {
            if (!actor.secretNotesSeen.Contains(definition.RequiredSecretNoteId.Value))
                return "blocked_required_secret_note_unseen";
            return location.doesTileHaveProperty(
                    definition.TileX.Value,
                    definition.TileY.Value,
                    "Diggable",
                    "Back") is not null
                ? "exact_current_world_journal_buried_tile_present_and_note_seen"
                : "blocked_runtime_journal_buried_tile_not_diggable";
        }

        return location.IsBuriedNutLocation(new Point(
            definition.TileX.Value,
            definition.TileY.Value))
                ? "exact_current_world_buried_nut_point_present"
                : "blocked_runtime_buried_nut_point_absent";
    }

    private static string GoldenWalnutActionBindingStatus(string optionId) => optionId switch
    {
        "foraging.harvest_bushes" or
        "fishing.catch_fish" or
        "processing.crack_geode" or
        "island.field_office_donate" or
        "island.field_office_survey" or
        "minigame.play_darts" or
        "farm.maintain_crops" or
        "executor.clear_obstacle" or
        "executor.combat_monster" or
        "executor.break_volcano_container" or
        "executor.break_volcano_stone" or
        "executor.combat_volcano_monster" or
        "executor.till_soil" => "existing_action_family_bound",
        _ => "catalogued_action_binding_pending"
    };
}
