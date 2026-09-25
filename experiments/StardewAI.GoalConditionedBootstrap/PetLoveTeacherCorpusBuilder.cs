using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class PetLoveTeacherCorpusBuilder
{
    private const int MaximumFriendship = 1000;
    private const int NativeDailyInteractionGain = 12;
    private const int StageOneDeadlineExclusive = 224;

    public static PetLoveTeacherCorpus Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(requestPath, "Pet-love corpus request");
        var request = CurrentTeacherFrontierSupport.Read<
            PetLoveTeacherCorpusRequest>(fullRequestPath, "Pet-love corpus request");
        ValidateRequest(request);
        var rows = request.Sources
            .Select(BuildRow)
            .OrderBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();
        ValidateRows(rows);
        var partitions = rows.Select(row => row.DatasetPartition)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new PetLoveTeacherCorpus
        {
            Status = partitions.Length == 3
                ? "ready_split_complete_pet_love_teacher_corpus"
                : "verified_partial_pet_love_teacher_corpus",
            CorpusId = request.CorpusId,
            RowCount = rows.Length,
            PartitionCount = partitions.Length,
            SplitComplete = RequiredPartitions.All(partitions.Contains),
            Rows = rows,
            FormalProductTrainingAuthorized = false
        };
    }

    public static PetLoveTeacherCorpus Verify(string corpusPath)
    {
        var fullPath = RequiredFile(corpusPath, "Pet-love Teacher corpus");
        var corpus = CurrentTeacherFrontierSupport.Read<PetLoveTeacherCorpus>(
            fullPath,
            "Pet-love Teacher corpus");
        if (corpus.SchemaVersion != PetLoveTeacherCorpusVersions.Corpus ||
            string.IsNullOrWhiteSpace(corpus.CorpusId) ||
            corpus.GoalId != GoalMethodTeacherCoverageGoalIds.Authoritative ||
            corpus.DirectionId != "earn_pet_love" ||
            corpus.MethodId != "grandpa.direct.earn_pet_love" ||
            corpus.FormalProductTrainingAuthorized ||
            corpus.Rows.Length == 0 ||
            corpus.RowCount != corpus.Rows.Length)
        {
            throw new InvalidDataException("Pet-love Teacher corpus header is invalid.");
        }

        var rebuilt = corpus.Rows.Select(row => BuildRow(new PetLoveTeacherCorpusSource(
                row.SourceId,
                row.BeforeSnapshotPath,
                row.AfterSnapshotPath,
                row.ExecutionResultPath)))
            .OrderBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();
        ValidateRows(rebuilt);
        if (!EqualJson(corpus.Rows, rebuilt))
            throw new InvalidDataException("Pet-love Teacher corpus rows drifted from source evidence.");
        var partitions = rebuilt.Select(row => row.DatasetPartition)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var splitComplete = RequiredPartitions.All(partitions.Contains);
        var expectedStatus = splitComplete
            ? "ready_split_complete_pet_love_teacher_corpus"
            : "verified_partial_pet_love_teacher_corpus";
        if (corpus.PartitionCount != partitions.Length ||
            corpus.SplitComplete != splitComplete ||
            corpus.Status != expectedStatus)
        {
            throw new InvalidDataException("Pet-love Teacher corpus partition summary drifted.");
        }
        return corpus;
    }

    private static PetLoveTeacherEvidenceRow BuildRow(
        PetLoveTeacherCorpusSource source)
    {
        if (string.IsNullOrWhiteSpace(source.SourceId))
            throw new InvalidDataException("Pet-love corpus source ID is required.");
        var beforePath = RequiredFile(source.BeforeSnapshotPath, "Pet-love before snapshot");
        var afterPath = RequiredFile(source.AfterSnapshotPath, "Pet-love after snapshot");
        var resultPath = RequiredFile(source.ExecutionResultPath, "Pet-love execution result");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforePath,
            "Pet-love before snapshot");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "Pet-love after snapshot");
        var result = CurrentTeacherFrontierSupport.Read<TrainingExecutionResult>(
            resultPath,
            "Pet-love execution result");
        ValidateSnapshot(before, "before");
        ValidateSnapshot(after, "after");
        if (before.GameVersion != after.GameVersion ||
            before.BridgeVersion != after.BridgeVersion)
        {
            throw new InvalidDataException("Pet-love snapshot versions changed during one action.");
        }

        var saveId = RequiredIdentity(before.SaveId, "save_id");
        var playerId = RequiredIdentity(before.PlayerId, "player_id");
        if (saveId != RequiredIdentity(after.SaveId, "after save_id") ||
            playerId != RequiredIdentity(after.PlayerId, "after player_id"))
        {
            throw new InvalidDataException("Pet-love snapshot identity changed during one action.");
        }
        var totalDay = ReadEnvelopeInt(before, "time", "total_days");
        if (totalDay < 0 || totalDay >= StageOneDeadlineExclusive ||
            ReadEnvelopeInt(after, "time", "total_days") != totalDay)
        {
            throw new InvalidDataException("Pet-love terminal interaction is outside one Stage-1 day.");
        }

        var beforePets = ReadEnvelopeArray(before, "farm", "pets");
        var afterPets = ReadEnvelopeArray(after, "farm", "pets");
        var petId = result.PetId;
        if (!Guid.TryParse(petId, out _))
            throw new InvalidDataException("Pet-love result has no native pet identity.");
        var beforePet = SinglePet(beforePets, petId, "before");
        var afterPet = SinglePet(afterPets, petId, "after");
        var friendshipBefore = RequiredInt(beforePet, "friendship_toward_farmer");
        var friendshipAfter = RequiredInt(afterPet, "friendship_toward_farmer");
        var projectedAfter = RequiredInt(beforePet, "friendship_after_daily_interaction");
        var projectedDelta = RequiredInt(beforePet, "daily_interaction_friendship_delta");
        var timesBefore = RequiredInt(beforePet, "times_pet_before");
        var timesAfter = RequiredInt(afterPet, "times_pet_before");
        var lastPetDayBefore = OptionalInt(
            beforePet,
            "last_pet_day_for_player");
        var beforeMail = ReadMail(before).Contains("petLoveMessage", StringComparer.Ordinal);
        var afterMail = ReadMail(after).Contains("petLoveMessage", StringComparer.Ordinal);
        var lastPetDayAfter = OptionalInt(afterPet, "last_pet_day_for_player");
        var nativeProjectionReady = RequiredBool(beforePet, "native_check_action_supported") &&
            RequiredString(beforePet, "runtime_type") == "StardewValley.Characters.Pet" &&
            RequiredString(beforePet, "native_check_action_declaring_type") ==
                "StardewValley.Characters.Pet" &&
            RequiredString(beforePet, "action_status") == "ready" &&
            !RequiredBool(beforePet, "granted_friendship_for_pet") &&
            RequiredBool(beforePet, "granted_friendship_after_daily_interaction");
        var exactTerminalTransition = friendshipBefore >=
                MaximumFriendship - NativeDailyInteractionGain &&
            friendshipBefore < MaximumFriendship &&
            friendshipAfter == MaximumFriendship &&
            projectedAfter == MaximumFriendship &&
            projectedDelta == MaximumFriendship - friendshipBefore &&
            timesAfter == timesBefore + 1 &&
            lastPetDayBefore != totalDay &&
            lastPetDayAfter == totalDay &&
            RequiredBool(afterPet, "granted_friendship_for_pet") &&
            !beforeMail && afterMail;
        var resultMatches = result.SchemaVersion == "training_execution_result.v1" &&
            result.BeforeStateHash == before.StateHash &&
            result.OptionId == "executor.pet_interact" &&
            result.Status == "applied" &&
            result.FeedbackAvailable &&
            result.ActualTicks is > 0 &&
            result.TrainingImpactScope == "executor_calibration" &&
            result.PrimitiveKind == "pet_interact" &&
            result.PrimitiveVerificationStatus == "verified" &&
            result.PrimitiveVerificationReasons.Contains(
                "native_Pet.checkAction_completed",
                StringComparer.Ordinal) &&
            result.PrimitiveVerificationReasons.Contains(
                "friendship_lastPetDay_timesPet_love_mail_and_adoption_mail_verified",
                StringComparer.Ordinal) &&
            result.BlockReasons.Length == 0 &&
            result.PetFriendshipBefore == friendshipBefore &&
            result.PetFriendshipAfter == friendshipAfter &&
            result.PetLastPetDayBefore == lastPetDayBefore &&
            result.PetLastPetDayBeforeMissing ==
                (lastPetDayBefore is null) &&
            result.PetTimesPetBefore == timesBefore &&
            result.PetTimesPetAfter == timesAfter &&
            result.PetGrantedFriendshipBefore == false &&
            result.PetGrantedFriendshipAfter == true &&
            result.PetLastPetDayAfter == totalDay &&
            result.PetLoveMailBefore == false &&
            result.PetLoveMailAfter == true;
        if (!nativeProjectionReady || !exactTerminalTransition || !resultMatches ||
            before.StateHash == after.StateHash)
        {
            throw new InvalidDataException(
                "Pet-love source is not one exact verified native terminal interaction.");
        }

        var splitKey = saveId + ":" + playerId + ":" + totalDay;
        var partition = PolicyTrajectoryDatasetBuilder.PartitionFor(splitKey);
        var rowId = StableId(new
        {
            source_id = source.SourceId,
            before_state_hash = before.StateHash,
            after_state_hash = after.StateHash,
            pet_id = petId
        });
        return new PetLoveTeacherEvidenceRow(
            rowId,
            source.SourceId,
            beforePath,
            CurrentTeacherFrontierSupport.HashFile(beforePath),
            before.StateHash,
            afterPath,
            CurrentTeacherFrontierSupport.HashFile(afterPath),
            after.StateHash,
            resultPath,
            CurrentTeacherFrontierSupport.HashFile(resultPath),
            splitKey,
            partition,
            saveId,
            playerId,
            totalDay,
            petId,
            friendshipBefore,
            friendshipAfter,
            "execute_pet_daily_interaction_now",
            "defer_pet_daily_interaction_one_day",
            true,
            true);
    }

    private static void ValidateRequest(PetLoveTeacherCorpusRequest request)
    {
        if (request.SchemaVersion != PetLoveTeacherCorpusVersions.Request ||
            string.IsNullOrWhiteSpace(request.CorpusId) ||
            request.FormalProductTrainingAuthorized ||
            request.Sources.Length == 0 ||
            request.Sources.Select(source => source.SourceId)
                .Distinct(StringComparer.Ordinal).Count() != request.Sources.Length)
        {
            throw new InvalidDataException("Pet-love Teacher corpus request is invalid.");
        }
    }

    private static void ValidateRows(PetLoveTeacherEvidenceRow[] rows)
    {
        if (rows.Length == 0 ||
            rows.Select(row => row.RowId).Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.Select(row => row.SplitKey).Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.Any(row => !row.TeacherComparisonVerified ||
                !row.NativeTerminalOutcomeVerified ||
                row.DatasetPartition != PolicyTrajectoryDatasetBuilder.PartitionFor(row.SplitKey)))
        {
            throw new InvalidDataException("Pet-love Teacher corpus rows are invalid or duplicate.");
        }
    }

    private static void ValidateSnapshot(SnapshotEnvelope snapshot, string label)
    {
        if (snapshot.SchemaVersion != "snapshot.v1" ||
            snapshot.GameVersion != "1.6.15" ||
            string.IsNullOrWhiteSpace(snapshot.BridgeVersion) ||
            string.IsNullOrWhiteSpace(snapshot.StateHash) ||
            snapshot.StateHash != SnapshotHash.ComputeStateHash(snapshot.State))
        {
            throw new InvalidDataException("Pet-love " + label + " snapshot is invalid.");
        }
    }

    private static string RequiredIdentity(FieldEnvelope<string?> field, string label)
    {
        if (!FieldEnvelopeValidator.IsReadableStatus(field.Status) ||
            string.IsNullOrWhiteSpace(field.Value))
        {
            throw new InvalidDataException("Pet-love " + label + " is unavailable.");
        }
        return field.Value;
    }

    private static JsonElement ReadEnvelopeValue(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            !envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not (FieldStatus.Available or FieldStatus.Derived) ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                $"Pet-love snapshot field {section}.{field} is unavailable.");
        }
        return value;
    }

    private static int ReadEnvelopeInt(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadEnvelopeValue(snapshot, section, field).GetInt32();

    private static JsonElement.ArrayEnumerator ReadEnvelopeArray(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadEnvelopeValue(snapshot, section, field).EnumerateArray();

    private static string[] ReadMail(SnapshotEnvelope snapshot) =>
        ReadEnvelopeValue(snapshot, "quests", "mail_received")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();

    private static JsonElement SinglePet(
        JsonElement.ArrayEnumerator pets,
        string petId,
        string label)
    {
        var matches = pets.Where(pet => RequiredString(pet, "pet_id") == petId)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Pet-love {label} snapshot does not contain one bound pet.");
    }

    private static string RequiredString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(field.GetString())
            ? field.GetString()!
            : throw new InvalidDataException("Pet-love property is missing: " + property);

    private static int RequiredInt(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException("Pet-love integer is missing: " + property);

    private static int? OptionalInt(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind != JsonValueKind.Null &&
        field.TryGetInt32(out var result)
            ? result
            : null;

    private static bool RequiredBool(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? field.GetBoolean()
            : throw new InvalidDataException("Pet-love boolean is missing: " + property);

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            throw new FileNotFoundException(label + " does not exist.", fullPath);
        return fullPath;
    }

    private static string StableId(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonDefaults.Compact);
        return "pet-love-" + Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant()[..24];
    }

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Compact),
        JsonSerializer.Serialize(right, JsonDefaults.Compact),
        StringComparison.Ordinal);

    private static readonly string[] RequiredPartitions =
    {
        PolicyDatasetPartitions.Train,
        PolicyDatasetPartitions.Validation,
        PolicyDatasetPartitions.Test
    };
}
