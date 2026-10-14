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
        ValidateManifest(manifest);

        var baseRequestPath = RequiredFile(
            manifest.BaseCoverageRequestPath,
            "Base goal-method Teacher coverage request");
        var corpusPath = RequiredFile(
            manifest.MoneyThresholdCorpusPath,
            "Money threshold Teacher corpus");
        _ = MoneyThresholdTeacherCorpusBuilder.Verify(corpusPath);
        return Build(manifest, baseRequestPath, corpusPath);
    }

    internal static GoalMethodTeacherCoverageRequest BuildFromVerifiedCorpus(
        string manifestPath,
        string verifiedCorpusPath,
        MoneyThresholdTeacherCorpus verifiedCorpus)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money threshold coverage-request manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdCoverageRequestManifest>(
                fullManifestPath,
                "Money threshold coverage-request manifest");
        ValidateManifest(manifest);
        var baseRequestPath = RequiredFile(
            manifest.BaseCoverageRequestPath,
            "Base goal-method Teacher coverage request");
        var corpusPath = RequiredFile(
            verifiedCorpusPath,
            "Verified Money threshold Teacher corpus");
        if (!string.Equals(
                Path.GetFullPath(manifest.MoneyThresholdCorpusPath),
                corpusPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Verified Money corpus does not match the coverage manifest.");
        }
        MoneyThresholdTeacherCorpusBuilder.ValidateInMemory(verifiedCorpus);
        return Build(
            manifest,
            baseRequestPath,
            corpusPath);
    }

    private static GoalMethodTeacherCoverageRequest Build(
        MoneyThresholdCoverageRequestManifest manifest,
        string baseRequestPath,
        string corpusPath)
    {
        var baseRequest = CurrentTeacherFrontierSupport.Read<
            GoalMethodTeacherCoverageRequest>(
                baseRequestPath,
                "Base goal-method Teacher coverage request");
        GoalMethodTeacherCoverageBuilder.ValidateRequest(baseRequest);

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

    private static void ValidateManifest(
        MoneyThresholdCoverageRequestManifest manifest)
    {
        if (manifest.SchemaVersion !=
                MoneyThresholdCoverageRequestVersions.Manifest ||
            string.IsNullOrWhiteSpace(manifest.SourceId) ||
            manifest.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "Money threshold coverage-request manifest is invalid.");
        }
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
