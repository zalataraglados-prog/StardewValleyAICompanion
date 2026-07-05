using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.WorldModel;

namespace StardewAI.Core.WorldModel
{
    public sealed class WorldModelProjector
    {
        private static readonly string[] RequiredPlannerFactPaths =
        {
            "identity.save_id",
            "identity.player_id",
            "time.season",
            "time.day",
            "time.time",
            "time.weather",
            "player.location_id",
            "player.money",
            "player.energy",
            "player.inventory",
            "farm.crops",
            "menus.active_menu",
            "transport.event_stream_websocket"
        };

        public WorldModelEnvelope Project(SnapshotEnvelope snapshot, string goal, string mode)
        {
            var requiredFacts = RequiredPlannerFactPaths
                .Select(path => ReadPlannerFact(snapshot, path))
                .ToArray();
            var missing = requiredFacts
                .Where(fact => !FieldEnvelopeValidator.IsReadableStatus(fact.Status))
                .Select(fact => fact.Path)
                .ToArray();
            var blockReasons = BuildBlockReasons(snapshot, missing);

            return new WorldModelEnvelope
            {
                StateHash = snapshot.StateHash,
                SnapshotSchemaVersion = snapshot.SchemaVersion,
                GameTick = snapshot.GameTick,
                InGameTime = ReadScalar<int?>(snapshot, "time.time") ?? snapshot.InGameTime.Value,
                RealTimestamp = snapshot.RealTimestamp,
                UserGoal = goal,
                Mode = string.IsNullOrWhiteSpace(mode) ? "relaxed" : mode,
                Completeness = new WorldModelCompleteness
                {
                    SnapshotCompleteness = snapshot.Completeness,
                    UnavailableCount = snapshot.UnavailableFields.Length,
                    UnavailableFields = snapshot.UnavailableFields,
                    RequiredFactCount = requiredFacts.Length,
                    ReadableRequiredFactCount = requiredFacts.Length - missing.Length,
                    AllRequiredFactsReadable = missing.Length == 0
                },
                Facts = new WorldModelFacts
                {
                    Game = ProjectFacts(snapshot, new Dictionary<string, string>
                    {
                        ["save_id"] = "identity.save_id",
                        ["player_id"] = "identity.player_id",
                        ["season"] = "time.season",
                        ["day"] = "time.day",
                        ["time"] = "time.time",
                        ["weather"] = "time.weather"
                    }),
                    Player = ProjectFacts(snapshot, new Dictionary<string, string>
                    {
                        ["location_id"] = "player.location_id",
                        ["tile_x"] = "player.tile_x",
                        ["tile_y"] = "player.tile_y",
                        ["facing_direction"] = "player.facing_direction",
                        ["money"] = "player.money",
                        ["health"] = "player.health",
                        ["max_health"] = "player.max_health",
                        ["energy"] = "player.energy",
                        ["max_energy"] = "player.max_energy",
                        ["current_tool"] = "player.current_tool",
                        ["inventory"] = "player.inventory"
                    }),
                    Farm = ProjectSection(snapshot, "farm"),
                    CurrentLocation = ProjectSection(snapshot, "current_location"),
                    Npcs = ProjectSection(snapshot, "npcs"),
                    Quests = ProjectSection(snapshot, "quests"),
                    WorldProgress = ProjectSection(snapshot, "world_progress"),
                    Menus = ProjectSection(snapshot, "menus"),
                    Mods = ProjectSection(snapshot, "mods"),
                    ModdedState = ProjectSection(snapshot, "modded_state")
                },
                PlannerInputs = new PlannerInputSummary
                {
                    Goal = goal,
                    Mode = string.IsNullOrWhiteSpace(mode) ? "relaxed" : mode,
                    RequiredFacts = requiredFacts,
                    Blocked = blockReasons.Length > 0,
                    BlockReasons = blockReasons
                },
                Audit = new WorldModelAudit
                {
                    Notes = new[]
                    {
                        "Only readable field envelopes are projected into facts.",
                        "Unavailable, stale, or error envelopes are counted and retained in planner_inputs instead of guessed.",
                        "The original snapshot state_hash remains the authority for replay and verification."
                    }
                }
            };
        }

