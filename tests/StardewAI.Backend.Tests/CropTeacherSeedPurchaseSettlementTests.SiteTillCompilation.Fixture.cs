using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Synthetic projected fields only. Retain evidence; dispose just the in-process host.
    private sealed class SiteTillFiles
    {
        internal OwnedRouteFixture F { get; }
        internal string Observation { get; }
        internal string Request { get; }
        internal string Plan { get; }
        internal string Narrow { get; }
        internal string Full { get; }
        internal string Queue { get; }
        internal MoneyRevenueCropSitePreparationPlan SitePlan { get; }

        internal SiteTillFiles(Action<JsonNode>? adjustContext = null, Action<Fixture>? prepareSource = null)
        {
            F = new OwnedRouteFixture(prepareSource: source =>
            {
                ChangeState(source.Before, "player", player =>
                {
                    player["tile_x"]!["value"] = 0; player["tile_y"]!["value"] = 1;
                    player["energy"] = JsonSerializer.SerializeToNode(new { status = "available", value = 270d });
                });
                source.Before.State["menus"] = JsonSerializer.SerializeToElement(new
                { active_menu = new { status = "available", value = new { is_open = false, type = "" } } });
                ChangeState(source.Before, "current_location", location =>
                {
                    location["map"]!["value"]!["id"] = "";
                    location["site_preparation_context"] = JsonSerializer.SerializeToNode(new
                    {
                        status = "available", read_at_tick = source.Before.GameTick, value = new
                        {
                            schema_version = "farm_site_preparation_context.v2", projection_status = "complete_bounded_current_farm_region",
                            location_id = "Farm", is_farm = true, map_width = 20, map_height = 20,
                            bounds = new { min_x = 0, min_y = 0, max_x = 3, max_y = 2 }, tile_count = 12,
                            hoe_tool_slot_index = 1, hoe_tool_qualified_item_id = "(T)Hoe",
                            hoe_energy_cost_per_single_tile_action = 2d, hoe_tool_status = "ready",
                            tiles = SiteObservationTiles().Select(tile => new
                            {
                                tile_x = tile.TileX, tile_y = tile.TileY, status = "ready_to_till", on_map = true,
                                diggable_back_property = true, map_passable = true, native_blocked_without_characters = false,
                                native_walking_collision_blocked = false, watering_refill_approach_standable = true,
                                has_hoe_dirt = false, hoe_dirt_has_crop = false, hoe_tool_slot_index = 1,
                                hoe_tool_qualified_item_id = "(T)Hoe", hoe_energy_cost = 2d,
                                blocker_kinds = Array.Empty<string>(), clearable_blocker_kinds = Array.Empty<string>()
                            }).ToArray()
                        }
                    });
                    adjustContext?.Invoke(location["site_preparation_context"]!["value"]!);
                });
                prepareSource?.Invoke(source);
            });
            var observed = ReadSiteSnapshot(WriteValidSiteMainObservation(F));
            observed.State["current_location"] = F.Source.Before.State["current_location"];
            observed.StateHash = SnapshotHash.ComputeStateHash(observed.State);
            Observation = Write("till-observation.json", observed);
            var layout = F.Inputs.Request.LayoutIntents.Single();
            Request = Write("till-request.json", new MoneyRevenueCropSitePreparationPlanRequest
            {
                CropProductionPlanPath = F.Inputs.Request.CropProductionPlanPath, CommitmentId = Id,
                PreparationSnapshotPath = Observation, LocationId = layout.LocationId, Tiles = layout.Tiles,
                WateringRefillPreparationTiles = layout.WateringRefillPreparationTiles ?? Array.Empty<MoneyRevenueCropPlantingTile>()
            });
            SitePlan = MoneyRevenueCropSitePreparationPlanBuilder.Build(Request);
            Plan = Write("till-plan.json", SitePlan);
            var step = MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(SitePlan)!;
            Assert.Equal("direct_till_ready", step.PlanStatus);
            var parameters = step.Parameters.Concat(new[] { new SmallModelActionParameter
                { Name = "precondition", Value = "candidate_id:" + step.CandidateId } }).ToArray();
            JsonObject Model(string hash) => CropMechanicalActionModelBuilder.Build(hash,
                "authoritative_crop_site_preparation_plan.v1", step.CandidateId, step.ActionOptionId, parameters,
                ExecutionTargetProfiles.TrainingSingleplayer,
                JsonSerializer.SerializeToNode(ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer), JsonOptions)!);
            Narrow = Write("till-narrow.json", Model(observed.StateHash));
            var full = Model(F.Source.Before.StateHash);
            Full = Write("till-full.json", full);
            Queue = Write("till-queue.json", new ActionQueueCompiler().Compile(full.Deserialize<SmallModelActionEnvelope>(JsonOptions)!,
                F.Source.Before, F.Source.Ledger));
        }

        private string Write<T>(string name, T value)
        {
            var path = Path.Combine(F.Source.RuntimePath, name); Fixture.Save(path, value); return path;
        }
        internal CropTeacherSiteTillCompilation Open(string? observation = null, string? original = null, string? commitment = null) =>
            CropTeacherSiteTillCompilation.Open(F.Inputs.Plan.RequestPath, F.Source.SourcePath, F.Source.LedgerPath,
                F.Inputs.Plan.CoveredCommitmentIds, commitment ?? Id, "site.till", observation ?? Observation,
                Request, Plan, original ?? Narrow, Full, Queue);
    }
}
