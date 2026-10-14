namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodFrontierBuilder
{
    private const string ProducerId =
        "StardewAI.GoalConditionedBootstrap.GoalMethodFrontierBuilder";
    private const string ProducerVersion = "v4";

    public static GoalMethodFrontierReport Build(
        string expansionPath,
        string dependencyExpansionPath,
        string isolatedTrainingAuthorizationPath,
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string acquisitionLoweringCatalogPath,
        string knowledgePath,
        string optionMatrixPath,
        string claimLedgerPath,
        string directionCatalogSourcePath)
    {
        var requirementInventoryFullPath = Path.GetFullPath(
            requirementInventoryPath);
        var requirementInventory = ReadRequirementInventory(
            requirementInventoryFullPath);
        if (requirementInventory.SchemaVersion !=
                "authoritative_goal_requirement_inventory.v1" ||
            requirementInventory.SourceEvidence is null ||
            requirementInventory.SourceEvidence.Length == 0)
        {
            throw new InvalidDataException(
                "Requirement inventory cannot provide transitive frontier identity.");
        }
        var identity = InvocationDeterministicBuildSession.Describe(
                new GoalMethodFrontierBuildInputs
                {
                    ExpansionPath = expansionPath,
                    DependencyExpansionPath = dependencyExpansionPath,
                    IsolatedTrainingAuthorizationPath =
                        isolatedTrainingAuthorizationPath,
                    RequirementInventoryPath = requirementInventoryFullPath,
                    AcquisitionLoweringPath = acquisitionLoweringPath,
                    AcquisitionLoweringCatalogPath =
                        acquisitionLoweringCatalogPath,
                    KnowledgePath = knowledgePath,
                    OptionMatrixPath = optionMatrixPath,
                    ClaimLedgerPath = claimLedgerPath,
                    DirectionCatalogSourcePath = directionCatalogSourcePath
                })
            .ToList();
        identity.AddRange(requirementInventory.SourceEvidence
            .OrderBy(source => source.SourceId, StringComparer.Ordinal)
            .Select(source => InvocationDeterministicBuildSession
                .VerifiedTransitiveFile(
                    "RequirementSource:" + source.SourceId,
                    source.Path,
                    source.Sha256)));

        return InvocationDeterministicBuildSession.GetOrBuild(
            ProducerId,
            ProducerVersion,
            identity,
            () => BuildCore(
                expansionPath,
                dependencyExpansionPath,
                isolatedTrainingAuthorizationPath,
                requirementInventoryFullPath,
                acquisitionLoweringPath,
                acquisitionLoweringCatalogPath,
                knowledgePath,
                optionMatrixPath,
                claimLedgerPath,
                directionCatalogSourcePath,
                requirementInventory),
            artifactKind: "goal_method_frontier");
    }

    private static AuthoritativeRequirementInventoryReport
        ReadRequirementInventory(string path) =>
            CurrentTeacherFrontierSupport.Read<
                AuthoritativeRequirementInventoryReport>(
                path,
                "Authoritative requirement inventory");
}
