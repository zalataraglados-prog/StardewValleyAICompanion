using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class MasterAnglerFreshSavePrerequisiteCompletionProofTests
{
    [Fact]
    public void CompleteAuditBindsExactFilesAndIdentityWithoutTrainingAdmission()
    {
        var fixture = new Fixture();
        var result = fixture.Build();
        Assert.True(result.Ready);
        Assert.Empty(result.BlockingReasons);
        Assert.Equal("grandpa.maximum_21", result.GoalId);
        Assert.Equal("1.6.15", result.GameVersion);
        Assert.Equal(fixture.AuditPath, result.AuditPath);
        Assert.Equal(Fixture.Hash(fixture.AuditPath), result.AuditSha256);
        Assert.Equal(fixture.DependencyPath, result.DependencyExpansionPath);
        Assert.Equal(Fixture.Hash(fixture.DependencyPath), result.DependencyExpansionSha256);
        Assert.Equal(fixture.ReceiptPath, result.GourmandContinuationReceiptPath);
        Assert.Equal(Fixture.Hash(fixture.ReceiptPath), result.GourmandContinuationReceiptSha256);
        Assert.Equal(3, result.VerifiedGourmandTransitionCount);
        Assert.Equal(72, result.NativeDenominatorCount);
        Assert.Equal(14, result.PrerequisiteFamilyCount);
        Assert.False(result.TrainingLabelEligible);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(
            MasterAnglerFreshSavePrerequisiteCompletionProofBuilder.Build(fixture.Audit,
                fixture.AuditPath, fixture.DependencyPath, "grandpa.maximum_21", "1.6.15")));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.False(json.RootElement.GetProperty("training_label_eligible").GetBoolean());
        Assert.Equal("master_angler_fresh_save_prerequisite_completion_proof.v1", result.SchemaVersion);
    }

    [Theory]
    [InlineData("status", "\"partial_fresh_save_prerequisite_inventory\"", "audit_not_complete")]
    [InlineData("training_label_eligible", "true", "audit_training_label_eligible")]
    [InlineData("static_opportunity_inventory_complete", "false", "static_opportunity_inventory_incomplete")]
    [InlineData("prerequisite_family_inventory_complete", "false", "prerequisite_family_inventory_incomplete")]
    [InlineData("missing_or_unbound_producer_families", "[\"ginger_island_access\"]", "missing_or_unbound_producer_families")]
    [InlineData("unknown_location_ids", "[\"unknown\"]", "unknown_location_ids")]
    [InlineData("species", "[]", "static_species_inventory_inconsistent")]
    [InlineData("native_denominator_count", "0", "static_species_inventory_inconsistent")]
    [InlineData("species_with_two_or_more_static_opportunity_days", "0", "static_species_inventory_inconsistent")]
    [InlineData("prerequisite_families", "[]", "prerequisite_family_inventory_inconsistent")]
    [InlineData("golden_walnut_gourmand_continuation_receipt_path", "\"\"", "gourmand_continuation_receipt_missing")]
    public void IncompleteInventoriesStayBlocked(string field, string value, string reason)
    {
        var fixture = new Fixture();
        fixture.ChangeAudit(root => root[field] = JsonNode.Parse(value));
        var result = fixture.Build();
        Assert.False(result.Ready);
        Assert.False(result.TrainingLabelEligible);
        Assert.Contains(reason, result.BlockingReasons);
    }

    [Theory]
    [InlineData("ready", "false")]
    [InlineData("verified_transition_count", "0")]
    [InlineData("verified_transition_count", "2")]
    [InlineData("verified_transition_count", "4")]
    [InlineData("blocking_reasons", "[\"blocked\"]")]
    [InlineData("blocking_reasons", "null")]
    [InlineData("schema_version", "\"other\"")]
    [InlineData("game_version", "\"1.6.14\"")]
    [InlineData("save_id", "\"\"")]
    [InlineData("source_run_id", "\"\"")]
    [InlineData("player_id", "\"\"")]
    [InlineData("source_episode_manifest_path", "\"\"")]
    [InlineData("source_episode_manifest_sha256", "\"\"")]
    [InlineData("source_episode_evidence_root_sha256", "\"bad\"")]
    [InlineData("native_receipt_evidence_root_sha256", "\"bad\"")]
    public void BoundButIncompleteReceiptNeverProducesReady(string field, string value)
    {
        var fixture = new Fixture();
        fixture.ChangeReceipt(root =>
        {
            root[field] = JsonNode.Parse(value);
            if (field == "ready")
                root["verified"] = JsonNode.Parse(value);
        });
        var result = fixture.Build();
        Assert.False(result.Ready);
        Assert.False(result.TrainingLabelEligible);
        Assert.NotEmpty(result.BlockingReasons);
    }

    [Theory]
    [InlineData("schema_version")]
    [InlineData("status")]
    [InlineData("goal_id")]
    [InlineData("game_version")]
    [InlineData("training_label_eligible")]
    [InlineData("static_opportunity_inventory_complete")]
    [InlineData("prerequisite_family_inventory_complete")]
    [InlineData("missing_or_unbound_producer_families")]
    [InlineData("unknown_location_ids")]
    [InlineData("dependency_expansion_path")]
    [InlineData("dependency_expansion_sha256")]
    [InlineData("golden_walnut_gourmand_continuation_receipt_path")]
    [InlineData("golden_walnut_gourmand_continuation_receipt_sha256")]
    [InlineData("golden_walnut_gourmand_continuation_receipt")]
    public void MissingNullOrDuplicateCriticalAuditFieldsAreRejected(string field)
    {
        var fixture = new Fixture();
        var original = File.ReadAllText(fixture.AuditPath);
        fixture.ChangeAuditFileOnly(root => root.Remove(field));
        Assert.Throws<InvalidDataException>(() => fixture.Build());
        File.WriteAllText(fixture.AuditPath, original);
        fixture.ChangeAuditFileOnly(root => root[field] = null);
        Assert.Throws<InvalidDataException>(() => fixture.Build());
        File.WriteAllText(fixture.AuditPath, original.Insert(1,
            JsonSerializer.Serialize(field) + ":" + JsonNode.Parse(original)![field]!.ToJsonString() + ","));
        Assert.Throws<InvalidDataException>(() => fixture.Build());
    }

    [Theory]
    [InlineData("training_label_eligible", "true")]
    [InlineData("training_label_eligible", null)]
    [InlineData("verified", "false")]
    [InlineData("ready", null)]
    [InlineData("blocking_reasons", null)]
    public void IgnoredOrDefaultedReceiptPropertiesCannotHideInvalidEvidence(string field, string? value)
    {
        var fixture = new Fixture();
        fixture.ChangeReceipt(root =>
        {
            if (value is null) root.Remove(field);
            else root[field] = JsonNode.Parse(value);
        });
        Assert.Throws<InvalidDataException>(() => fixture.Build());
    }

    [Theory]
    [InlineData("audit_goal")]
    [InlineData("audit_game")]
    [InlineData("audit_schema")]
    [InlineData("dependency_bytes")]
    [InlineData("dependency_goal")]
    [InlineData("dependency_path")]
    [InlineData("receipt_bytes")]
    [InlineData("embedded_receipt_training")]
    [InlineData("receipt_duplicate")]
    [InlineData("supplied_audit")]
    public void WrongIdentityHashOrReceiptBindingIsRejected(string change)
    {
        var fixture = new Fixture();
        switch (change)
        {
            case "audit_goal": fixture.ChangeAudit(root => root["goal_id"] = "other"); break;
            case "audit_game": fixture.ChangeAudit(root => root["game_version"] = "other"); break;
            case "audit_schema": fixture.ChangeAudit(root => root["schema_version"] = "other"); break;
            case "dependency_bytes": File.AppendAllText(fixture.DependencyPath, " "); break;
            case "dependency_goal":
                File.WriteAllText(fixture.DependencyPath,
                    "{\"schema_version\":\"goal_method_dependency_expansions.v1\",\"goal_id\":\"other\"}");
                fixture.ChangeAudit(root => root["dependency_expansion_sha256"] = Fixture.Hash(fixture.DependencyPath));
                break;
            case "dependency_path": fixture.ChangeAudit(root => root["dependency_expansion_path"] = fixture.AuditPath); break;
            case "receipt_bytes": File.AppendAllText(fixture.ReceiptPath, " "); break;
            case "embedded_receipt_training": fixture.ChangeAudit(root =>
                root["golden_walnut_gourmand_continuation_receipt"]!["training_label_eligible"] = true); break;
            case "receipt_duplicate":
                var text = File.ReadAllText(fixture.ReceiptPath);
                File.WriteAllText(fixture.ReceiptPath, text.Insert(1, "\"ready\":true,"));
                fixture.ChangeAudit(root => root["golden_walnut_gourmand_continuation_receipt_sha256"] = Fixture.Hash(fixture.ReceiptPath));
                break;
            case "supplied_audit":
                fixture.Audit.Status = "partial_fresh_save_prerequisite_inventory";
                Assert.Throws<InvalidDataException>(() => MasterAnglerFreshSavePrerequisiteCompletionProofBuilder.Build(
                    fixture.Audit, fixture.AuditPath, fixture.DependencyPath, "grandpa.maximum_21", "1.6.15"));
                return;
        }
        Assert.Throws<InvalidDataException>(() => fixture.Build());
    }

    [Theory]
    [InlineData("duplicate_species")]
    [InlineData("insufficient_static_days")]
    [InlineData("duplicate_family")]
    [InlineData("unbound_family")]
    [InlineData("unknown_species")]
    public void CompleteFlagsCannotHideInconsistentInventoryRows(string change)
    {
        var fixture = new Fixture();
        fixture.ChangeAudit(root =>
        {
            var species = root["species"]!.AsArray();
            var families = root["prerequisite_families"]!.AsArray();
            switch (change)
            {
                case "duplicate_species": species.Add(species[0]!.DeepClone()); break;
                case "insufficient_static_days": species[0]!["static_opportunity_day_count"] = 1; break;
                case "duplicate_family": families.Add(families[0]!.DeepClone()); break;
                case "unbound_family": families[0]!["producer_status"] = "reusable_but_unbound"; break;
                case "unknown_species": families[0]!["required_by_species_ids"] = new JsonArray("other"); break;
            }
        });
        Assert.False(fixture.Build().Ready);
    }

    [Theory]
    [InlineData("species", 71, "static_species_inventory_inconsistent")]
    [InlineData("species", 73, "static_species_inventory_inconsistent")]
    [InlineData("families", 13, "prerequisite_family_inventory_inconsistent")]
    [InlineData("families", 15, "prerequisite_family_inventory_inconsistent")]
    public void ExactAuthoritativeDenominatorsCannotDrift(string collection, int count, string blocker)
    {
        var fixture = new Fixture();
        fixture.ChangeAudit(root =>
        {
            var rows = root[collection == "species" ? "species" : "prerequisite_families"]!.AsArray();
            while (rows.Count > count)
                rows.RemoveAt(rows.Count - 1);
            while (rows.Count < count)
                rows.Add(rows[0]!.DeepClone());
        });
        var result = fixture.Build();
        Assert.False(result.Ready);
        Assert.Contains(blocker, result.BlockingReasons);
    }

    private sealed class Fixture
    {
        public string AuditPath { get; }
        public string DependencyPath { get; }
        public string ReceiptPath { get; }
        public MasterAnglerFreshSavePrerequisiteAuditReport Audit { get; private set; }

        public Fixture()
        {
            // A tiny upstream inventory tests the proof gate, not native species coverage.
            var root = Path.Combine(Path.GetTempPath(), "stardewai-completion-proof-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            AuditPath = Path.Combine(root, "audit.json");
            DependencyPath = Path.Combine(root, "dependencies.json");
            ReceiptPath = Path.Combine(root, "receipt.json");
            File.WriteAllText(DependencyPath,
                "{\"schema_version\":\"goal_method_dependency_expansions.v1\",\"goal_id\":\"grandpa.maximum_21\",\"directions\":[]}");
            var receipt = new GoldenWalnutGourmandContinuationReceipt(
                GoldenWalnutGourmandContinuationReceiptVerifier.SchemaVersion, true, 3, Array.Empty<string>(),
                Path.Combine(root, "manifest.json"), new string('a', 64), new string('b', 64), new string('c', 64),
                "run", "save", "player", "1.6.15");
            File.WriteAllText(ReceiptPath, JsonSerializer.Serialize(receipt));
            var species = Enumerable.Range(1, 72)
                .Select(index => new MasterAnglerFreshSaveSpeciesPrerequisite
                {
                    QualifiedItemId = "fixture-species-" + index,
                    StaticOpportunityDayCount = 2
                })
                .ToArray();
            var families = Enumerable.Range(1, 14)
                .Select(index => new MasterAnglerFreshSavePrerequisiteFamily
                {
                    FamilyId = index == 1 ? "ginger_island_access" : "fixture-family-" + index,
                    ProducerStatus = "implemented_and_bound",
                    RequiredBySpeciesIds = new[] { species[index - 1].QualifiedItemId }
                })
                .ToArray();
            Audit = new MasterAnglerFreshSavePrerequisiteAuditReport
            {
                Status = "fresh_save_prerequisite_inventory_complete", GoalId = "grandpa.maximum_21", GameVersion = "1.6.15",
                DependencyExpansionPath = DependencyPath, DependencyExpansionSha256 = Hash(DependencyPath),
                NativeDenominatorCount = 72, SpeciesWithTwoOrMoreStaticOpportunityDays = 72,
                StaticOpportunityInventoryComplete = true, PrerequisiteFamilyInventoryComplete = true,
                GoldenWalnutGourmandContinuationReceipt = receipt,
                GoldenWalnutGourmandContinuationReceiptPath = ReceiptPath,
                GoldenWalnutGourmandContinuationReceiptSha256 = Hash(ReceiptPath),
                Species = species,
                PrerequisiteFamilies = families
            };
            File.WriteAllText(AuditPath, JsonSerializer.Serialize(Audit));
        }

        public MasterAnglerFreshSavePrerequisiteCompletionProof Build() =>
            MasterAnglerFreshSavePrerequisiteCompletionProofBuilder.Build(
                Audit, AuditPath, DependencyPath, "grandpa.maximum_21", "1.6.15");

        public void ChangeAudit(Action<JsonObject> change)
        {
            var json = JsonNode.Parse(File.ReadAllText(AuditPath))!.AsObject();
            change(json);
            File.WriteAllText(AuditPath, json.ToJsonString());
            Audit = JsonSerializer.Deserialize<MasterAnglerFreshSavePrerequisiteAuditReport>(
                File.ReadAllText(AuditPath))!;
        }

        public void ChangeReceipt(Action<JsonObject> change)
        {
            var receipt = JsonNode.Parse(File.ReadAllText(ReceiptPath))!.AsObject();
            change(receipt);
            File.WriteAllText(ReceiptPath, receipt.ToJsonString());
            ChangeAudit(root =>
            {
                root["golden_walnut_gourmand_continuation_receipt"] = receipt.DeepClone();
                root["golden_walnut_gourmand_continuation_receipt_sha256"] = Hash(ReceiptPath);
            });
        }

        public void ChangeAuditFileOnly(Action<JsonObject> change)
        {
            var json = JsonNode.Parse(File.ReadAllText(AuditPath))!.AsObject();
            change(json);
            File.WriteAllText(AuditPath, json.ToJsonString());
        }

        public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }
}
