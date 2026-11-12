using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;

namespace StardewAI.LiveTrainingLoop;

public static class CropTeacherControlInterruptClassifier
{
    public static bool HasSafeSpeakerlessDialogue(JsonObject snapshot) =>
        SafeSpeakerlessDialogueRecovery.HasSafeSpeakerlessDialogue(snapshot);
}
