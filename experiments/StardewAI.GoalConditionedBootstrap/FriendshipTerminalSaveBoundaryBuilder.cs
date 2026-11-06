using System.Globalization;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record FriendshipTerminalSaveBoundaryRequest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("source_save_file")] string SourceSaveFile,
    [property: JsonPropertyName("isolated_save_file")] string IsolatedSaveFile,
    [property: JsonPropertyName("isolation_root")] string IsolationRoot,
    [property: JsonPropertyName("basis_snapshot_path")] string BasisSnapshotPath,
    [property: JsonPropertyName("target_qualifying_count")] int TargetQualifyingCount,
    [property: JsonPropertyName("declared_local_prerequisite_only")] bool DeclaredLocalPrerequisiteOnly,
    [property: JsonPropertyName("formal_product_training_authorized")] bool FormalProductTrainingAuthorized,
    [property: JsonPropertyName("declared_local_story_prerequisites")] bool DeclaredLocalStoryPrerequisites = false)
{
    [JsonPropertyName("decision_winter_day_year_two")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DecisionWinterDayYearTwo { get; init; }
}

public static partial class FriendshipTerminalSaveBoundaryBuilder
{
    // Complete pinned JoshHouse event IDs, not story rewards or measured acquisitions.
    private static readonly string[] LocalStoryEventIds = { "18", "19", "21", "2119820", "56", "5837189" };

    public static object Prepare(string requestPath, string outputPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<FriendshipTerminalSaveBoundaryRequest>(requestPath, "friendship_terminal_save_boundary_request.v1");
        Require(request.DeclaredLocalPrerequisiteOnly && request.DeclaredLocalStoryPrerequisites && !request.FormalProductTrainingAuthorized &&
            request.TargetQualifyingCount is 5 or 10, "Friendship boundary requires declared local five/ten prerequisites.");
        Require(request.DecisionWinterDayYearTwo is null or >= 1 and <= 28,
            "Friendship boundary requires a valid declared year-two winter day.");
        var (source, target) = IsolatedSaveBoundaryFiles.ValidateScope(request.SourceSaveFile, request.IsolatedSaveFile, request.IsolationRoot);
        IsolatedSaveBoundaryFiles.ValidateOutput(outputPath, request.IsolationRoot);
        var sourceHash = CurrentTeacherFrontierSupport.HashFile(source);
        Require(sourceHash == CurrentTeacherFrontierSupport.HashFile(target), "Friendship boundary needs a byte-identical fresh clone.");
        var basisPath = RequiredFile(request.BasisSnapshotPath);
        var basis = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(basisPath, "Friendship boundary native basis");
        var deficit = new CurrentFriendshipDeficitBuilder().Build(basis);
        Require(deficit.Status == "ready_current_state_only", "Friendship boundary native population is incomplete.");
        var threshold = Value(basis, "npcs", "grandpa_friendship_progress").GetProperty("threshold_points").GetInt32();
        var document = IsolatedSaveBoundaryFiles.ReadDocument(target);
        XElement Required(XElement parent, string name) => parent.Elements(name).SingleOrDefault() ??
            throw new InvalidDataException("Friendship boundary save field missing: " + name);
        var root = document.Root;
        Require(root?.Name == "SaveGame" && Required(root!, "gameVersion").Value == basis.GameVersion,
            "Friendship boundary requires the same versioned native save.");
        var player = Required(root!, "player");
        Require(basis.SaveId.Value == Required(player, "farmName").Value && basis.PlayerId.Value == Required(player, "UniqueMultiplayerID").Value,
            "Friendship boundary native basis belongs to another farmer/save.");
        Require(Value(basis, "time", "year").GetInt32().ToString(CultureInfo.InvariantCulture) == Required(root!, "year").Value &&
            Value(basis, "time", "season").GetString() == Required(root!, "currentSeason").Value &&
            Value(basis, "time", "day").GetInt32().ToString(CultureInfo.InvariantCulture) == Required(root!, "dayOfMonth").Value,
            "Friendship boundary native basis date differs from the source save.");
        var entries = Required(player, "friendshipData").Elements("item").ToArray();
        var groups = entries.GroupBy(row => row.Element("key")?.Element("string")?.Value ?? string.Empty, StringComparer.Ordinal).ToArray();
        Require(groups.All(group => group.Key.Length > 0 && group.Count() == 1), "Friendship boundary save has ambiguous friendship keys.");
        var rows = groups.ToDictionary(group => group.Key,
            group => Required(Required(group.Single(), "value"), "Friendship"), StringComparer.Ordinal);
        int Points(string name) => int.Parse(Required(rows[name], "Points").Value, CultureInfo.InvariantCulture);
        foreach (var native in deficit.Villagers.Where(row => row.UniqueNpcIdentity))
            Require(native.FriendshipRowExists == rows.ContainsKey(native.NpcName) &&
                (!native.FriendshipRowExists || Points(native.NpcName) == native.FriendshipPoints),
                "Friendship boundary native points differ from source save: " + native.NpcName);
        var nearNames = new[] { "George", "Evelyn" };
        var social = Value(basis, "npcs", "social_interaction").EnumerateArray().ToArray();
        foreach (var name in nearNames)
        {
            var natives = deficit.Villagers.Where(row => row.NpcName == name).ToArray();
            var observed = social.Where(row => row.GetProperty("name").GetString() == name).ToArray();
            Require(natives.Length == 1 && natives[0].UniqueNpcIdentity && natives[0].FriendshipPoints is >= 0 &&
                natives[0].FriendshipPoints < threshold && rows.ContainsKey(name) &&
                Required(rows[name], "Status").Value == "Friendly" && observed.Length == 1 &&
                observed[0].GetProperty("location_id").GetString() == "JoshHouse" &&
                observed[0].GetProperty("can_socialize").GetBoolean() &&
                observed[0].GetProperty("expected_talk_friendship_delta_complete").GetBoolean() &&
                observed[0].GetProperty("expected_talk_friendship_delta").GetInt32() >= 20,
                "Friendship boundary needs two actual ordinary social targets in JoshHouse.");
        }
        var maps = Value(basis, "locations", "social_route_date_evidence").GetProperty("locations").EnumerateArray()
            .Where(row => row.GetProperty("location_id").GetString() == "JoshHouse").ToArray();
        Require(maps.Length == 1 && maps[0].GetProperty("static_walkable_tile_ranges").EnumerateArray().Any(range =>
            range.GetProperty("y").GetInt32() == 22 && range.GetProperty("start_x").GetInt32() <= 15 &&
            range.GetProperty("end_x").GetInt32() >= 15), "Friendship boundary starting tile lacks native walkability evidence.");
        var already = deficit.Villagers.Where(row => row.Qualifies).Select(row => row.NpcName).ToArray();
        Require(already.Distinct(StringComparer.Ordinal).Count() == already.Length && already.Length < request.TargetQualifyingCount &&
            already.All(rows.ContainsKey), "Friendship boundary cannot downgrade or ambiguously replace existing qualifiers.");
        var sociallyKnown = social.Where(row => row.GetProperty("can_socialize").GetBoolean())
            .Select(row => row.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);
        var seeds = already.Concat(deficit.Villagers.Where(row => row.UniqueNpcIdentity && rows.ContainsKey(row.NpcName) &&
                !nearNames.Contains(row.NpcName, StringComparer.Ordinal) && row.NpcName != "Alex" &&
                !row.Qualifies && sociallyKnown.Contains(row.NpcName) &&
                row.FriendshipPoints is >= 0 && row.FriendshipPoints < threshold)
            .Select(row => row.NpcName).OrderBy(name => name, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal).Take(request.TargetQualifyingCount - 1).ToArray();
        Require(seeds.Length == request.TargetQualifyingCount - 1 && !seeds.Intersect(nearNames, StringComparer.Ordinal).Any(),
            "Friendship boundary lacks distinct existing native prerequisite rows.");
        var changes = seeds.Concat(nearNames).Select(name => new { npc_name = name, before = Points(name),
            after = nearNames.Contains(name, StringComparer.Ordinal) ? threshold - 20 : Math.Max(threshold, Points(name)) }).ToArray();
        ApplyDeclaredCalendar(root!, player, request.DecisionWinterDayYearTwo);
        foreach (var change in changes)
        {
            Required(rows[change.npc_name], "Points").Value = change.after.ToString(CultureInfo.InvariantCulture);
            if (nearNames.Contains(change.npc_name, StringComparer.Ordinal)) Required(rows[change.npc_name], "TalkedToToday").Value = "false";
        }
        var events = Required(player, "eventsSeen");
        var seen = events.Elements("int").Select(row => row.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var eventId in LocalStoryEventIds.Where(id => !seen.Contains(id))) events.Add(new XElement("int", eventId));
        Required(player, "lastSleepLocation").Value = "JoshHouse";
        Required(Required(player, "lastSleepPoint"), "X").Value = "15";
        Required(Required(player, "lastSleepPoint"), "Y").Value = "22";
        Required(Required(player, "sleptInTemporaryBed"), "boolean").Value = "true";
        Required(Required(player, "Position"), "X").Value = (15 * 64).ToString(CultureInfo.InvariantCulture);
        Required(Required(player, "Position"), "Y").Value = (22 * 64).ToString(CultureInfo.InvariantCulture);
        IsolatedSaveBoundaryFiles.Commit(source, target, request.IsolationRoot, sourceHash, document);
        return new
        {
            schema_version = "friendship_terminal_save_boundary.v1", status = "prepared_local_prerequisites_not_native_outcome",
            request_path = requestPath, source_save_file = source, isolated_save_file = target,
            source_save_sha256 = sourceHash, prepared_save_sha256 = CurrentTeacherFrontierSupport.HashFile(target),
            basis_snapshot_path = basisPath, basis_snapshot_sha256 = CurrentTeacherFrontierSupport.HashFile(basisPath),
            target_qualifying_count = request.TargetQualifyingCount, declared_qualifying_count_before = request.TargetQualifyingCount - 1,
            starting_location = "JoshHouse", starting_tile_x = 15, starting_tile_y = 22,
            declared_completed_local_event_ids = LocalStoryEventIds.ToArray(), existing_event_records_preserved = true,
            prior_story_progress_verified = false,
            declared_point_changes = changes, existing_relationship_status_preserved = true, inventory_unchanged = true,
            prior_friendship_acquisition_verified = false, native_criterion_outcome_verified = false, formal_product_training_authorized = false
        };
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
