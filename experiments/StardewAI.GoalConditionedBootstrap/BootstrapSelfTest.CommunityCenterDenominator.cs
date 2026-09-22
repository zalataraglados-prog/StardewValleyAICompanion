using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunCurrentCommunityCenterDenominator(
        string requirementInventoryPath,
        string snapshotPath,
        string outputRoot)
    {
        var root = Path.Combine(
            Path.GetFullPath(outputRoot),
            "current-community-center-denominator-fixture");
        Directory.CreateDirectory(root);
        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            requirementInventoryPath,
            "Authoritative requirement inventory");

        var standardSnapshot = JsonNode.Parse(
                File.ReadAllText(Path.GetFullPath(snapshotPath)))!.AsObject();
        RefreshCommunityCenterCompleteCount(standardSnapshot);
        var standardPath = Path.Combine(root, "standard-snapshot.json");
        WriteNode(standardPath, standardSnapshot);
        var standard = CurrentCommunityCenterDenominatorBuilder.Build(
            requirementInventoryPath,
            standardPath);
        Require(standard.Status == "ready" &&
                standard.BundleMode == "standard" &&
                standard.ActiveBundleCount == 30 &&
                standard.SupplementalBundleCount == 1,
            "Current standard Community Center denominator was not admitted.");

        var remixedSnapshot = standardSnapshot.DeepClone().AsObject();
        ApplyRemixedFixture(
            remixedSnapshot,
            inventory.CommunityCenterDenominatorCatalog);
        RefreshCommunityCenterCompleteCount(remixedSnapshot);
        var remixedPath = Path.Combine(root, "remixed-snapshot.json");
        WriteNode(remixedPath, remixedSnapshot);
        var remixed = CurrentCommunityCenterDenominatorBuilder.Build(
            requirementInventoryPath,
            remixedPath);
        Require(remixed.Status == "ready" &&
                remixed.BundleMode == "remixed" &&
                remixed.ActiveBundleCount == 30 &&
                remixed.SupplementalBundleCount == 1 &&
                remixed.ActiveBundles.Count(value =>
                    !string.IsNullOrWhiteSpace(value.SourceTemplateId)) == 30,
            "A native remixed Community Center realization was not admitted.");

        var tamperedSnapshot = remixedSnapshot.DeepClone().AsObject();
        var tamperedRows = CommunityCenterRows(tamperedSnapshot);
        var firstArea = inventory.CommunityCenterDenominatorCatalog.RemixedAreas[0];
        var tamperedKey = firstArea.AreaName + "/" + firstArea.KeyIds[0];
        var tamperedRow = tamperedRows.Single(row =>
            string.Equals((string?)row["bundle_data_key"], tamperedKey,
                StringComparison.Ordinal));
        var tamperedIngredient = tamperedRow["ingredients"]!.AsArray()[0]!.AsObject();
        tamperedIngredient["required_stack"] =
            tamperedIngredient["required_stack"]!.GetValue<int>() + 1;
        var tamperedPath = Path.Combine(root, "tampered-snapshot.json");
        WriteNode(tamperedPath, tamperedSnapshot);
        var rejected = false;
        try
        {
            CurrentCommunityCenterDenominatorBuilder.Build(
                requirementInventoryPath,
                tamperedPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A tampered remixed Community Center ingredient was admitted.");

        Write(Path.Combine(root, "self-test-report.json"), new
        {
            schema_version =
                "current_community_center_denominator_self_test.v1",
            status = "pass",
            standard_mode = standard.BundleMode,
            remixed_mode = remixed.BundleMode,
            active_bundle_count = remixed.ActiveBundleCount,
            supplemental_bundle_count = remixed.SupplementalBundleCount,
            tampered_snapshot_rejected = rejected
        });
    }

    private static void ApplyRemixedFixture(
        JsonObject snapshot,
        CommunityCenterDenominatorCatalog catalog)
    {
        var rows = CommunityCenterRows(snapshot).ToDictionary(
            row => (string)row["bundle_data_key"]!,
            StringComparer.Ordinal);
        foreach (var area in catalog.RemixedAreas)
        {
            var assigned = new CommunityCenterBundleTemplate?[area.KeyIds.Length];
            var set = area.BundleSets.FirstOrDefault();
            foreach (var template in set?.Templates ??
                         Array.Empty<CommunityCenterBundleTemplate>())
            {
                assigned[template.Index] = template;
            }
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (var position = 0; position < assigned.Length; position++)
            {
                if (assigned[position] is not null)
                    continue;
                var indexed = area.PoolTemplates
                    .Where(template => template.Index == position &&
                        !used.Contains(template.TemplateId))
                    .OrderBy(template => template.TemplateId,
                        StringComparer.Ordinal)
                    .ToArray();
                var candidates = indexed.Length > 0
                    ? indexed
                    : area.PoolTemplates
                        .Where(template => template.Index == -1 &&
                            !used.Contains(template.TemplateId))
                        .OrderBy(template => template.TemplateId,
                            StringComparer.Ordinal)
                        .ToArray();
                assigned[position] = candidates.FirstOrDefault() ??
                    throw new InvalidDataException(
                        "Self-test could not assign a remixed pool template.");
                used.Add(assigned[position]!.TemplateId);
            }

            for (var position = 0; position < area.KeyIds.Length; position++)
            {
                var key = area.AreaName + "/" + area.KeyIds[position];
                var row = rows[key];
                var template = assigned[position]!;
                row["internal_name"] = template.InternalName;
                row["required_slot_count"] = template.RequiredItemCount;
                row["completed_ingredient_count"] = 0;
                row["complete"] = false;
                var ingredients = new JsonArray();
                for (var index = 0; index < template.PickCount; index++)
                {
                    var ingredient = template.IngredientSlots[index].Options[0];
                    ingredients.Add(new JsonObject
                    {
                        ["ingredient_index"] = index,
                        ["item_id_or_category"] = ingredient.ItemIdOrCategory,
                        ["required_stack"] = ingredient.Amount,
                        ["minimum_quality"] = ingredient.MinimumQuality,
                        ["completed"] = false
                    });
                }
                row["ingredients"] = ingredients;
            }
        }
    }

    private static JsonObject[] CommunityCenterRows(JsonObject snapshot) =>
        snapshot["state"]!["world_progress"]!["community_center"]!["value"]![
                "bundle_rows"]!
            .AsArray()
            .Select(value => value!.AsObject())
            .ToArray();

    private static void RefreshCommunityCenterCompleteCount(JsonObject snapshot)
    {
        var completed = CommunityCenterRows(snapshot)
            .Count(row => row["complete"]!.GetValue<bool>());
        snapshot["state"]!["world_progress"]!["community_center"]!["value"]![
            "complete_bundle_count"] = completed;
    }

    private static void WriteNode(string path, JsonObject value) =>
        File.WriteAllText(
            path,
            value.ToJsonString(JsonDefaults.Options) + Environment.NewLine);
}
