using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static string BuildPetLoveTeacherCorpusFixture(string outputRoot)
    {
        var fixtureRoot = Path.Combine(outputRoot, "pet-love-terminal-fixture");
        Directory.CreateDirectory(fixtureRoot);
        var sources = new List<PetLoveTeacherCorpusSource>();
        foreach (var partition in new[]
                 {
                     PolicyDatasetPartitions.Train,
                     PolicyDatasetPartitions.Validation,
                     PolicyDatasetPartitions.Test
                 })
        {
            var suffix = 0;
            string saveId;
            const string playerId = "pet-love-player";
            const int totalDay = 100;
            do
            {
                saveId = "pet-love-" + partition + "-" + suffix++;
            } while (PolicyTrajectoryDatasetBuilder.PartitionFor(
                         saveId + ":" + playerId + ":" + totalDay) != partition);

            var sourceId = "pet-love-" + partition;
            var sourceRoot = Path.Combine(fixtureRoot, sourceId);
            Directory.CreateDirectory(sourceRoot);
            var petId = partition switch
            {
                PolicyDatasetPartitions.Train =>
                    "11111111-1111-1111-1111-111111111111",
                PolicyDatasetPartitions.Validation =>
                    "22222222-2222-2222-2222-222222222222",
                _ => "33333333-3333-3333-3333-333333333333"
            };
            var before = PetLoveSnapshot(
                saveId,
                playerId,
                totalDay,
                petId,
                988,
                4,
                null,
                false,
                false);
            var after = PetLoveSnapshot(
                saveId,
                playerId,
                totalDay,
                petId,
                1000,
                5,
                totalDay,
                true,
                true);
            var beforePath = Path.Combine(sourceRoot, "before-snapshot.json");
            var afterPath = Path.Combine(sourceRoot, "after-snapshot.json");
            var resultPath = Path.Combine(sourceRoot, "execution-result.json");
            Write(beforePath, before);
            Write(afterPath, after);
            Write(resultPath, new TrainingExecutionResult
            {
                RunId = sourceId,
                QueueId = sourceId + "-queue",
                QueueItemId = sourceId + "-pet",
                BeforeStateHash = before.StateHash,
                OptionId = "executor.pet_interact",
                Status = "applied",
                FeedbackAvailable = true,
                ActualTicks = 12,
                TrainingImpactScope = "executor_calibration",
                PrimitiveKind = "pet_interact",
                PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons = new[]
                {
                    "native_Pet.checkAction_completed",
                    "friendship_lastPetDay_timesPet_love_mail_and_adoption_mail_verified"
                },
                PetId = petId,
                PetFriendshipBefore = 988,
                PetFriendshipAfter = 1000,
                PetLastPetDayBeforeMissing = true,
                PetLastPetDayAfter = totalDay,
                PetTimesPetBefore = 4,
                PetTimesPetAfter = 5,
                PetGrantedFriendshipBefore = false,
                PetGrantedFriendshipAfter = true,
                PetLoveMailBefore = false,
                PetLoveMailAfter = true,
                BlockReasons = Array.Empty<string>()
            });
            sources.Add(new PetLoveTeacherCorpusSource(
                sourceId,
                beforePath,
                afterPath,
                resultPath));
        }

        var requestPath = Path.Combine(
            fixtureRoot,
            "pet-love-teacher-corpus-request.json");
        Write(requestPath, new PetLoveTeacherCorpusRequest
        {
            CorpusId = "self-test-pet-love-terminal-corpus",
            Sources = sources.ToArray(),
            FormalProductTrainingAuthorized = false
        });
        var corpusPath = Path.Combine(
            fixtureRoot,
            "pet-love-teacher-corpus.json");
        var corpus = PetLoveTeacherCorpusBuilder.Build(requestPath);
        Require(corpus.SplitComplete && corpus.RowCount == 3,
            "Pet-love fixture did not cover all independent partitions.");
        Write(corpusPath, corpus);
        PetLoveTeacherCorpusBuilder.Verify(corpusPath);
        return corpusPath;
    }

    private static SnapshotEnvelope PetLoveSnapshot(
        string saveId,
        string playerId,
        int totalDay,
        string petId,
        int friendship,
        int timesPet,
        int? lastPetDay,
        bool grantedFriendship,
        bool petLoveMail)
    {
        var willGrant = !grantedFriendship;
        var projectedFriendship = willGrant
            ? Math.Min(1000, friendship + 12)
            : friendship;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(new
            {
                time = new
                {
                    total_days = Envelope(totalDay)
                },
                farm = new
                {
                    pets = Envelope(new[]
                    {
                        new
                        {
                            pet_id = petId,
                            runtime_type = "StardewValley.Characters.Pet",
                            native_check_action_declaring_type =
                                "StardewValley.Characters.Pet",
                            native_check_action_supported = true,
                            friendship_toward_farmer = friendship,
                            friendship_after_daily_interaction =
                                projectedFriendship,
                            granted_friendship_for_pet = grantedFriendship,
                            granted_friendship_after_daily_interaction =
                                grantedFriendship || willGrant,
                            last_pet_day_for_player = lastPetDay,
                            current_total_days = totalDay,
                            times_pet_before = timesPet,
                            times_pet_after_daily_interaction = willGrant
                                ? timesPet + 1
                                : timesPet,
                            daily_interaction_friendship_delta = willGrant
                                ? projectedFriendship - friendship
                                : 0,
                            pet_love_mail_before = petLoveMail,
                            pet_love_mail_after_daily_interaction =
                                petLoveMail || willGrant &&
                                projectedFriendship >= 1000,
                            action_status = willGrant
                                ? "ready"
                                : "already_petted_today"
                        }
                    })
                },
                quests = new
                {
                    mail_received = Envelope(petLoveMail
                        ? new[] { "petLoveMessage" }
                        : Array.Empty<string>())
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
            GameTick = 1,
            RealTimestamp = "2026-09-25T00:00:00Z",
            Completeness = "complete",
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static object Envelope(object value) => new
    {
        value,
        status = FieldStatus.Available,
        source = new { kind = "self_test", path = "fixture" },
        adapter = "self_test",
        read_at_tick = 1,
        confidence = 1
    };

    private static FieldEnvelope<string?> Identity(string value) => new()
    {
        Value = value,
        Status = FieldStatus.Available,
        Source = new SourceRef { Kind = "self_test", Path = "fixture" },
        Adapter = "self_test",
        ReadAtTick = 1,
        Confidence = 1
    };
}