        private static string[] BuildBlockReasons(SnapshotEnvelope snapshot, string[] missingRequiredFacts)
        {
            var reasons = new List<string>();
            if (string.IsNullOrWhiteSpace(snapshot.StateHash))
            {
                reasons.Add("state_hash_missing");
            }

            if (snapshot.UnavailableFields.Length > 0)
            {
                reasons.Add("snapshot_has_unavailable_fields");
            }

            if (missingRequiredFacts.Length > 0)
            {
                reasons.Add("required_facts_unreadable:" + string.Join(",", missingRequiredFacts));
            }

            return reasons.ToArray();
        }

        private static Dictionary<string, JsonElement> ProjectSection(SnapshotEnvelope snapshot, string sectionName)
        {
            if (!snapshot.State.TryGetValue(sectionName, out var section) || section.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, JsonElement>();
            }

            return section.EnumerateObject()
                .Where(property => TryReadEnvelopeValue(property.Value, out _))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(property => property.Name, property => ReadEnvelopeValue(property.Value));
        }

        private static Dictionary<string, JsonElement> ProjectFacts(SnapshotEnvelope snapshot, IReadOnlyDictionary<string, string> aliases)
        {
            var facts = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var pair in aliases.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (TryReadValue(snapshot, pair.Value, out var value))
                {
                    facts[pair.Key] = value;
                }
            }

            return facts;
        }

        private static PlannerFactRef ReadPlannerFact(SnapshotEnvelope snapshot, string path)
        {
            var envelope = ReadPath(snapshot, path);
            if (!envelope.HasValue || envelope.Value.ValueKind != JsonValueKind.Object)
            {
                return new PlannerFactRef
                {
                    Path = path,
                    Status = "missing",
                    Source = "missing"
                };
            }

            var status = envelope.Value.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "missing"
                : "missing";
            var confidence = envelope.Value.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDouble(out var parsedConfidence)
                ? parsedConfidence
                : 0;
            var readAtTick = envelope.Value.TryGetProperty("read_at_tick", out var tickElement) && tickElement.TryGetInt64(out var parsedTick)
                ? parsedTick
                : 0;
            var source = envelope.Value.TryGetProperty("source", out var sourceElement) &&
                sourceElement.ValueKind == JsonValueKind.Object &&
                sourceElement.TryGetProperty("path", out var sourcePath)
                    ? sourcePath.GetString() ?? "unknown"
                    : "unknown";

            return new PlannerFactRef
            {
                Path = path,
                Status = status,
                Confidence = confidence,
                ReadAtTick = readAtTick,
                Source = source
            };
        }

        private static T? ReadScalar<T>(SnapshotEnvelope snapshot, string path)
        {
            return TryReadValue(snapshot, path, out var value)
                ? value.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                : default;
        }

        private static bool TryReadValue(SnapshotEnvelope snapshot, string path, out JsonElement value)
        {
            var envelope = ReadPath(snapshot, path);
            if (envelope.HasValue && TryReadEnvelopeValue(envelope.Value, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadEnvelopeValue(JsonElement envelope, out JsonElement value)
        {
            if (envelope.ValueKind == JsonValueKind.Object &&
                envelope.TryGetProperty("status", out var status) &&
                FieldEnvelopeValidator.IsReadableStatus(status.GetString()) &&
                envelope.TryGetProperty("value", out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private static JsonElement ReadEnvelopeValue(JsonElement envelope)
        {
            return envelope.GetProperty("value");
        }

        private static JsonElement? ReadPath(SnapshotEnvelope snapshot, string path)
        {
            var parts = path.Split('.');
            if (parts.Length == 0 || !snapshot.State.TryGetValue(parts[0], out var current))
            {
                return null;
            }

            for (var i = 1; i < parts.Length; i++)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(parts[i], out current))
                {
                    return null;
                }
            }

            return current;
        }
    }
}
