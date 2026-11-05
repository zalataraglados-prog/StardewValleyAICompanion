using System.Globalization;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record SkillTerminalSaveBoundaryRequest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("source_save_file")] string SourceSaveFile,
    [property: JsonPropertyName("isolated_save_file")] string IsolatedSaveFile,
    [property: JsonPropertyName("isolation_root")] string IsolationRoot,
    [property: JsonPropertyName("target_scoring_level")] int TargetScoringLevel,
    [property: JsonPropertyName("decision_spring_day_year_two")] int DecisionSpringDayYearTwo,
    [property: JsonPropertyName("declared_local_prerequisite_only")] bool DeclaredLocalPrerequisiteOnly,
    [property: JsonPropertyName("formal_product_training_authorized")] bool FormalProductTrainingAuthorized);

public static class SkillTerminalSaveBoundaryBuilder
{
    public static object Prepare(string requestPath, string outputPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<SkillTerminalSaveBoundaryRequest>(requestPath, "skill_terminal_save_boundary_request.v1");
        Require(request.DeclaredLocalPrerequisiteOnly && !request.FormalProductTrainingAuthorized &&
            request.TargetScoringLevel is 15 or 25 && request.DecisionSpringDayYearTwo is >= 1 and <= 28,
            "Skill boundary requires a declared local 15/25 prerequisite and valid spring day.");
        var (source, target) = IsolatedSaveBoundaryFiles.ValidateScope(request.SourceSaveFile, request.IsolatedSaveFile, request.IsolationRoot);
        IsolatedSaveBoundaryFiles.ValidateOutput(outputPath, request.IsolationRoot);
        var sourceHash = CurrentTeacherFrontierSupport.HashFile(source);
        Require(sourceHash == CurrentTeacherFrontierSupport.HashFile(target), "Skill boundary needs a byte-identical fresh clone.");
        var document = IsolatedSaveBoundaryFiles.ReadDocument(target);
        XElement Required(XElement parent, string name) => parent.Elements(name).SingleOrDefault() ??
            throw new InvalidDataException("Skill boundary save field missing: " + name);
        var root = document.Root;
        Require(root?.Name == "SaveGame" && Required(root!, "gameVersion").Value == NativeSkillExperienceRules.GameVersion,
            "Skill boundary requires the full versioned native save.");
        var player = Required(root!, "player");
        Require(Required(player, "luckLevel").Value == "0", "Skill boundary cannot reset a nonvanilla Luck level.");
        var xp = Required(player, "experiencePoints").Elements().ToArray();
        Require(xp.Length == 6 && xp.All(row => row.Name == "int" && int.TryParse(row.Value, out var value) && value >= 0) &&
            xp[5].Value == "0", "Skill boundary needs six valid native XP slots and dormant Luck.");
        var fields = new[] { "farmingLevel", "fishingLevel", "foragingLevel", "miningLevel", "combatLevel" };
        var levels = fields.Select(field => Required(player, field)).ToArray();
        Require(levels.All(row => int.TryParse(row.Value, out var value) && value is >= 0 and <= 10), "Skill boundary base levels are invalid.");
        var otherLevel = request.TargetScoringLevel == 15 ? 6 : 10;
        var nextFishingLevel = request.TargetScoringLevel == 15 ? 6 : 10;
        var fishingXpBefore = NativeSkillExperienceRules.ExperienceForLevel(nextFishingLevel) - 5;
        for (var index = 0; index < 5; index++)
        {
            levels[index].Value = (index == 1 ? nextFishingLevel - 1 : otherLevel).ToString(CultureInfo.InvariantCulture);
            xp[index].Value = (index == 1 ? fishingXpBefore : NativeSkillExperienceRules.ExperienceForLevel(otherLevel))
                .ToString(CultureInfo.InvariantCulture);
        }
        Required(root!, "year").Value = "2";
        Required(root!, "currentSeason").Value = "spring";
        Required(root!, "dayOfMonth").Value = request.DecisionSpringDayYearTwo.ToString(CultureInfo.InvariantCulture);
        var values = Required(Required(player, "stats"), "Values");
        var daysPlayed = values.Elements("item").SingleOrDefault(row => row.Element("key")?.Element("string")?.Value == "daysPlayed")?
            .Element("value")?.Element("unsignedInt") ?? throw new InvalidDataException("Skill boundary daysPlayed missing.");
        daysPlayed.Value = (112 + request.DecisionSpringDayYearTwo).ToString(CultureInfo.InvariantCulture);
        IsolatedSaveBoundaryFiles.Commit(source, target, request.IsolationRoot, sourceHash, document);
        return new
        {
            schema_version = "skill_terminal_save_boundary.v1", status = "prepared_local_prerequisites_not_native_outcome",
            request_path = requestPath, source_save_file = source, isolated_save_file = target,
            source_save_sha256 = sourceHash, prepared_save_sha256 = CurrentTeacherFrontierSupport.HashFile(target),
            target_scoring_level = request.TargetScoringLevel, measured_skill_id = "fishing", expected_native_gain = 5,
            fishing_level_before = nextFishingLevel - 1, fishing_experience_before = fishingXpBefore,
            decision_spring_day_year_two = request.DecisionSpringDayYearTwo,
            prior_skill_acquisition_verified = false, native_criterion_outcome_verified = false,
            formal_product_training_authorized = false
        };
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
