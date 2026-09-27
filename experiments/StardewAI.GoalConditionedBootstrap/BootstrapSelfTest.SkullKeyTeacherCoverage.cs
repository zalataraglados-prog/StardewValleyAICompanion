using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static string BuildSkullKeyTeacherCorpusFixture(string outputRoot)
    {
        var fixtureRoot = Path.Combine(outputRoot, "skull-key-terminal-fixture");
        Directory.CreateDirectory(fixtureRoot);
        var sources = new List<SkullKeyTeacherCorpusSource>();
        foreach (var partition in new[]
                 {
                     PolicyDatasetPartitions.Train,
                     PolicyDatasetPartitions.Validation,
                     PolicyDatasetPartitions.Test
                 })
        {
            var suffix = 0;
            string saveId;
            const string playerId = "skull-key-player";
            const int totalDay = 120;
            do
            {
                saveId = "skull-key-" + partition + "-" + suffix++;
            } while (PolicyTrajectoryDatasetBuilder.PartitionFor(
                         saveId + ":" + playerId + ":" + totalDay) != partition);

            var sourceId = "skull-key-" + partition;
            var sourceRoot = Path.Combine(fixtureRoot, sourceId);
            Directory.CreateDirectory(sourceRoot);
            var before = SkullKeySnapshot(
                saveId,
                playerId,
                totalDay,
                gameTick: 100,
                hasSkullKey: false,
                hasRewardChest: true);
            var after = SkullKeySnapshot(
                saveId,
                playerId,
                totalDay,
                gameTick: 220,
                hasSkullKey: true,
                hasRewardChest: false);
            var beforePath = Path.Combine(sourceRoot, "before-snapshot.json");
            var afterPath = Path.Combine(sourceRoot, "after-snapshot.json");
            var resultPath = Path.Combine(sourceRoot, "execution-result.json");
            var episodePath = Path.Combine(sourceRoot, "execution-episode.json");
            Write(beforePath, before);
            Write(afterPath, after);
            var changes = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.has_skull_key",
                    Before = "false",
                    After = "true"
                }
            };
            var reasons = new[]
            {
                "native_reward_chest_open_handled",
                "native_reward_item_claimed",
                "player_has_skull_key_transition_observed"
            };
            var result = new TrainingExecutionResult
            {
                RunId = sourceId,
                QueueId = sourceId + "-queue",
                QueueItemId = sourceId + "-claim",
                BeforeStateHash = before.StateHash,
                OptionId = "executor.interact",
                Status = "applied",
                FeedbackAvailable = true,
                ActualTicks = 120,
                PrimitiveKind = "claim_skull_key",
                PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons = reasons,
                RequestedEffect =
                    "interaction_kind=overlay_object;expected_action_type=SkullKeyChest;required_postcondition=player.has_skull_key=true",
                ObservedEffect =
                    "open_handled=true;claim_handled=true;player.has_skull_key=true",
                BlockReasons = Array.Empty<string>(),
                ChangedFacts = changes
            };
            Write(resultPath, result);
            Write(episodePath, new PlanExecutionEpisodeEnvelope
            {
                EpisodeId = sourceId + "-episode",
                RunId = sourceId,
                SourceStateHash = before.StateHash,
                AfterStateHash = after.StateHash,
                StateHashChanged = true,
                BeforeGameTick = before.GameTick,
                AfterGameTick = after.GameTick,
                AfterSnapshotFresh = true,
                AfterSnapshotNote = "fresh",
                ExecutionResultPath = resultPath,
                BeforeSnapshotPath = beforePath,
                AfterSnapshotPath = afterPath,
                QueueId = result.QueueId,
                OptionId = result.OptionId,
                Status = result.Status,
                Success = true,
                TrainingRole = TrainingRoles.ExecutorCalibration,
                BlockReasons = Array.Empty<string>(),
                EffectiveQueueItem = JsonSerializer.SerializeToElement(new
                {
                    queue_item_id = result.QueueItemId,
                    option_id = result.OptionId,
                    parameters = new[]
                    {
                        new { name = "target_tile_x", value = "11" },
                        new { name = "target_tile_y", value = "10" },
                        new { name = "interaction_kind", value = "overlay_object" },
                        new { name = "expected_action_type", value = "SkullKeyChest" },
                        new { name = "required_postcondition", value = "player.has_skull_key=true" }
                    }
                }),
                PrimitiveKind = result.PrimitiveKind,
                PrimitiveVerificationStatus =
                    result.PrimitiveVerificationStatus,
                PrimitiveVerificationReasons = reasons,
                RequestedEffect = result.RequestedEffect,
                ObservedEffect = result.ObservedEffect,
                ChangedFacts = JsonSerializer.SerializeToElement(changes)
            });
            sources.Add(new SkullKeyTeacherCorpusSource(
                sourceId,
                episodePath));
        }

        var requestPath = Path.Combine(
            fixtureRoot,
            "skull-key-teacher-corpus-request.json");
        Write(requestPath, new SkullKeyTeacherCorpusRequest
        {
            CorpusId = "self-test-skull-key-terminal-corpus",
            Sources = sources.ToArray(),
            FormalProductTrainingAuthorized = false
        });
        var corpusPath = Path.Combine(
            fixtureRoot,
            "skull-key-teacher-corpus.json");
        var corpus = SkullKeyTeacherCorpusBuilder.Build(requestPath);
        Require(corpus.SplitComplete && corpus.RowCount == 3,
            "Skull Key fixture did not cover all independent partitions.");
        Write(corpusPath, corpus);
        SkullKeyTeacherCorpusBuilder.Verify(corpusPath);
        RequireSkullKeyEpisodeTamperRejected(
            fixtureRoot,
            sources[0],
            "queue-item-id",
            episode => episode["effective_queue_item"]!["queue_item_id"] =
                "forged-queue-item");
        RequireSkullKeyEpisodeTamperRejected(
            fixtureRoot,
            sources[0],
            "requested-effect",
            episode => episode["requested_effect"] = "forged-effect");
        return corpusPath;
    }

    private static void RequireSkullKeyEpisodeTamperRejected(
        string fixtureRoot,
        SkullKeyTeacherCorpusSource source,
        string suffix,
        Action<JsonObject> tamper)
    {
        var episode = JsonNode.Parse(
                File.ReadAllText(source.ExecutionEpisodePath))!
            .AsObject();
        tamper(episode);
        var tamperedEpisodePath = Path.Combine(
            fixtureRoot,
            "tampered-" + suffix + "-execution-episode.json");
        File.WriteAllText(
            tamperedEpisodePath,
            episode.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }) + Environment.NewLine);
        var requestPath = Path.Combine(
            fixtureRoot,
            "tampered-" + suffix + "-request.json");
        Write(requestPath, new SkullKeyTeacherCorpusRequest
        {
            CorpusId = "self-test-skull-key-tampered-" + suffix,
            Sources = new[]
            {
                new SkullKeyTeacherCorpusSource(
                    "tampered-" + suffix,
                    tamperedEpisodePath)
            },
            FormalProductTrainingAuthorized = false
        });

        var rejected = false;
        try
        {
            SkullKeyTeacherCorpusBuilder.Build(requestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "Tampered Skull Key " + suffix + " episode was accepted.");
    }

    private static SnapshotEnvelope SkullKeySnapshot(
        string saveId,
        string playerId,
        int totalDay,
        long gameTick,
        bool hasSkullKey,
        bool hasRewardChest)
    {
        var chests = hasRewardChest
            ? new object[]
            {
                new
                {
                    tile_x = 11,
                    tile_y = 10,
                    runtime_type = "StardewValley.Objects.Chest",
                    item_count = 1,
                    contains_skull_key = true,
                    special_item_which = 4,
                    interaction_kind = "overlay_object",
                    expected_action_type = "SkullKeyChest",
                    source =
                        "MineShaft.overlayObjects Chest.Items SpecialItem.which"
                }
            }
            : Array.Empty<object>();
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(new
            {
                time = new
                {
                    total_days = Envelope(totalDay)
                },
                player = new
                {
                    has_skull_key = Envelope(hasSkullKey),
                    location_id = Envelope("UndergroundMine120")
                },
                mining = new
                {
                    current_mine = Envelope(new
                    {
                        location_id = "UndergroundMine120",
                        mine_level = 120,
                        mine_area = 0,
                        mine_kind = "ordinary_mines",
                        generated_identity = "UndergroundMine120:120",
                        is_loaded_current_location = true,
                        is_skull_cavern = false,
                        is_quarry_mine = false,
                        is_dangerous = false,
                        additional_difficulty = 0,
                        is_slime_area = false,
                        is_dino_area = false,
                        is_monster_area = false,
                        source =
                            "MineShaft.mineLevel/getMineArea/GetAdditionalDifficulty/is*Area"
                    }),
                    floor_objectives = Envelope(new
                    {
                        skull_key_applicable = true,
                        skull_key_acquired = hasSkullKey,
                        skull_key_reward_chests = chests,
                        source = "MineShaft flags and simple methods only"
                    })
                }
            }),
            JsonDefaults.Options)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = "0.1.0",
            GameVersion = "1.6.15",
            SaveId = Identity(saveId),
            PlayerId = Identity(playerId),
            GameTick = gameTick,
            RealTimestamp = "2026-09-28T00:00:00Z",
            Completeness = "complete",
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }
}
