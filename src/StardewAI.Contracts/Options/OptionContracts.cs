using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;

namespace StardewAI.Contracts.Options
{
    public static class OptionBehaviorCategories
    {
        public const string Mechanical = "mechanical";
        public const string ParameterizedMechanical = "parameterized_mechanical";
        public const string SpatialPlanning = "spatial_planning";
        public const string EconomicStrategic = "economic_strategic";
        public const string SocialStrategic = "social_strategic";
        public const string ExplorationUncertain = "exploration_uncertain";
        public const string LongTermStrategic = "long_term_strategic";
        public const string Recovery = "recovery";
        public const string Unknown = "unknown";
    }

    public static class CompilerResponsibilities
    {
        public const string FullActionExpansion = "full_action_expansion";
        public const string ParameterExpansion = "parameter_expansion";
        public const string PlanValidation = "plan_validation";
        public const string StrategySelectionOnly = "strategy_selection_only";
        public const string Unsupported = "unsupported";
        public const string Unknown = "unknown";
    }

    public static class TrainingRoles
    {
        public const string ExecutorCalibration = "executor_calibration";
        public const string StrategyValue = "strategy_value";
        public const string Mixed = "mixed";
        public const string Unknown = "unknown";
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionSemanticKind
    {
        Unknown,
        GoalTemplate,
        CompositeOptionSpec,
        PrimitiveOptionSpec,
        ExecutionStepSpec,
        PlannerInternalSpec,
        AdapterCapabilitySpec
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ParameterSchemaPolicy
    {
        Unknown,
        GoalParameters,
        CandidateBoundParameters,
        PrimitiveActionParameters,
        NoParameters
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequiredFactPolicy
    {
        Unknown,
        AllRequiredFailClosed
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionRiskClass
    {
        Unknown,
        R0PureRecovery,
        R1ReversibleInteraction,
        R2Consumptive,
        R3CrossDayCommitment,
        R4IrreversibleAssetChange,
        R5RelationshipOrRouteChoice
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionIrreversibility
    {
        Unknown,
        None,
        Consumptive,
        CrossDayCommitment,
        IrreversibleAssetChange,
        RelationshipOrRouteChoice
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionConfirmationPolicy
    {
        Unknown,
        NotRequired,
        PolicyAuthorizationRequired,
        ExplicitUserConfirmationRequired
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionHostPolicy
    {
        Unknown,
        ControllingActorAllowed,
        HostOnly
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionOwnershipPolicy
    {
        Unknown,
        ActorState,
        ActorInventory,
        SharedFarmState,
        SharedWorldState,
        Mixed
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionModAdapterPolicy
    {
        Unknown,
        VanillaNativeOnly,
        ExplicitVerifiedAdapterOnly
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionRuntimeStatus
    {
        Unknown,
        RegisteredOnly,
        OfflineVerified,
        RuntimeVerified,
        LongDurationVerified
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionTrainingEligibility
    {
        Unknown,
        BlockedPendingRuntimeEvidence,
        EvaluationOnly,
        Eligible
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AutonomousCandidatePolicy
    {
        Unknown,
        Allowed,
        PolicyAuthorizationRequired,
        ExplicitUserConfirmationRequired,
        Forbidden
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OptionProductStatus
    {
        Unknown,
        Registered,
        InternalPreview,
        ProductReady
    }

    public sealed class OptionSpec
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "option_spec.v2";

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("behavior_category")]
        public string BehaviorCategory { get; set; } = OptionBehaviorCategories.Unknown;

        [JsonPropertyName("compiler_responsibility")]
        public string CompilerResponsibility { get; set; } = CompilerResponsibilities.Unknown;

        [JsonPropertyName("training_role")]
        public string TrainingRole { get; set; } = TrainingRoles.Unknown;

        [JsonPropertyName("semantic_kind")]
        public OptionSemanticKind SemanticKind { get; set; } = OptionSemanticKind.Unknown;

        [JsonPropertyName("parameter_schema")]
        public ParameterSchemaPolicy ParameterSchema { get; set; } = ParameterSchemaPolicy.Unknown;

        [JsonPropertyName("required_fact_policy")]
        public RequiredFactPolicy RequiredFactPolicy { get; set; } = RequiredFactPolicy.Unknown;

        [JsonPropertyName("required_state_factors")]
        public string[] RequiredStateFactors { get; set; } = new string[0];

        [JsonPropertyName("estimated_effects")]
        public string[] EstimatedEffects { get; set; } = new string[0];

        [JsonPropertyName("irreversible_effects")]
        public string[] IrreversibleEffects { get; set; } = new string[0];

        [JsonPropertyName("safety_constraints")]
        public string[] SafetyConstraints { get; set; } = new string[0];

        [JsonPropertyName("recoverability")]
        public string Recoverability { get; set; } = "unknown";

        [JsonPropertyName("risk_level")]
        public string RiskLevel { get; set; } = "unknown";

        [JsonPropertyName("risk_class")]
        public OptionRiskClass RiskClass { get; set; } = OptionRiskClass.Unknown;

        [JsonPropertyName("irreversibility")]
        public OptionIrreversibility Irreversibility { get; set; } = OptionIrreversibility.Unknown;

        [JsonPropertyName("confirmation_policy")]
        public OptionConfirmationPolicy ConfirmationPolicy { get; set; } = OptionConfirmationPolicy.Unknown;

        [JsonPropertyName("host_policy")]
        public OptionHostPolicy HostPolicy { get; set; } = OptionHostPolicy.Unknown;

        [JsonPropertyName("ownership_policy")]
        public OptionOwnershipPolicy OwnershipPolicy { get; set; } = OptionOwnershipPolicy.Unknown;

        [JsonPropertyName("mod_adapter_policy")]
        public OptionModAdapterPolicy ModAdapterPolicy { get; set; } = OptionModAdapterPolicy.Unknown;

        [JsonPropertyName("compiler_binding")]
        public string CompilerBinding { get; set; } = string.Empty;

        [JsonPropertyName("before_verifier_binding")]
        public string BeforeVerifierBinding { get; set; } = string.Empty;

        [JsonPropertyName("after_verifier_binding")]
        public string AfterVerifierBinding { get; set; } = string.Empty;

        [JsonPropertyName("runtime_evidence_id")]
        public string RuntimeEvidenceId { get; set; } = string.Empty;

        [JsonPropertyName("runtime_status")]
        public OptionRuntimeStatus RuntimeStatus { get; set; } = OptionRuntimeStatus.Unknown;

        [JsonPropertyName("training_eligibility")]
        public OptionTrainingEligibility TrainingEligibility { get; set; } = OptionTrainingEligibility.Unknown;

        [JsonPropertyName("autonomous_candidate_policy")]
        public AutonomousCandidatePolicy AutonomousCandidatePolicy { get; set; } = AutonomousCandidatePolicy.Unknown;

        [JsonPropertyName("product_status")]
        public OptionProductStatus ProductStatus { get; set; } = OptionProductStatus.Unknown;
    }

    public sealed class OptionInstance
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "option_instance.v1";

        [JsonPropertyName("instance_id")]
        public string InstanceId { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("bound_goal_id")]
        public string BoundGoalId { get; set; } = string.Empty;

        [JsonPropertyName("bound_parameters")]
        public object BoundParameters { get; set; } = new object();
    }

    public sealed class OptionAvailabilityRequest
    {
        [JsonPropertyName("state_hash")]
        public string? StateHash { get; set; }

        [JsonPropertyName("candidate_option_ids")]
        public string[] CandidateOptionIds { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("candidates")]
        public OptionAvailabilityCandidate[] Candidates { get; set; } = System.Array.Empty<OptionAvailabilityCandidate>();

        [JsonPropertyName("include_executor_calibration_options")]
        public bool IncludeExecutorCalibrationOptions { get; set; }
    }

    public sealed class OptionAvailabilityCandidate
    {
        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public SmallModelActionParameter[] Parameters { get; set; } = System.Array.Empty<SmallModelActionParameter>();
    }

    public sealed class OptionAvailabilityEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "option_availability.v1";

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("current_time")]
        public int CurrentTime { get; set; }

        [JsonPropertyName("availability_scope")]
        public string AvailabilityScope { get; set; } = "field_availability_and_executor_gate";

        [JsonPropertyName("options")]
        public OptionAvailability[] Options { get; set; } = System.Array.Empty<OptionAvailability>();
    }

    public sealed class OptionAvailability
    {
        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "blocked";

        [JsonPropertyName("preview_only")]
        public bool PreviewOnly { get; set; }

        [JsonPropertyName("executor_enabled")]
        public bool ExecutorEnabled { get; set; }

        [JsonPropertyName("training_role")]
        public string TrainingRole { get; set; } = TrainingRoles.Unknown;

        [JsonPropertyName("behavior_category")]
        public string BehaviorCategory { get; set; } = OptionBehaviorCategories.Unknown;

        [JsonPropertyName("compiler_responsibility")]
        public string CompilerResponsibility { get; set; } = CompilerResponsibilities.Unknown;

        [JsonPropertyName("required_state_factors")]
        public string[] RequiredStateFactors { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("parameters")]
        public SmallModelActionParameter[] Parameters { get; set; } = System.Array.Empty<SmallModelActionParameter>();

        [JsonPropertyName("missing_state_factors")]
        public string[] MissingStateFactors { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("blocking_reasons")]
        public string[] BlockingReasons { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("hard_block_reasons")]
        public string[] HardBlockReasons { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("precondition_results")]
        public PreconditionResult[] PreconditionResults { get; set; } = System.Array.Empty<PreconditionResult>();

        [JsonPropertyName("availability_notes")]
        public string[] AvailabilityNotes { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("economic_candidates")]
        public EconomicCandidate[] EconomicCandidates { get; set; } = System.Array.Empty<EconomicCandidate>();

        [JsonPropertyName("event_candidates")]
        public EventCandidate[] EventCandidates { get; set; } = System.Array.Empty<EventCandidate>();

        [JsonPropertyName("social_candidates")]
        public EventCandidate[] SocialCandidates { get; set; } = System.Array.Empty<EventCandidate>();
    }

    public sealed class EventCandidate
    {
        [JsonPropertyName("candidate_id")]
        public string CandidateId { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("location_id")]
        public string LocationId { get; set; } = string.Empty;

        [JsonPropertyName("tile_x")]
        public int? TileX { get; set; }

        [JsonPropertyName("tile_y")]
        public int? TileY { get; set; }

        [JsonPropertyName("expected_effect")]
        public string ExpectedEffect { get; set; } = string.Empty;

        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("slot_index")]
        public int? SlotIndex { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("shop_id")]
        public string ShopId { get; set; } = string.Empty;

        [JsonPropertyName("estimated_ticks")]
        public int EstimatedTicks { get; set; }

        [JsonPropertyName("energy_cost")]
        public int EnergyCost { get; set; }

        [JsonPropertyName("availability_class")]
        public string AvailabilityClass { get; set; } = string.Empty;

        [JsonPropertyName("allowed_now")]
        public bool? AllowedNow { get; set; }

        [JsonPropertyName("allowed_today")]
        public bool? AllowedToday { get; set; }

        [JsonPropertyName("next_open_time")]
        public int? NextOpenTime { get; set; }

        [JsonPropertyName("effective_open_time")]
        public int? EffectiveOpenTime { get; set; }

        [JsonPropertyName("closes_at")]
        public int? ClosesAt { get; set; }

        [JsonPropertyName("wait_cost")]
        public int? WaitCost { get; set; }

        [JsonPropertyName("gate_reasons")]
        public string[] GateReasons { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("parameters")]
        public SmallModelActionParameter[] Parameters { get; set; } = System.Array.Empty<SmallModelActionParameter>();

        [JsonPropertyName("full_shipment_known")]
        public bool? FullShipmentKnown { get; set; }

        [JsonPropertyName("full_shipment_eligible")]
        public bool? FullShipmentEligible { get; set; }

        [JsonPropertyName("full_shipment_current_shipped_count")]
        public int? FullShipmentCurrentShippedCount { get; set; }

        [JsonPropertyName("full_shipment_already_shipped")]
        public bool? FullShipmentAlreadyShipped { get; set; }

        [JsonPropertyName("full_shipment_contributes")]
        public bool? FullShipmentContributes { get; set; }

        [JsonPropertyName("available_stack")]
        public int? AvailableStack { get; set; }
    }

    public sealed class EconomicCandidate
    {
        [JsonPropertyName("candidate_id")]
        public string CandidateId { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shop_id")]
        public string ShopId { get; set; } = string.Empty;

        [JsonPropertyName("slot_index")]
        public int? SlotIndex { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("unit_price")]
        public int UnitPrice { get; set; }

        [JsonPropertyName("total_value")]
        public int TotalValue { get; set; }

        [JsonPropertyName("currency_balance")]
        public int? CurrencyBalance { get; set; }

        [JsonPropertyName("stock")]
        public int? Stock { get; set; }

        [JsonPropertyName("infinite_stock")]
        public bool InfiniteStock { get; set; }

        [JsonPropertyName("can_ship")]
        public bool CanShip { get; set; }

        [JsonPropertyName("can_shop_sell")]
        public bool CanShopSell { get; set; }

        [JsonPropertyName("full_shipment_known")]
        public bool? FullShipmentKnown { get; set; }

        [JsonPropertyName("full_shipment_eligible")]
        public bool? FullShipmentEligible { get; set; }

        [JsonPropertyName("full_shipment_current_shipped_count")]
        public int? FullShipmentCurrentShippedCount { get; set; }

        [JsonPropertyName("full_shipment_already_shipped")]
        public bool? FullShipmentAlreadyShipped { get; set; }

        [JsonPropertyName("full_shipment_contributes")]
        public bool? FullShipmentContributes { get; set; }

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = System.Array.Empty<string>();
    }
}
