using System;
using System.Collections.Generic;
using System.IO;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public interface IPolicyTeacherSupervisionReplayRegistry
{
    IPolicyTeacherSupervisionReplayVerifier? Resolve(string teacherId, string teacherVersion);
}

public sealed class PolicyTeacherSupervisionReplayRegistry :
    IPolicyTeacherSupervisionReplayVerifier, IPolicyTeacherSupervisionReplayRegistry
{
    private readonly Dictionary<(string Id, string Version), IPolicyTeacherSupervisionReplayVerifier> providers = new();
    public string TeacherId => "deterministic_teacher.registered_sources";
    public string TeacherVersion => "v1";

    public PolicyTeacherSupervisionReplayRegistry(params IPolicyTeacherSupervisionReplayVerifier[] verifiers)
    {
        if (verifiers is null || verifiers.Length == 0)
            throw new ArgumentException("Teacher replay registry needs explicit providers.", nameof(verifiers));
        foreach (var verifier in verifiers)
        {
            if (verifier is null || verifier is IPolicyTeacherSupervisionReplayRegistry ||
                string.IsNullOrWhiteSpace(verifier.TeacherId) || string.IsNullOrWhiteSpace(verifier.TeacherVersion) ||
                !providers.TryAdd((verifier.TeacherId, verifier.TeacherVersion), verifier))
                throw new ArgumentException("Teacher replay providers must have unique exact identities and cannot nest registries.", nameof(verifiers));
        }
    }

    public IPolicyTeacherSupervisionReplayVerifier? Resolve(string teacherId, string teacherVersion) =>
        providers.TryGetValue((teacherId, teacherVersion), out var verifier) ? verifier : null;

    public PolicyDecisionTrajectoryEnvelope Rebuild(PolicyDecisionTrajectoryEnvelope claimed)
    {
        var claim = claimed.Audit?.TeacherSupervision;
        var verifier = claim is null ? null : Resolve(claim.TeacherId, claim.TeacherVersion);
        return verifier?.Rebuild(claimed) ?? throw new InvalidDataException("Independent Teacher provider is not registered.");
    }
}
