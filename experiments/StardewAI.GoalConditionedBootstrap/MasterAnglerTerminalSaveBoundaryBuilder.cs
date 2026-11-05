using System.Globalization;
using System.Text;
using System.Xml;
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
        {
            Require(Path.IsPathFullyQualified(outputPath) && outputPath.IndexOfAny(new[] { '*', '?', '[', ']' }) < 0,
                "Terminal boundary report requires an exact absolute path.");
            var report = Path.GetFullPath(outputPath);
            var reportRoot = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.IsolationRoot)));
            Require(string.Equals(Path.GetDirectoryName(report), reportRoot, PathComparison) &&
                Path.GetExtension(report).Equals(".json", StringComparison.OrdinalIgnoreCase) && !File.Exists(report) &&
                !Directory.Exists(report) && new FileInfo(report).LinkTarget is null,
                "Terminal boundary report must be a new JSON file beside the isolation root, never an input or save file.");
        }
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
        using var reader = XmlReader.Create(target, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var document = XDocument.Load(reader);
        reader.Close();
        PrepareXml(document, set.Groups.Select(row => row.Alternatives.Single().QualifiedItemId).ToArray(),
            request.MissingQualifiedItemId, request.DecisionSpringDayYearTwo, inventory.GameVersion);

        // Replace only the clone's directory entry, even if a caller supplied a hard-linked file.
        var staged = target + ".boundary-" + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false }))
            document.Save(writer);
        ValidateScope(source, target, request.IsolationRoot);
        Require(sourceHash == CurrentTeacherFrontierSupport.HashFile(source) && sourceHash == CurrentTeacherFrontierSupport.HashFile(target),
            "Terminal boundary source or target changed during preparation.");
        File.Move(staged, target, overwrite: true);
        Require(sourceHash == CurrentTeacherFrontierSupport.HashFile(source), "Original save changed during terminal boundary preparation.");
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
    {
        static string Absolute(string path)
        {
            Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && path.IndexOfAny(new[] { '*', '?', '[', ']' }) < 0,
                "Terminal boundary paths must be exact absolute paths without wildcards.");
            return Path.GetFullPath(path);
        }
        var source = Absolute(sourcePath);
        var target = Absolute(targetPath);
        var root = Absolute(isolationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = root + Path.DirectorySeparatorChar;
        Require(Directory.Exists(root) && root != Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) &&
            File.Exists(source) && File.Exists(target) && !source.Equals(target, PathComparison) &&
            target.StartsWith(prefix, PathComparison) && !source.StartsWith(prefix, PathComparison) &&
            Path.GetFileName(source) == Path.GetFileName(target), "Terminal boundary must write only a strict isolated copy, never its source or a drive root.");
        foreach (var path in new[] { source, target, root })
        {
            Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "Terminal boundary rejects reparse points.");
            for (var parent = new DirectoryInfo(File.Exists(path) ? Path.GetDirectoryName(path)! : path); parent is not null; parent = parent.Parent)
                Require((parent.Attributes & FileAttributes.ReparsePoint) == 0, "Terminal boundary rejects reparse-point ancestors.");
        }
        return (source, target);
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
