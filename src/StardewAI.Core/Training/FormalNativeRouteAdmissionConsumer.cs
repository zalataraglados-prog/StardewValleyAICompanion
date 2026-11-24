using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Training;

public static class FormalNativeRouteAdmissionConsumer
{
    public const string ProofReferenceSchemaVersion = "formal_native_route_proof_reference.v1";
    public const string FunctionalProofReferenceSchemaVersion = "functional_native_route_proof_reference.v1";

    public static IReadOnlyList<string> BlockerIds { get; } = Array.AsReadOnly(new[]
    {
        "golden_walnut_master_angler_native_admission_not_bound",
        "gourmand_orders_2_3_native_admission_not_bound"
    });

    public static FormalNativeRouteAssessment Evaluate(FormalNativeRouteProofReference? proof)
    {
        if (proof is null)
            return Blocked(proof, "selected_route_unavailable", "No frozen selected-route proof was supplied.");
        if (proof.SchemaVersion == FunctionalProofReferenceSchemaVersion)
            return EvaluateFunctional(proof);
        if (proof.SchemaVersion != ProofReferenceSchemaVersion || proof.NativeEvidence is null ||
            string.IsNullOrWhiteSpace(proof.SelectedRoutePath) || !Path.IsPathFullyQualified(proof.SelectedRoutePath) ||
            proof.SelectedRouteSha256 is not { Length: 64 } || !proof.SelectedRouteSha256.All(Uri.IsHexDigit))
            return Blocked(proof, "selected_route_binding_invalid", "The selected-route reference is incomplete or unsupported.");

        try
        {
            VerifyRouteBytes(proof);
            _ = GoldenWalnutNativeEvidenceReader.Verify(proof.NativeEvidence);
            VerifyRouteBytes(proof);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or
            ArgumentException or InvalidOperationException or KeyNotFoundException or NotSupportedException)
        {
            return Blocked(proof, "native_source_replay_rejected", ex.Message);
        }

        // Byte binding and native source replay do not establish that this was the selected route,
        // or prove its wallet, fish access, deadlines or Gourmand branch. Keep both locks closed.
        return Blocked(proof, "selected_route_reconciliation_pending",
            "Native sources replayed; selected-route and resource reconciliation are not accepted.", sourceReplayVerified: true);
    }

    private static FormalNativeRouteAssessment EvaluateFunctional(FormalNativeRouteProofReference proof)
    {
        try
        {
            if (proof.NativeEvidence is not null) throw new InvalidDataException("Functional evidence must not masquerade as a fresh-save acquisition binding.");
            FunctionalRouteReplayer.VerifyArtifact(new(proof.SelectedRoutePath, proof.SelectedRouteSha256));
            var count = FunctionalRouteReplayer.Verify(proof.SelectedRoutePath);
            VerifyRouteBytes(proof);
            return new(proof, true,
                $"{count} strict native functional frames and the original joint day plan verified. Sample years are retained. Formal admission still requires the target route's resource, acquisition-order and deadline reconciliation; no same-save long run is required by this functional result.",
                BlockerIds.Select(id => new FormalNativeRouteGate(id, "blocked", "functional_verified_target_plan_not_bound")).ToArray())
                { ProofScope = "functional_existing_access" };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or
            ArgumentException or InvalidOperationException or KeyNotFoundException or NotSupportedException)
        {
            return Blocked(proof, "functional_route_replay_rejected", ex.Message) with { ProofScope = "functional_existing_access" };
        }
    }

    private static void VerifyRouteBytes(FormalNativeRouteProofReference proof)
    {
        if ((File.GetAttributes(proof.SelectedRoutePath) & FileAttributes.ReparsePoint) != 0 ||
            !string.Equals(StructuredPolicyCheckpointStore.HashFile(proof.SelectedRoutePath),
                proof.SelectedRouteSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Selected-route artifact hash or identity mismatch.");
    }

    private static FormalNativeRouteAssessment Blocked(FormalNativeRouteProofReference? proof, string reason,
        string detail, bool sourceReplayVerified = false) =>
        new(proof, sourceReplayVerified, detail,
            BlockerIds.Select(id => new FormalNativeRouteGate(id, "blocked", reason)).ToArray());
}
