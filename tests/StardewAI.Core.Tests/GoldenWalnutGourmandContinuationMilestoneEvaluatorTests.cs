using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutGourmandContinuationMilestoneEvaluatorTests
{
    [Fact]
    public void ExactCompletedNativeProjectionAndThreeCollectedRewardsAreReady()
    {
        var snapshot = Snapshot();
        var before = JsonSerializer.Serialize(snapshot);
        var result = GoldenWalnutGourmandContinuationMilestoneEvaluator.Evaluate(snapshot);
        Assert.True(result.Ready);
        Assert.Equal(3, result.RequestsFulfilled);
        Assert.Equal(15, result.CollectedGourmandWalnutQuantity);
        Assert.Empty(result.BlockingReasons);
        Assert.Equal(before, JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void NullInputIsProgrammerMisuse()
    {
        Assert.Throws<ArgumentNullException>(() => GoldenWalnutGourmandContinuationMilestoneEvaluator.Evaluate(null!));
    }

    [Theory]
    [InlineData("save", "unavailable", "save")]
    [InlineData("save", "stale", "save")]
    [InlineData("save", "available", " ")]
    [InlineData("player", "derived", "player")]
    [InlineData("player", "available", "")]
    [InlineData("player", "available", null)]
    public void IdentityMustBeAvailableAndNonempty(string identity, string status, string? value)
    {
        var result = Evaluate(root =>
        {
            root[identity + "_id"]!["status"] = status;
            root[identity + "_id"]!["value"] = value;
        });
        Blocked(result, identity + "_identity_unavailable");
    }

    [Fact]
    public void WrongGameVersionIsBlocked()
    {
        Blocked(Evaluate(root => root["game_version"] = "1.6.16"), "game_version_not_locked_1_6_15");
    }

    [Theory]
    [InlineData("island_gourmand", "gourmand_progress", "missing")]
    [InlineData("island_gourmand", "gourmand_progress", "stale")]
    [InlineData("island_gourmand", "gourmand_progress", "unavailable")]
    [InlineData("island_gourmand", "gourmand_progress", "derived")]
    [InlineData("golden_walnut_sources", "golden_walnut_source_catalog", "missing")]
    [InlineData("golden_walnut_sources", "golden_walnut_source_catalog", "unavailable")]
    [InlineData("golden_walnut_sources", "golden_walnut_source_catalog", "stale")]
    public void MissingOrUnreadableFieldStatusCannotSupplyMilestone(string field, string prefix, string change)
    {
        var result = Evaluate(root =>
        {
            var world = root["state"]!["world_progress"]!.AsObject();
            if (change == "missing") world.Remove(field);
            else world[field]!["status"] = change;
        });
        Blocked(result, prefix + "_unavailable");
    }

    [Theory]
    [InlineData("schema_version", "\"other\"", "gourmand_progress_identity_invalid")]
    [InlineData("projection_status", "\"exact_native_active_request_projected\"", "gourmand_progress_identity_invalid")]
    [InlineData("required_mail_satisfied", "false", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("requests_fulfilled", "2", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("requests_fulfilled", "4", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("total_requests", "4", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("complete", "false", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("active_request_index", "2", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("request_state", "\"ready_matching_crop_present\"", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("required_crop_item_id", "\"248\"", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("required_crop_qualified_item_id", "\"(O)248\"", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("required_crop_display_name", "\"Garlic\"", "gourmand_requests_incomplete_or_contradictory")]
    public void RequestTerminalContradictionsAreBlocked(string field, string value, string blocker)
    {
        Blocked(Evaluate(root => Progress(root)[field] = JsonNode.Parse(value)), blocker);
    }

    [Theory]
    [InlineData("schema_version", "gourmand_progress_identity_invalid")]
    [InlineData("active_request_index", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("required_crop_item_id", "gourmand_requests_incomplete_or_contradictory")]
    [InlineData("required_crop_display_name", "gourmand_requests_incomplete_or_contradictory")]
    public void MissingPropertiesCannotBecomeConstructorDefaults(string field, string blocker)
    {
        Blocked(Evaluate(root => Progress(root).Remove(field)), blocker);
    }

    [Theory]
    [InlineData("schema_version", "\"wrong\"")]
    [InlineData("projection_status", "\"partial\"")]
    [InlineData("native_source_key_count", "68")]
    [InlineData("native_walnut_total", "129")]
    public void SourceCatalogIdentityMustRemainLocked(string field, string value)
    {
        var result = Evaluate(root => Catalog(root)[field] = JsonNode.Parse(value));
        Blocked(result, "golden_walnut_source_catalog_identity_invalid");
        Assert.Equal(0, result.CollectedGourmandWalnutQuantity);
    }

    [Theory]
    [InlineData("gourmand_only")]
    [InlineData("missing_non_gourmand")]
    [InlineData("duplicate_non_gourmand")]
    [InlineData("unknown_source")]
    public void HeaderCannotClaimNativeDenominatorWithoutExactFullSourceKeySet(string change)
    {
        var result = Evaluate(root =>
        {
            var rows = Catalog(root)["sources"]!.AsArray();
            var unrelated = rows.OfType<JsonObject>().First(row =>
                !row["source_key"]!.GetValue<string>().StartsWith("IslandGourmand", StringComparison.Ordinal));
            if (change == "gourmand_only")
                Catalog(root)["sources"] = new JsonArray(rows.OfType<JsonObject>().Where(row =>
                    row["source_key"]!.GetValue<string>().StartsWith("IslandGourmand", StringComparison.Ordinal))
                    .Select(row => row.DeepClone()).ToArray());
            else if (change == "missing_non_gourmand") rows.Remove(unrelated);
            else if (change == "unknown_source") unrelated["source_key"] = "UnknownNativeSource";
            else
            {
                var duplicate = unrelated.DeepClone();
                var other = rows.OfType<JsonObject>().First(row => !ReferenceEquals(row, unrelated) &&
                    !row["source_key"]!.GetValue<string>().StartsWith("IslandGourmand", StringComparison.Ordinal));
                rows[rows.IndexOf(other)] = duplicate;
            }
        });
        Blocked(result, "golden_walnut_source_catalog_rows_invalid");
        Assert.Equal(15, result.CollectedGourmandWalnutQuantity);
    }

    [Theory]
    [InlineData("native_quantity", null)]
    [InlineData("collected_quantity", null)]
    [InlineData("remaining_quantity", null)]
    [InlineData("native_quantity", "2")]
    [InlineData("collected_quantity", "-1")]
    [InlineData("collected_quantity", "2")]
    [InlineData("remaining_quantity", "0")]
    [InlineData("remaining_quantity", "-1")]
    public void NonGourmandRowsAlsoRequireExplicitValidQuantities(string field, string? value)
    {
        var result = Evaluate(root =>
        {
            var row = Catalog(root)["sources"]!.AsArray().OfType<JsonObject>().First(source =>
                !source["source_key"]!.GetValue<string>().StartsWith("IslandGourmand", StringComparison.Ordinal) &&
                source["native_quantity"]!.GetValue<int>() == 1);
            if (value is null) row.Remove(field);
            else row[field] = JsonNode.Parse(value);
        });
        Blocked(result, "golden_walnut_source_catalog_rows_invalid");
    }

    [Theory]
    [InlineData("tracked_collected_quantity", "14")]
    [InlineData("tracked_remaining_quantity", "114")]
    [InlineData("tracked_collected_quantity", null)]
    [InlineData("tracked_remaining_quantity", null)]
    public void AggregateTotalsMustBePresentAndEqualToValidatedRows(string field, string? value)
    {
        var result = Evaluate(root =>
        {
            if (value is null) Catalog(root).Remove(field);
            else Catalog(root)[field] = JsonNode.Parse(value);
        });
        Blocked(result, "golden_walnut_source_catalog_totals_invalid");
    }

    [Fact]
    public void ValidAdditionalCollectedSourcesAreIncludedInAggregateValidation()
    {
        var result = Evaluate(root =>
        {
            var row = Catalog(root)["sources"]!.AsArray().OfType<JsonObject>().First(source =>
                !source["source_key"]!.GetValue<string>().StartsWith("IslandGourmand", StringComparison.Ordinal));
            var quantity = row["native_quantity"]!.GetValue<int>();
            row["collected_quantity"] = quantity;
            row["remaining_quantity"] = 0;
            Catalog(root)["tracked_collected_quantity"] = 15 + quantity;
            Catalog(root)["tracked_remaining_quantity"] = 115 - quantity;
        });
        Assert.True(result.Ready);
        Assert.Equal(15, result.CollectedGourmandWalnutQuantity);
    }

    [Theory]
    [InlineData("IslandGourmand1", false)]
    [InlineData("IslandGourmand2", false)]
    [InlineData("IslandGourmand3", false)]
    [InlineData("IslandGourmand1", true)]
    [InlineData("IslandGourmand2", true)]
    [InlineData("IslandGourmand3", true)]
    public void EveryRewardKeyMustAppearExactlyOnce(string key, bool duplicate)
    {
        var result = Evaluate(root =>
        {
            var rows = Catalog(root)["sources"]!.AsArray();
            var row = Source(root, key);
            if (duplicate) rows.Add(row.DeepClone());
            else rows.Remove(row);
        });
        Blocked(result, "gourmand_source_missing_or_duplicate:" + key);
        Assert.Equal(10, result.CollectedGourmandWalnutQuantity);
    }

    [Theory]
    [InlineData("tracking_kind", "\"limited_drop\"")]
    [InlineData("location_id", "\"IslandWest\"")]
    [InlineData("tile_x", "4")]
    [InlineData("tile_y", "4")]
    [InlineData("native_quantity", "6")]
    [InlineData("acquisition_option_id", "\"executor.wait_ticks\"")]
    [InlineData("probability_status", "\"bounded_native_rng\"")]
    [InlineData("access_phase", "\"resort_only\"")]
    [InlineData("required_mail_key", "\"Island_Resort\"")]
    [InlineData("required_mail_satisfied", "false")]
    [InlineData("resort_only", "true")]
    [InlineData("optional_unlock_walnut_cost", "20")]
    [InlineData("required_secret_note_id", "1010")]
    public void SourceIdentityCannotBeSubstituted(string field, string value)
    {
        var result = Evaluate(root => Source(root)[field] = JsonNode.Parse(value));
        Blocked(result, "gourmand_source_identity_invalid:IslandGourmand2");
        Assert.Equal(10, result.CollectedGourmandWalnutQuantity);
    }

    [Theory]
    [InlineData("collected_quantity", "4")]
    [InlineData("collected_quantity", "6")]
    [InlineData("remaining_quantity", "1")]
    [InlineData("remaining_quantity", "-1")]
    [InlineData("remaining_quantity", null)]
    public void PartialOrContradictoryQuantitiesCannotCountAsFive(string field, string? value)
    {
        var result = Evaluate(root =>
        {
            if (value is null) Source(root).Remove(field);
            else Source(root)[field] = JsonNode.Parse(value);
        });
        Blocked(result, "gourmand_source_quantity_incomplete:IslandGourmand2");
        Assert.Equal(10, result.CollectedGourmandWalnutQuantity);
    }

    [Fact]
    public void MissingNullValuedSourceIdentityCannotBecomeConstructorDefault()
    {
        Blocked(Evaluate(root => Source(root).Remove("tile_x")), "gourmand_source_identity_invalid:IslandGourmand2");
    }

    [Theory]
    [InlineData("save_id", "save_identity_unavailable")]
    [InlineData("player_id", "player_identity_unavailable")]
    [InlineData("state", "gourmand_progress_unavailable")]
    public void NullSnapshotMembersAreOrdinaryIncompleteEvidence(string field, string blocker)
    {
        Blocked(Evaluate(root => root[field] = null), blocker);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[]")]
    public void NullOrMissingSourceRowsFailClosedWithoutThrowing(string rows)
    {
        Blocked(Evaluate(root => Catalog(root)["sources"] = JsonNode.Parse(rows)),
            "gourmand_source_missing_or_duplicate:IslandGourmand1");
    }

    [Theory]
    [InlineData("island_gourmand", "null", "gourmand_progress_unreadable")]
    [InlineData("island_gourmand", "[]", "gourmand_progress_unreadable")]
    [InlineData("island_gourmand", "{\"requests_fulfilled\":\"three\"}", "gourmand_progress_unreadable")]
    [InlineData("golden_walnut_sources", "42", "golden_walnut_source_catalog_unreadable")]
    [InlineData("golden_walnut_sources", "{\"sources\":false}", "golden_walnut_source_catalog_unreadable")]
    public void MalformedProjectionIsBlockedRatherThanThrown(string field, string value, string blocker)
    {
        Blocked(Evaluate(root => root["state"]!["world_progress"]![field]!["value"] = JsonNode.Parse(value)), blocker);
    }

    [Fact]
    public void EmptySnapshotReturnsStableBlockers()
    {
        var result = GoldenWalnutGourmandContinuationMilestoneEvaluator.Evaluate(new SnapshotEnvelope());
        Assert.False(result.Ready);
        Assert.Equal(-1, result.RequestsFulfilled);
        Assert.Equal(0, result.CollectedGourmandWalnutQuantity);
        Assert.Equal(new[]
        {
            "game_version_not_locked_1_6_15", "save_identity_unavailable", "player_identity_unavailable",
            "gourmand_progress_unavailable", "golden_walnut_source_catalog_unavailable", "gourmand_walnut_quantity_incomplete"
        }, result.BlockingReasons);
    }

    private static void Blocked(GoldenWalnutGourmandContinuationMilestoneAssessment result, string reason)
    {
        Assert.False(result.Ready);
        Assert.Contains(reason, result.BlockingReasons);
    }

    private static GoldenWalnutGourmandContinuationMilestoneAssessment Evaluate(Action<JsonObject> change)
    {
        var root = JsonSerializer.SerializeToNode(Snapshot())!.AsObject();
        change(root);
        return GoldenWalnutGourmandContinuationMilestoneEvaluator.Evaluate(root.Deserialize<SnapshotEnvelope>()!);
    }

    private static JsonObject Progress(JsonObject root) => root["state"]!["world_progress"]!["island_gourmand"]!["value"]!.AsObject();
    private static JsonObject Catalog(JsonObject root) => root["state"]!["world_progress"]!["golden_walnut_sources"]!["value"]!.AsObject();
    private static JsonObject Source(JsonObject root, string key = "IslandGourmand2") => Catalog(root)["sources"]!.AsArray()
        .OfType<JsonObject>().Single(row => row["source_key"]!.GetValue<string>() == key);

    private static SnapshotEnvelope Snapshot()
    {
        var sources = GoldenWalnutNativeSourceCatalog.Sources.Select(definition => new GoldenWalnutSourceProgressRef
        {
            SourceKey = definition.SourceKey, TrackingKind = definition.TrackingKind, LocationId = definition.LocationId,
            TileX = definition.TileX, TileY = definition.TileY, NativeQuantity = definition.NativeQuantity,
            CollectedQuantity = definition.SourceKey.StartsWith("IslandGourmand", StringComparison.Ordinal) ? 5 : 0,
            RemainingQuantity = definition.SourceKey.StartsWith("IslandGourmand", StringComparison.Ordinal) ? 0 : definition.NativeQuantity,
            AcquisitionOptionId = definition.AcquisitionOptionId, ProbabilityStatus = definition.ProbabilityStatus,
            AccessPhase = definition.AccessPhase, RequiredMailKey = definition.RequiredMailKey,
            RequiredMailSatisfied = true, ResortOnly = definition.ResortOnly,
            OptionalUnlockWalnutCost = definition.OptionalUnlockWalnutCost, RequiredSecretNoteId = definition.RequiredSecretNoteId
        }).ToArray();
        var progress = new IslandGourmandProgressRef
        {
            ProjectionStatus = "complete_native_three_requests", RequiredMailSatisfied = true,
            RequestsFulfilled = 3, TotalRequests = 3, Complete = true, ActiveRequestIndex = null, RequestState = "complete"
        };
        var catalog = new GoldenWalnutSourceCatalogRef
        {
            SchemaVersion = GoldenWalnutNativeSourceCatalog.SchemaVersion,
            ProjectionStatus = GoldenWalnutNativeSourceCatalog.ProjectionStatus,
            NativeSourceKeyCount = GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount,
            NativeWalnutTotal = GoldenWalnutNativeSourceCatalog.NativeWalnutTotal,
            Sources = sources, TrackedCollectedQuantity = 15, TrackedRemainingQuantity = 115
        };
        return new SnapshotEnvelope
        {
            GameVersion = "1.6.15",
            SaveId = new() { Status = FieldStatus.Available, Value = "save" },
            PlayerId = new() { Status = FieldStatus.Available, Value = "player" },
            State = new Dictionary<string, JsonElement>
            {
                ["world_progress"] = JsonSerializer.SerializeToElement(new
                {
                    island_gourmand = new { status = "available", value = progress },
                    golden_walnut_sources = new { status = "available", value = catalog }
                })
            }
        };
    }
}
