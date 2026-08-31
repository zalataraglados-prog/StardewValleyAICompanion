using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveStoryEvent
    {
        public ActiveStoryEvent(PendingExecution pending, Event nativeEvent)
        {
            Pending = pending;
            NativeEvent = nativeEvent;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
            InitialCommandIndex = nativeEvent.CurrentCommand;
            LastCommandIndex = nativeEvent.CurrentCommand;
            InitialLocationId = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
            InitialEventSeen = Game1.player.eventsSeen?.Contains(nativeEvent.id) == true;
        }

        public PendingExecution Pending { get; }
        public Event NativeEvent { get; }
        public string StartedAt { get; }
        public int InitialCommandIndex { get; }
        public int LastCommandIndex { get; set; }
        public string InitialLocationId { get; }
        public bool InitialEventSeen { get; }
        public int ElapsedTicks { get; set; }
        public int StalledTicks { get; set; }
        public int DialogueClicks { get; set; }
        public int MenuActions { get; set; }
        public bool ProgressObserved { get; set; }
        public bool BoundResponseConsumed { get; set; }
        public string LastMenuType { get; set; } = string.Empty;
        public string LastQuestionKey { get; set; } = string.Empty;
    }
}
