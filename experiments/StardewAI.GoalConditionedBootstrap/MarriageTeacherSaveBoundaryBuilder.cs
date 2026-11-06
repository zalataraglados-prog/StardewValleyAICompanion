using System.Globalization;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MarriageTeacherSaveBoundaryRequest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("source_save_file")] string SourceSaveFile,
    [property: JsonPropertyName("isolated_save_file")] string IsolatedSaveFile,
    [property: JsonPropertyName("isolation_root")] string IsolationRoot,
    [property: JsonPropertyName("basis_snapshot_path")] string BasisSnapshotPath,
    [property: JsonPropertyName("decision_winter_day_year_two")] int DecisionWinterDayYearTwo,
    [property: JsonPropertyName("declared_local_prerequisites_only")] bool DeclaredLocalPrerequisitesOnly,
    [property: JsonPropertyName("formal_product_training_authorized")] bool FormalProductTrainingAuthorized)
{
    [JsonPropertyName("unmarried_npc_basis_snapshot_path")]
    public string? UnmarriedNpcBasisSnapshotPath { get; init; }
}

public static class MarriageTeacherSaveBoundaryBuilder
{
    public static object Prepare(string requestPath, string outputPath)
    {
        var request = ReadTyped<MarriageTeacherSaveBoundaryRequest>(requestPath, "marriage_teacher_save_boundary_request.v1");
        Require(request.DeclaredLocalPrerequisitesOnly && !request.FormalProductTrainingAuthorized &&
            request.DecisionWinterDayYearTwo is >= 1 and <= 28, "Marriage boundary requires declared isolated prerequisites and a valid winter day.");
        var (source, target) = IsolatedSaveBoundaryFiles.ValidateScope(request.SourceSaveFile, request.IsolatedSaveFile, request.IsolationRoot);
        IsolatedSaveBoundaryFiles.ValidateOutput(outputPath, request.IsolationRoot);
        var sourceHash = CurrentTeacherFrontierSupport.HashFile(source);
        Require(sourceHash == CurrentTeacherFrontierSupport.HashFile(target), "Marriage boundary requires a byte-identical clone.");
        var basis = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(RequiredFile(request.BasisSnapshotPath), "Marriage boundary native basis");
        var current = new CurrentMarriageHouseStateBuilder().Build(basis);
        Require(current.Status == "ready_current_state_only" && current.MarriedOrRoommate == true && current.Spouse == "Abigail" &&
            current.FarmhouseUpgradeLevel >= 2 && current.DaysUntilFarmhouseUpgrade == -1, "Marriage boundary requires its exact existing NPC marriage and settled house basis.");
        var document = IsolatedSaveBoundaryFiles.ReadDocument(target);
        XElement Required(XElement parent, string name) => parent.Elements(name).SingleOrDefault() ??
            throw new InvalidDataException("Marriage boundary field missing or duplicated: " + name);
        var root = document.Root;
        Require(root?.Name == "SaveGame" && Required(root!, "gameVersion").Value == basis.GameVersion,
            "Marriage boundary game version differs from its native basis.");
        var player = Required(root!, "player");
        Require(Required(player, "farmName").Value == basis.SaveId.Value && Required(player, "UniqueMultiplayerID").Value == basis.PlayerId.Value &&
            Required(player, "spouse").Value == current.Spouse && Required(player, "houseUpgradeLevel").Value == current.FarmhouseUpgradeLevel?.ToString(CultureInfo.InvariantCulture) &&
            Required(player, "daysUntilHouseUpgrade").Value == "-1", "Marriage boundary farmer or house differs from its native basis.");
        Require(Required(root!, "year").Value == current.Year?.ToString(CultureInfo.InvariantCulture) &&
            Required(root!, "currentSeason").Value == current.Season && Required(root!, "dayOfMonth").Value == current.Day?.ToString(CultureInfo.InvariantCulture),
            "Marriage boundary source date differs from its native basis.");
        var matches = Required(player, "friendshipData").Elements("item")
            .Where(row => row.Element("key")?.Element("string")?.Value == "Abigail").ToArray();
        var observed = Value(basis, "npcs", "friendships").EnumerateArray().Where(row => row.GetProperty("npc_name").GetString() == "Abigail").ToArray();
        Require(matches.Length == 1 && observed.Length == 1, "Marriage boundary target identity is missing or ambiguous.");
        var friendship = Required(Required(matches[0], "value"), "Friendship");
        Require(Required(friendship, "Status").Value == "Married" && observed[0].GetProperty("status").GetString() == "Married" &&
            Required(friendship, "Points").Value == observed[0].GetProperty("points").GetInt32().ToString(CultureInfo.InvariantCulture),
            "Marriage boundary relationship differs from its native basis.");
        var statistics = Required(Required(player, "stats"), "Values").Elements("item")
            .Where(row => row.Element("key")?.Element("string")?.Value == "daysPlayed").ToArray();
        Require(statistics.Length == 1 && uint.TryParse(statistics[0].Element("value")?.Element("unsignedInt")?.Value,
            NumberStyles.Integer, CultureInfo.InvariantCulture, out _), "Marriage boundary requires one valid native daysPlayed statistic.");
        var catalog = Value(basis, "npcs", "schedule_catalog").GetProperty("villagers").EnumerateArray()
            .Where(row => row.GetProperty("npc_name").GetString() == "Abigail").ToArray();
        Require(catalog.Length == 1, "Marriage boundary native default location is ambiguous.");
        var ordinaryPath = RequiredFile(request.UnmarriedNpcBasisSnapshotPath ?? string.Empty);
        var ordinary = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(ordinaryPath, "Unmarried NPC native default metadata");
        var ordinaryState = new CurrentMarriageHouseStateBuilder().Build(ordinary);
        Require(ordinary.GameVersion == basis.GameVersion && ordinary.SaveId.Value == basis.SaveId.Value && ordinary.PlayerId.Value == basis.PlayerId.Value &&
            ordinaryState.Status == "ready_current_state_only" && ordinaryState.MarriedOrRoommate == false && ordinaryState.Engaged == false &&
            ordinaryState.Spouse == string.Empty, "Marriage boundary normal-home metadata must come from the same native farmer without a partnership.");
        var normalRows = Value(ordinary, "npcs", "schedule_catalog").GetProperty("villagers").EnumerateArray()
            .Where(row => row.GetProperty("npc_name").GetString() == "Abigail").ToArray();
        Require(normalRows.Length == 1, "Marriage boundary unmarried native default is ambiguous.");
        var defaultMap = normalRows[0].GetProperty("default_map").GetString();
        var defaultX = normalRows[0].GetProperty("default_tile_x").GetInt32();
        var defaultY = normalRows[0].GetProperty("default_tile_y").GetInt32();
        Require(defaultX >= 0 && defaultX <= int.MaxValue / 64 && defaultY >= 0 && defaultY <= int.MaxValue / 64,
            "Marriage boundary default coordinate is invalid.");
        var locations = Required(root!, "locations").Elements().ToArray();
        var actors = locations.SelectMany(location => location.Elements("characters").SelectMany(list => list.Elements())
            .Where(actor => actor.Element("name")?.Value == "Abigail")).ToArray();
        var homes = locations.Where(location => location.Element("name")?.Value == defaultMap).ToArray();
        Require(actors.Length == 1 && homes.Length == 1 && !string.IsNullOrWhiteSpace(defaultMap) &&
            Required(actors[0], "defaultMap").Value == catalog[0].GetProperty("default_map").GetString(), "Marriage boundary lacks the exact serialized NPC/native default owner.");
        var spawn = Required(actors[0], "DefaultPosition");
        Require(float.TryParse(Required(spawn, "X").Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
            float.TryParse(Required(spawn, "Y").Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _), "Marriage boundary native default position is invalid.");

        Required(root!, "year").Value = "2";
        Required(root!, "currentSeason").Value = "winter";
        Required(root!, "dayOfMonth").Value = request.DecisionWinterDayYearTwo.ToString(CultureInfo.InvariantCulture);
        Required(Required(statistics[0], "value"), "unsignedInt").Value = (196 + request.DecisionWinterDayYearTwo).ToString(CultureInfo.InvariantCulture);
        Required(player, "spouse").Value = string.Empty;
        Required(friendship, "Status").Value = "Dating";
        Required(friendship, "Points").Value = "2420";
        foreach (var field in new[] { "GiftsToday", "GiftsThisWeek" }) Required(friendship, field).Value = "0";
        foreach (var field in new[] { "TalkedToToday", "ProposalRejected", "RoommateMarriage" }) Required(friendship, field).Value = "false";
        // Married defaults themselves are serialized. Restore normal defaults from an observed unmarried native load, not example coordinates.
        Required(actors[0], "defaultMap").Value = defaultMap!;
        Required(spawn, "X").Value = (defaultX * 64).ToString(CultureInfo.InvariantCulture);
        Required(spawn, "Y").Value = (defaultY * 64).ToString(CultureInfo.InvariantCulture);
        Required(actors[0], "Position").ReplaceWith(new XElement("Position", spawn.Elements().Select(field => new XElement(field))));
        actors[0].Remove();
        Required(homes[0], "characters").Add(actors[0]);
        IsolatedSaveBoundaryFiles.Commit(source, target, request.IsolationRoot, sourceHash, document);
        return new { schema_version = "marriage_teacher_save_boundary.v1", source_save_sha256 = sourceHash,
            isolated_save_sha256 = CurrentTeacherFrontierSupport.HashFile(target), target_npc = "Abigail", declared_points = 2420,
            declared_status = "Dating", declared_winter_day_year_two = request.DecisionWinterDayYearTwo,
            original_house_inventory_player_sleep_location_and_other_relationships_preserved = true,
            target_npc_serialized_owner_and_position_restored_to_native_default = defaultMap, declared_local_prerequisites_only = true,
            unmarried_default_metadata_path = ordinaryPath, unmarried_default_metadata_sha256 = CurrentTeacherFrontierSupport.HashFile(ordinaryPath),
            prior_partnership_acquisition_verified = false, prior_house_acquisition_verified = false,
            native_criterion_outcome_verified = false, formal_product_training_authorized = false };
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
