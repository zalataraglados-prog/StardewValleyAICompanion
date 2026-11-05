using System.Globalization;
using System.Xml.Linq;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class MasterAnglerTerminalSaveBoundaryBuilder
{
    public static MasterAnglerTerminalSaveBoundaryReport Prepare(string requestPath, string? outputPath = null)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<MasterAnglerTerminalSaveBoundaryRequest>(requestPath, "master_angler_terminal_save_boundary_request.v1");
        Require(request.DeclaredLocalPrerequisiteOnly && !request.FormalProductTrainingAuthorized &&
            request.DecisionSpringDayYearTwo is >= 1 and <= 28, "Terminal save preparation requires an explicit local boundary and valid year-two spring day.");
        var (source, target) = ValidateScope(request.SourceSaveFile, request.IsolatedSaveFile, request.IsolationRoot);
        if (outputPath is not null)
            IsolatedSaveBoundaryFiles.ValidateOutput(outputPath, request.IsolationRoot);
        var sourceHash = CurrentTeacherFrontierSupport.HashFile(source);
        Require(sourceHash == CurrentTeacherFrontierSupport.HashFile(target), "Terminal boundary target must be a fresh byte-identical save copy.");
        var inventoryPath = RequiredFile(request.RequirementInventoryPath);
        var loweringPath = RequiredFile(request.AcquisitionLoweringPath);
        var inventory = ReadTyped<AuthoritativeRequirementInventoryReport>(inventoryPath, "authoritative_goal_requirement_inventory.v1");
        var lowering = ReadTyped<AcquisitionRouteOptionLoweringReport>(loweringPath, "acquisition_route_option_lowering.v1");
        CurrentTeacherFrontierSupport.ValidateAuthority(inventoryPath, inventory, lowering, "Terminal save boundary");
        var set = CurrentTeacherFrontierSupport.SingleSet(inventory.RequirementSets, row => row.RequirementSetId, "master_angler", "Terminal save boundary");
        var lowered = CurrentTeacherFrontierSupport.SingleSet(lowering.RequirementSets, row => row.RequirementSetId, "master_angler", "Terminal save boundary lowering");
        CurrentMasterAnglerTeacherFrontierBuilder.ValidateSets(set, lowered);
        var document = IsolatedSaveBoundaryFiles.ReadDocument(target);
        PrepareXml(document, set.Groups.Select(row => row.Alternatives.Single().QualifiedItemId).ToArray(),
            request.MissingQualifiedItemId, request.DecisionSpringDayYearTwo, inventory.GameVersion);

        IsolatedSaveBoundaryFiles.Commit(source, target, request.IsolationRoot, sourceHash, document);
        return new(requestPath, CurrentTeacherFrontierSupport.HashFile(requestPath), source, target, sourceHash,
            CurrentTeacherFrontierSupport.HashFile(target), CurrentTeacherFrontierSupport.HashFile(inventoryPath),
            CurrentTeacherFrontierSupport.HashFile(loweringPath), request.MissingQualifiedItemId, request.DecisionSpringDayYearTwo);
    }

    private static void PrepareXml(XDocument document, string[] species, string missing, int day, string version)
    {
        Require(species.Length == 72 && species.Distinct(StringComparer.Ordinal).Count() == 72 && species.Contains(missing, StringComparer.Ordinal) &&
            species.All(id => id.StartsWith("(O)", StringComparison.Ordinal)) && day is >= 1 and <= 28 && version == "1.6.15",
            "Terminal boundary must retain exactly 72 authoritative fish and one declared missing species.");
        var root = document.Root;
        Require(root?.Name == "SaveGame", "Terminal boundary requires the full native SaveGame XML, not metadata.");
        XElement Required(XElement parent, string name) => parent.Elements(name).SingleOrDefault() ??
            throw new InvalidDataException("Terminal boundary save field missing: " + name);
        Require(Required(root!, "gameVersion").Value == version, "Terminal boundary save version differs from authority.");
        var player = Required(root!, "player");
        var caught = Required(player, "fishCaught");
        var achievements = Required(player, "achievements");
        var ids = achievements.Elements().Select(row => row.Name == "int" && int.TryParse(row.Value, out var id) && id >= 0
            ? id : throw new InvalidDataException("Terminal boundary save achievements are malformed.")).ToArray();
        Require(ids.Distinct().Count() == ids.Length && !ids.Contains(26), "Do not erase or inject an already earned Master Angler achievement.");
        var year = Required(root!, "year");
        var season = Required(root!, "currentSeason");
        var date = Required(root!, "dayOfMonth");
        var values = Required(Required(player, "stats"), "Values");
        var daysPlayed = values.Elements("item").SingleOrDefault(row => row.Element("key")?.Element("string")?.Value == "daysPlayed")?
            .Element("value")?.Element("unsignedInt") ?? throw new InvalidDataException("Terminal boundary native daysPlayed statistic is missing.");
        caught.ReplaceNodes(species.Where(id => id != missing).Order(StringComparer.Ordinal).Select(id => new XElement("item",
            new XElement("key", new XElement("string", id)), new XElement("value", new XElement("ArrayOfInt", new XElement("int", 1), new XElement("int", 1))))));
        foreach (var id in new[] { 24, 25 }.Where(id => !ids.Contains(id))) achievements.Add(new XElement("int", id));
        year.Value = "2";
        season.Value = "spring";
        date.Value = day.ToString(CultureInfo.InvariantCulture);
        daysPlayed.Value = (112 + day).ToString(CultureInfo.InvariantCulture);
    }

    private static (string Source, string Target) ValidateScope(string sourcePath, string targetPath, string isolationRoot)
        => IsolatedSaveBoundaryFiles.ValidateScope(sourcePath, targetPath, isolationRoot);
}
