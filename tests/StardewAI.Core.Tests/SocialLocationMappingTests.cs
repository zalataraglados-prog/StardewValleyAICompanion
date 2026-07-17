using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class SocialLocationMappingSourceGuardTests
{
    [Fact]
    public void BuildExecutionRequestCallsResolveLocationIdAndAssignsLocationId()
    {
        var source = LiveTrainingLoopSources.All;

        Assert.Contains("SocialLocationMapping.ResolveLocationId(item, optionId)", source);
        Assert.Contains("executionRequest.LocationId = socialTargetLocation", source);
        Assert.Contains("string.IsNullOrWhiteSpace(socialTargetLocation)", source);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file", Path.Combine(parts));
    }
}

public sealed class SocialLocationMappingTests
{
    [Fact]
    public void SocialInteractExecutorMapsTargetLocationToLocationId()
    {
        var item = QueueItemWithParams("executor.social_interact",
            Parameter("target_location", "SeedShop"),
            Parameter("npc_name", "Abigail"),
            Parameter("social_action_kind", "talk"));

        var locationId = SocialLocationMapping.ResolveLocationId(item, "executor.social_interact");

        Assert.Equal("SeedShop", locationId);
    }

    [Fact]
    public void SocialInteractExecutorReturnsEmptyWhenTargetLocationMissing()
    {
        var item = QueueItemWithParams("executor.social_interact",
            Parameter("npc_name", "Abigail"),
            Parameter("social_action_kind", "talk"));

        var locationId = SocialLocationMapping.ResolveLocationId(item, "executor.social_interact");

        Assert.Empty(locationId);
    }

    [Fact]
    public void FishingExecutorLocationIdFromLocationIdParamIsPreserved()
    {
        var item = QueueItemWithParams("executor.catch_fish",
            Parameter("location_id", "Forest"),
            Parameter("target_tile_x", "12"),
            Parameter("target_tile_y", "34"));

        var locationId = SocialLocationMapping.ResolveLocationId(item, "executor.catch_fish");

        Assert.Empty(locationId);
    }

    [Fact]
    public void FishingExecutorWithTargetLocationDoesNotMapToLocationId()
    {
        var item = QueueItemWithParams("executor.catch_fish",
            Parameter("location_id", "Forest"),
            Parameter("target_location", "SeedShop"),
            Parameter("target_tile_x", "12"),
            Parameter("target_tile_y", "34"));

        var locationId = SocialLocationMapping.ResolveLocationId(item, "executor.catch_fish");

        Assert.Empty(locationId);
    }

    [Fact]
    public void MoveToTileExecutorDoesNotMapTargetLocationToLocationId()
    {
        var item = QueueItemWithParams("executor.move_to_tile",
            Parameter("target_location", "SeedShop"),
            Parameter("target_tile_x", "41"),
            Parameter("target_tile_y", "23"));

        var locationId = SocialLocationMapping.ResolveLocationId(item, "executor.move_to_tile");

        Assert.Empty(locationId);
    }

    [Fact]
    public void InteractExecutorDoesNotMapTargetLocationToLocationId()
    {
        var item = QueueItemWithParams("executor.interact",
            Parameter("target_location", "SeedShop"),
            Parameter("interaction_kind", "map_action"));

        var locationId = SocialLocationMapping.ResolveLocationId(item, "executor.interact");

        Assert.Empty(locationId);
    }

    [Fact]
    public void NullItemReturnsEmpty()
    {
        var locationId = SocialLocationMapping.ResolveLocationId(null, "executor.social_interact");

        Assert.Empty(locationId);
    }

    private static JsonObject QueueItemWithParams(string optionId, params JsonObject[] parameters)
    {
        return new JsonObject
        {
            ["queue_item_id"] = "queue-item.test",
            ["option_id"] = optionId,
            ["status"] = "pending",
            ["normalized_command"] = new JsonObject
            {
                ["command_type"] = "compiled_action_steps",
                ["parameters"] = new JsonArray(parameters)
            }
        };
    }

    private static JsonObject Parameter(string name, string value)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["value"] = value
        };
    }
}
