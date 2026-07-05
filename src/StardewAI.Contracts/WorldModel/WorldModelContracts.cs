using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.WorldModel
{
    public sealed class WorldModelEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "world_model.v1";

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("snapshot_schema_version")]
        public string SnapshotSchemaVersion { get; set; } = string.Empty;

        [JsonPropertyName("game_tick")]
        public long GameTick { get; set; }

        [JsonPropertyName("in_game_time")]
        public int? InGameTime { get; set; }

        [JsonPropertyName("real_timestamp")]
        public string RealTimestamp { get; set; } = string.Empty;

        [JsonPropertyName("user_goal")]
        public string UserGoal { get; set; } = string.Empty;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "relaxed";

        [JsonPropertyName("completeness")]
        public WorldModelCompleteness Completeness { get; set; } = new();

        [JsonPropertyName("facts")]
        public WorldModelFacts Facts { get; set; } = new();

        [JsonPropertyName("planner_inputs")]
        public PlannerInputSummary PlannerInputs { get; set; } = new();

        [JsonPropertyName("audit")]
        public WorldModelAudit Audit { get; set; } = new();
    }

    public sealed class WorldModelCompleteness
    {
        [JsonPropertyName("snapshot_completeness")]
        public string SnapshotCompleteness { get; set; } = string.Empty;

        [JsonPropertyName("unavailable_count")]
        public int UnavailableCount { get; set; }

        [JsonPropertyName("unavailable_fields")]
        public string[] UnavailableFields { get; set; } = Array.Empty<string>();

        [JsonPropertyName("required_fact_count")]
        public int RequiredFactCount { get; set; }

        [JsonPropertyName("readable_required_fact_count")]
        public int ReadableRequiredFactCount { get; set; }

        [JsonPropertyName("all_required_facts_readable")]
        public bool AllRequiredFactsReadable { get; set; }
    }

    public sealed class WorldModelFacts
    {
        [JsonPropertyName("game")]
        public Dictionary<string, JsonElement> Game { get; set; } = new();

        [JsonPropertyName("player")]
        public Dictionary<string, JsonElement> Player { get; set; } = new();

        [JsonPropertyName("farm")]
        public Dictionary<string, JsonElement> Farm { get; set; } = new();

        [JsonPropertyName("current_location")]
        public Dictionary<string, JsonElement> CurrentLocation { get; set; } = new();

        [JsonPropertyName("npcs")]
        public Dictionary<string, JsonElement> Npcs { get; set; } = new();

        [JsonPropertyName("quests")]
        public Dictionary<string, JsonElement> Quests { get; set; } = new();

        [JsonPropertyName("world_progress")]
        public Dictionary<string, JsonElement> WorldProgress { get; set; } = new();

        [JsonPropertyName("menus")]
        public Dictionary<string, JsonElement> Menus { get; set; } = new();

        [JsonPropertyName("mods")]
        public Dictionary<string, JsonElement> Mods { get; set; } = new();

        [JsonPropertyName("modded_state")]
        public Dictionary<string, JsonElement> ModdedState { get; set; } = new();
    }

    public sealed class PlannerInputSummary
    {
        [JsonPropertyName("goal")]
        public string Goal { get; set; } = string.Empty;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "relaxed";

        [JsonPropertyName("required_facts")]
        public PlannerFactRef[] RequiredFacts { get; set; } = Array.Empty<PlannerFactRef>();

        [JsonPropertyName("blocked")]
        public bool Blocked { get; set; }

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class PlannerFactRef
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("read_at_tick")]
        public long ReadAtTick { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    public sealed class WorldModelAudit
    {
        [JsonPropertyName("projector")]
        public string Projector { get; set; } = "StardewAI.Core.WorldModel.WorldModelProjector";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "transparent_snapshot_projection_only";

        [JsonPropertyName("notes")]
        public string[] Notes { get; set; } = Array.Empty<string>();
    }
}
