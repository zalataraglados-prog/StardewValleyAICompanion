using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
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
        Assert.Equal(new[] { "ginger_island_access" },
            result.SharedCapabilityCertificateIds);
        Assert.Equal(72, result.NativeDenominatorCount);
        Assert.Equal(14, result.PrerequisiteFamilyCount);
        Assert.False(result.TrainingLabelEligible);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(
            MasterAnglerFreshSavePrerequisiteCompletionProofBuilder.Build(fixture.Audit,
                fixture.AuditPath, fixture.DependencyPath, "grandpa.maximum_21", "1.6.15")));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.False(json.RootElement.GetProperty("training_label_eligible").GetBoolean());
        Assert.Equal("master_angler_fresh_save_prerequisite_completion_proof.v2", result.SchemaVersion);
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
    [InlineData("ginger_island_access_capability_certificate_path", "\"\"", "ginger_island_access_capability_certificate_missing")]
    [InlineData("ginger_island_access_capability_certificate", "null", "ginger_island_access_capability_certificate_missing")]
    public void IncompleteInventoriesStayBlocked(string field, string value, string reason)
    {
        var fixture = new Fixture();
        fixture.ChangeAudit(root => root[field] = JsonNode.Parse(value));
        var result = fixture.Build();
        Assert.False(result.Ready);
        Assert.False(result.TrainingLabelEligible);
        Assert.Contains(reason, result.BlockingReasons);
    }

    [Fact]
    public void BoundButIncompleteCapabilityCertificateNeverProducesReady()
    {
        var fixture = new Fixture();
        fixture.ChangeSnapshot(root =>
            root["state"]!["locations"]!["ginger_island_access"]!["value"]!
                ["target_access"]!["island_south_east_cave_accessible"] = false);
        var result = fixture.Build();
        Assert.False(result.Ready);
        Assert.False(result.TrainingLabelEligible);
        Assert.Contains("ginger_island_access_capability_certificate_invalid",
            result.BlockingReasons);
    }

    [Fact]
    public void CapabilityCertificateRejectsSnapshotStateHashMismatch()
    {
        var fixture = new Fixture();
        var snapshot = JsonNode.Parse(File.ReadAllText(fixture.SnapshotPath))!.AsObject();
        snapshot["state_hash"] = new string('0', 64);
        File.WriteAllText(fixture.SnapshotPath, snapshot.ToJsonString());

        Assert.Contains("does not match its state", Assert.Throws<InvalidDataException>(() =>
            MasterAnglerSharedCapabilityCertificateBuilder.ReadAndReplay(
                fixture.CertificatePath)).Message);
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
    [InlineData("ginger_island_access_capability_certificate_path")]
    [InlineData("ginger_island_access_capability_certificate_sha256")]
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
    public void IgnoredOrDefaultedCertificatePropertiesCannotHideInvalidEvidence(string field, string? value)
    {
        var fixture = new Fixture();
        fixture.ChangeCertificateFileOnly(root =>
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
    [InlineData("certificate_bytes")]
    [InlineData("embedded_certificate_training")]
    [InlineData("certificate_duplicate")]
    [InlineData("supplied_audit")]
    public void WrongIdentityHashOrCertificateBindingIsRejected(string change)
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
            case "certificate_bytes": File.AppendAllText(fixture.CertificatePath, " "); break;
            case "embedded_certificate_training": fixture.ChangeAudit(root =>
                root["ginger_island_access_capability_certificate"]!["training_label_eligible"] = true); break;
            case "certificate_duplicate":
                var text = File.ReadAllText(fixture.CertificatePath);
                File.WriteAllText(fixture.CertificatePath, text.Insert(1, "\"ready\":true,"));
                fixture.ChangeAudit(root => root["ginger_island_access_capability_certificate_sha256"] =
                    Fixture.Hash(fixture.CertificatePath));
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

    [Fact]
    public void ReadyProofClosesOnlyTheBoundMasterAnglerDependency()
    {
        var fixture = new Fixture();
        var proofPath = fixture.WriteProof();
        var result = MasterAnglerDependencyExpansionCompletionBuilder.Build(
            proofPath, fixture.DependencyPath, "grandpa.maximum_21", "1.6.15");
        var direction = result["directions"]!.AsArray()
            .Single(value => value!["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler")!.AsObject();
        Assert.Equal("complete", direction["status"]!.GetValue<string>());
        Assert.Empty(direction["blockers"]!.AsArray());
        var inventory = direction["nodes"]!.AsArray()
            .Single(value => value!["id"]!.GetValue<string>() ==
                "dependency.complete_master_angler.fresh_save_prerequisite_inventory")!;
        Assert.Equal("fresh_save_prerequisite_inventory_complete",
            inventory["rule"]!["status"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(proofPath),
            inventory["rule"]!["completion_proof_path"]!.GetValue<string>());
        Assert.Equal(Fixture.Hash(proofPath),
            inventory["rule"]!["completion_proof_sha256"]!.GetValue<string>());
        Assert.False(inventory["rule"]!["training_label_eligible"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("proof")]
    [InlineData("dependency")]
    [InlineData("duplicate_direction")]
    [InlineData("missing_node")]
    public void DependencyClosureRejectsDriftedOrIncompleteEvidence(string change)
    {
        var fixture = new Fixture();
        var proofPath = fixture.WriteProof();
        switch (change)
        {
            case "proof":
            {
                var proof = JsonNode.Parse(File.ReadAllText(proofPath))!;
                proof["audit_sha256"] = new string('0', 64);
                File.WriteAllText(proofPath, proof.ToJsonString());
                break;
            }
            case "dependency": File.AppendAllText(fixture.DependencyPath, " "); break;
            case "duplicate_direction":
            {
                var root = JsonNode.Parse(File.ReadAllText(fixture.DependencyPath))!;
                root["directions"]!.AsArray().Add(
                    root["directions"]![0]!.DeepClone());
                File.WriteAllText(fixture.DependencyPath, root.ToJsonString());
                break;
            }
            case "missing_node":
            {
                var root = JsonNode.Parse(File.ReadAllText(fixture.DependencyPath))!;
                root["directions"]![0]!["nodes"]!.AsArray().RemoveAt(0);
                File.WriteAllText(fixture.DependencyPath, root.ToJsonString());
                break;
            }
        }
        Assert.Throws<InvalidDataException>(() =>
            MasterAnglerDependencyExpansionCompletionBuilder.Build(
                proofPath, fixture.DependencyPath,
                "grandpa.maximum_21", "1.6.15"));
    }

    private sealed class Fixture
    {
        public string AuditPath { get; }
        public string DependencyPath { get; }
        public string SnapshotPath { get; }
        public string CertificatePath { get; }
        public MasterAnglerFreshSavePrerequisiteAuditReport Audit { get; private set; }

        public Fixture()
        {
            // A tiny upstream inventory tests the proof gate, not native species coverage.
            var root = Path.Combine(Path.GetTempPath(), "stardewai-completion-proof-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            AuditPath = Path.Combine(root, "audit.json");
            DependencyPath = Path.Combine(root, "dependencies.json");
            SnapshotPath = Path.Combine(root, "snapshot.json");
            CertificatePath = Path.Combine(root, "capability-certificate.json");
            var requiredNodeIds = new[]
            {
                "dependency.complete_master_angler.fresh_save_prerequisite_inventory",
                "dependency.complete_master_angler.target_date_opportunity_producer",
                "dependency.complete_master_angler.trap_lifecycle_intent",
                "dependency.complete_master_angler.current_legal_rod_attempt",
                "dependency.complete_master_angler.current_ready_trap_collection",
                "dependency.complete_master_angler.acquisition_recurrence",
                "dependency.complete_master_angler.all_native_species_caught"
            };
            File.WriteAllText(DependencyPath, JsonSerializer.Serialize(new
            {
                schema_version = "goal_method_dependency_expansions.v1",
                goal_id = "grandpa.maximum_21",
                directions = new[]
                {
                    new
                    {
                        direction_id = "complete_master_angler",
                        status = "in_progress",
                        completion_node_id = requiredNodeIds[^1],
                        source_ids = new[] { "fixture" },
                        blockers = new[] { "runtime_completion_proof_missing" },
                        nodes = requiredNodeIds.Select(id => new
                        {
                            id,
                            kind = id.EndsWith("all_native_species_caught",
                                StringComparison.Ordinal)
                                ? "terminal_fact"
                                : "state_fact",
                            rule = new { status = "pending" }
                        }),
                        edges = Array.Empty<object>()
                    }
                }
            }));
            var snapshot = new SnapshotEnvelope
            {
                GameVersion = "1.6.15",
                SaveId = new FieldEnvelope<string?>
                {
                    Status = FieldStatus.Available,
                    Value = "save"
                },
                PlayerId = new FieldEnvelope<string?>
                {
                    Status = FieldStatus.Available,
                    Value = "player"
                },
                GameTick = 123,
                State = new Dictionary<string, JsonElement>
                {
                    ["locations"] = JsonSerializer.SerializeToElement(new
                    {
                        ginger_island_access = new
                        {
                            status = "available",
                            value = new
                            {
                                schema_version = "ginger_island_access.v1",
                                projection_status =
                                    "complete_locked_base_1.6.15_native_contract",
                                target_access = new
                                {
                                    island_south_accessible = true,
                                    island_north_accessible = true,
                                    island_west_accessible = true,
                                    island_south_east_accessible = true,
                                    island_south_east_cave_accessible = true
                                }
                            }
                        }
                    })
                }
            };
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            File.WriteAllText(SnapshotPath, JsonSerializer.Serialize(snapshot));
            var certificate =
                MasterAnglerSharedCapabilityCertificateBuilder.BuildGingerIslandAccess(
                    SnapshotPath,
                    "run");
            File.WriteAllText(CertificatePath, JsonSerializer.Serialize(certificate));
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
                GingerIslandAccessCapabilityCertificate = certificate,
                GingerIslandAccessCapabilityCertificatePath = CertificatePath,
                GingerIslandAccessCapabilityCertificateSha256 = Hash(CertificatePath),
                Species = species,
                PrerequisiteFamilies = families
            };
            File.WriteAllText(AuditPath, JsonSerializer.Serialize(Audit));
        }

        public MasterAnglerFreshSavePrerequisiteCompletionProof Build() =>
            MasterAnglerFreshSavePrerequisiteCompletionProofBuilder.Build(
                Audit, AuditPath, DependencyPath, "grandpa.maximum_21", "1.6.15");

        public string WriteProof()
        {
            var path = Path.Combine(Path.GetDirectoryName(AuditPath)!, "proof.json");
            File.WriteAllText(path, JsonSerializer.Serialize(Build()));
            return path;
        }

        public void ChangeAudit(Action<JsonObject> change)
        {
            var json = JsonNode.Parse(File.ReadAllText(AuditPath))!.AsObject();
            change(json);
            File.WriteAllText(AuditPath, json.ToJsonString());
            Audit = JsonSerializer.Deserialize<MasterAnglerFreshSavePrerequisiteAuditReport>(
                File.ReadAllText(AuditPath))!;
        }

        public void ChangeSnapshot(Action<JsonObject> change)
        {
            var snapshot = JsonNode.Parse(File.ReadAllText(SnapshotPath))!.AsObject();
            change(snapshot);
            var state = snapshot["state"]!.Deserialize<Dictionary<string, JsonElement>>()!;
            snapshot["state_hash"] = SnapshotHash.ComputeStateHash(state);
            File.WriteAllText(SnapshotPath, snapshot.ToJsonString());
            var certificate =
                MasterAnglerSharedCapabilityCertificateBuilder.BuildGingerIslandAccess(
                    SnapshotPath,
                    "run");
            File.WriteAllText(CertificatePath, JsonSerializer.Serialize(certificate));
            ChangeAudit(root =>
            {
                root["ginger_island_access_capability_certificate"] =
                    JsonSerializer.SerializeToNode(certificate);
                root["ginger_island_access_capability_certificate_sha256"] =
                    Hash(CertificatePath);
            });
        }

        public void ChangeCertificateFileOnly(Action<JsonObject> change)
        {
            var certificate = JsonNode.Parse(
                File.ReadAllText(CertificatePath))!.AsObject();
            change(certificate);
            File.WriteAllText(CertificatePath, certificate.ToJsonString());
            ChangeAudit(root =>
                root["ginger_island_access_capability_certificate_sha256"] =
                    Hash(CertificatePath));
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
