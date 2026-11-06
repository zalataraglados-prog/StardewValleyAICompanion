using System.Text.Json;
using StardewAI.Contracts.Options;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CurrentMuseumCollectionItem(
    string RequirementId, string ItemId, string QualifiedItemId, bool Donated);

public sealed record CurrentMuseumCollectionIntake(
    string SnapshotPath, string SnapshotSha256, string SourceStateHash, long GameTick,
    string KnowledgePath, string KnowledgeSha256, string RequirementInventoryPath,
    string RequirementInventorySha256, string DependenciesPath, string DependenciesSha256,
    JsonElement OriginalDirection, CurrentMuseumCollectionItem[] Items,
    int DonatedCount, int MissingItemCount, bool CollectionComplete,
    bool NativeAchievementReceived, string[] ConsideredOptionIds,
    OptionAvailabilityEnvelope Availability)
{
    public string SchemaVersion => "current_museum_collection_intake.v1";
    public string Status => "ready_current_state_only";
    public string CandidateScope => "original_current_donation_sources_not_acquisition_or_teacher_labels";
    public bool CurrentTransitionSourcesReadable =>
        Availability.Options.Select(row => row.OptionId).SequenceEqual(ConsideredOptionIds) &&
        Availability.Options.All(row => row.ReadEligible);
    public bool LocalTerminalFactsSatisfied => CollectionComplete && NativeAchievementReceived;
    public bool ResourceReservationsVerified => false;
    public bool TrainingLabelEligible => false;
    public bool NativeOutcomeVerified => false;
    public bool FullRootVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}
