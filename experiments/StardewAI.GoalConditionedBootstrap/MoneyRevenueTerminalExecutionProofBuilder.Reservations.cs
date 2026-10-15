using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueTerminalExecutionProofBuilder
{
    private static TerminalReservationVerification
        VerifyStrategyLedgerAndReservation(
            MoneyRevenueCounterfactualProofRequest request,
            MoneyRevenueTerminalAlternativeProof proof,
            SnapshotEnvelope terminalStart,
            SnapshotEnvelope terminalActionBefore,
            string terminalActionBeforePath,
            SnapshotEnvelope terminalActionAfter,
            string terminalActionAfterPath,
            string runId)
    {
        var beforeCapturePath = RequiredFile(
            proof.BeforeStrategyLedgerCapturePath,
            "Money Revenue terminal before strategy-ledger capture");
        var afterCapturePath = RequiredFile(
            proof.AfterStrategyLedgerCapturePath,
            "Money Revenue terminal after strategy-ledger capture");
        Require(!string.Equals(
                    beforeCapturePath,
                    afterCapturePath,
                    StringComparison.OrdinalIgnoreCase),
            "Money Revenue terminal strategy-ledger captures are not independent artifacts.");
        var beforeCapture = VerifyBoundaryCapture(
            beforeCapturePath,
            StrategyLedgerBoundaryKinds.BeforeTerminalAction,
            request,
            runId,
            terminalActionBefore,
            terminalActionBeforePath);
        var afterCapture = VerifyBoundaryCapture(
            afterCapturePath,
            StrategyLedgerBoundaryKinds.AfterTerminalAction,
            request,
            runId,
            terminalActionAfter,
            terminalActionAfterPath);
        Require(beforeCapture.CaptureId != afterCapture.CaptureId,
            "Money Revenue terminal strategy-ledger capture identities were reused across boundaries.");

        var beforePath = Path.GetFullPath(beforeCapture.StrategyLedgerPath);
        var afterPath = Path.GetFullPath(afterCapture.StrategyLedgerPath);
        Require(string.Equals(
                    beforePath,
                    Path.GetFullPath(request.TerminalStrategyLedgerPath),
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    beforePath,
                    afterPath,
                    StringComparison.OrdinalIgnoreCase),
            "Money Revenue terminal strategy-ledger paths are not independently anchored.");

        var beforeSha256 = Hash(beforePath);
        var afterSha256 = Hash(afterPath);
        Require(beforeSha256 == request.TerminalStrategyLedgerSha256 &&
                afterSha256 == beforeSha256 &&
                string.Equals(
                    File.ReadAllText(beforePath),
                    File.ReadAllText(afterPath),
                    StringComparison.Ordinal),
            "Money Revenue terminal strategy ledger changed during execution.");

        var beforeState = AcquisitionStrategyLedgerReader.Read(
            beforePath,
            terminalStart);
        var afterState = AcquisitionStrategyLedgerReader.Read(
            afterPath,
            terminalActionAfter);
        var ledger = beforeState.Ledger;
        Require(EqualJson(ledger, afterState.Ledger) &&
                beforeState.ActorPlayerId == afterState.ActorPlayerId &&
                ledger.LedgerId == request.TerminalStrategyLedgerId &&
                ledger.Revision == request.TerminalStrategyLedgerRevision &&
                ledger.SourceStateHash ==
                    request.TerminalStrategyLedgerSourceStateHash &&
                ledger.SourceStateHash == terminalStart.StateHash,
            "Money Revenue terminal strategy-ledger identity drifted.");

        var resources = AcquisitionResourceInputSnapshotState.Read(
            JsonSerializer.SerializeToElement(
                terminalActionBefore,
                JsonDefaults.Options));
        Require(resources.MaterialEvidenceAvailable &&
                resources.MaterialGraph is not null &&
                resources.MaterialGraph.PlayerId == beforeState.ActorPlayerId,
            "Money Revenue terminal material graph is unavailable or belongs to another actor.");
        var graph = resources.MaterialGraph ??
            throw new InvalidDataException(
                "Money Revenue terminal material graph is missing.");
        var candidate = request.TerminalCandidate;
        Require(candidate.SlotIndex.HasValue && candidate.Quantity > 0,
            "Money Revenue terminal candidate has no exact inventory slot or quantity.");
        var slotIndex = candidate.SlotIndex ??
            throw new InvalidDataException(
                "Money Revenue terminal candidate inventory slot is missing.");
        var matching = graph.InventoryNodes
            .Where(node =>
                node.InventoryKind == "player_inventory" &&
                node.OwnerPlayerId == beforeState.ActorPlayerId &&
                node.SupplyState == "available" &&
                node.ActorUseAuthorized)
            .SelectMany(node => node.Slots
                .Where(slot =>
                    slot.SlotIndex == slotIndex &&
                    slot.QualifiedItemId == candidate.QualifiedItemId)
                .Select(slot => new { Node = node, Slot = slot }))
            .ToArray();
        Require(matching.Length == 1,
            "Money Revenue terminal target inventory slot is not unique and actor-owned.");

        var activeReservations = ledger.MaterialReservations
            .Where(row => row.Status ==
                StardewAI.Contracts.Strategy.StrategyCommitmentStatuses.Active)
            .ToArray();
        var projection = new MaterialSupplyProjection().Project(
            graph,
            activeReservations);
        Require(projection.Status == "available" &&
                projection.BlockingReasons.Length == 0,
            "Money Revenue terminal material reservations are invalid: " +
            string.Join(",", projection.BlockingReasons));
        var target = matching[0];
        var projected = projection.Slots.Where(row =>
                row.NodeId == target.Node.NodeId &&
                row.SlotIndex == target.Slot.SlotIndex &&
                row.QualifiedItemId == target.Slot.QualifiedItemId)
            .ToArray();
        Require(projected.Length == 1 &&
                projected[0].Stack == target.Slot.Stack &&
                target.Slot.SalePrice == candidate.UnitPrice &&
                target.Slot.Stack >= candidate.Quantity &&
                projected[0].AvailableQuantity >= candidate.Quantity,
            "Money Revenue terminal target quantity is reserved or unavailable.");
        VerifyTargetInventoryConsumption(
            beforeState.ActorPlayerId,
            target.Node.NodeId,
            target.Slot,
            candidate.Quantity,
            terminalActionAfter);

        return new TerminalReservationVerification(
            runId,
            beforeCapture.CaptureId,
            Hash(beforeCapturePath),
            afterCapture.CaptureId,
            Hash(afterCapturePath),
            beforeSha256,
            afterSha256,
            ledger.LedgerId,
            ledger.Revision,
            target.Node.NodeId,
            target.Slot.SlotIndex,
            target.Slot.Stack,
            projected[0].ReservedQuantity,
            projected[0].AvailableQuantity,
            beforePath,
            afterPath);
    }

    private static StrategyLedgerBoundaryCapture VerifyBoundaryCapture(
        string capturePath,
        string expectedBoundaryKind,
        MoneyRevenueCounterfactualProofRequest request,
        string runId,
        SnapshotEnvelope expectedSnapshot,
        string expectedSnapshotPath)
    {
        var capture = CurrentTeacherFrontierSupport.Read<
            StrategyLedgerBoundaryCapture>(
            capturePath,
            "Money Revenue strategy-ledger boundary capture");
        var expected = StrategyLedgerBoundaryCaptureBuilder.Build(
            expectedBoundaryKind,
            request.RequestId,
            request.ConcreteAlternativeId,
            runId,
            expectedSnapshotPath,
            capture.StrategyLedgerPath);
        Require(EqualJson(capture, expected) &&
                !capture.FormalProductTrainingAuthorized &&
                capture.SnapshotSha256 == Hash(expectedSnapshotPath) &&
                capture.StateHash == expectedSnapshot.StateHash &&
                capture.GameTick == expectedSnapshot.GameTick,
            "Money Revenue strategy-ledger boundary capture identity is invalid.");
        return capture;
    }

    private static void VerifyTargetInventoryConsumption(
        long actorPlayerId,
        string targetNodeId,
        MaterialInventorySlot targetSlot,
        int quantity,
        SnapshotEnvelope after)
    {
        var resources = AcquisitionResourceInputSnapshotState.Read(
            JsonSerializer.SerializeToElement(after, JsonDefaults.Options));
        Require(resources.MaterialEvidenceAvailable &&
                resources.MaterialGraph is not null &&
                resources.MaterialGraph.PlayerId == actorPlayerId,
            "Money Revenue terminal after-inventory graph is unavailable.");
        var graph = resources.MaterialGraph ??
            throw new InvalidDataException(
                "Money Revenue terminal after-inventory graph is missing.");
        var nodes = graph.InventoryNodes.Where(node =>
                node.NodeId == targetNodeId &&
                node.InventoryKind == "player_inventory" &&
                node.OwnerPlayerId == actorPlayerId &&
                node.SupplyState == "available" &&
                node.ActorUseAuthorized)
            .ToArray();
        Require(nodes.Length == 1,
            "Money Revenue terminal after-inventory node drifted.");
        var slots = nodes[0].Slots.Where(slot =>
                slot.SlotIndex == targetSlot.SlotIndex)
            .ToArray();
        var expectedStack = checked(targetSlot.Stack - quantity);
        Require(expectedStack == 0
                ? slots.Length == 0
                : slots.Length == 1 &&
                  slots[0].QualifiedItemId == targetSlot.QualifiedItemId &&
                  slots[0].Quality == targetSlot.Quality &&
                  slots[0].Stack == expectedStack,
            "Money Revenue terminal target inventory delta is not exact.");
    }

    private sealed record TerminalReservationVerification(
        string RunId,
        string BeforeCaptureId,
        string BeforeCaptureSha256,
        string AfterCaptureId,
        string AfterCaptureSha256,
        string BeforeLedgerSha256,
        string AfterLedgerSha256,
        string LedgerId,
        int LedgerRevision,
        string TargetNodeId,
        int TargetSlotIndex,
        int TargetStack,
        int TargetReservedQuantity,
        int TargetAvailableQuantity,
        string BeforeLedgerPath,
        string AfterLedgerPath);
}
