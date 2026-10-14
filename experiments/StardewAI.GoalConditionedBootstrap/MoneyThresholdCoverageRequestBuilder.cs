namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdCoverageRequestBuilder
{
    public static GoalMethodTeacherCoverageRequest Build(string manifestPath)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money threshold coverage-request manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdCoverageRequestManifest>(
                fullManifestPath,
                "Money threshold coverage-request manifest");
        if (manifest.SchemaVersion !=
                MoneyThresholdCoverageRequestVersions.Manifest ||
            string.IsNullOrWhiteSpace(manifest.SourceId) ||
            manifest.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "Money threshold coverage-request manifest is invalid.");
        }

        var baseRequestPath = RequiredFile(
            manifest.BaseCoverageRequestPath,
            "Base goal-method Teacher coverage request");
        var corpusPath = RequiredFile(
            manifest.MoneyThresholdCorpusPath,
            "Money threshold Teacher corpus");
        var baseRequest = CurrentTeacherFrontierSupport.Read<
            GoalMethodTeacherCoverageRequest>(
                baseRequestPath,
                "Base goal-method Teacher coverage request");
        GoalMethodTeacherCoverageBuilder.ValidateRequest(baseRequest);
        _ = MoneyThresholdTeacherCorpusBuilder.Verify(corpusPath);

        var retained = baseRequest.Sources
            .Where(source => source.SourceKind !=
                GoalMethodTeacherCoverageSourceKinds
                    .MoneyThresholdProgressionCorpus)
            .ToArray();
        if (retained.Any(source => source.SourceId == manifest.SourceId))
        {
            throw new InvalidDataException(
                "Money threshold coverage source ID collides with a non-Money source.");
        }

        var request = new GoalMethodTeacherCoverageRequest
        {
            CoverageId = baseRequest.CoverageId,
            Sources = retained
                .Append(new GoalMethodTeacherCoverageSource(
                    manifest.SourceId,
                    GoalMethodTeacherCoverageSourceKinds
                        .MoneyThresholdProgressionCorpus,
                    corpusPath))
                .OrderBy(source => source.SourceId, StringComparer.Ordinal)
                .ToArray(),
            FormalProductTrainingAuthorized = false
        };
        GoalMethodTeacherCoverageBuilder.ValidateRequest(request);
        return request;
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }
}
