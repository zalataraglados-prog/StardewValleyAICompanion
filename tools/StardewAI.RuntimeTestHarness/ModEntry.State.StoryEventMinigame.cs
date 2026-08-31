using StardewValley;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveStoryEventMinigame
    {
        public ActiveStoryEventMinigame(PendingExecution pending, IMinigame minigame, Event? nativeEvent)
        {
            Pending = pending;
            NativeMinigame = minigame;
            NativeEvent = nativeEvent;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
            InitialCommandIndex = nativeEvent?.CurrentCommand ?? -1;
            LastCommandIndex = InitialCommandIndex;
            InitialLocationId = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        }

        public PendingExecution Pending { get; }
        public IMinigame NativeMinigame { get; }
        public Event? NativeEvent { get; }
        public string StartedAt { get; }
        public int InitialCommandIndex { get; }
        public int LastCommandIndex { get; set; }
        public string InitialLocationId { get; }
        public int ElapsedTicks { get; set; }
        public int DialogueClicks { get; set; }
        public bool ProgressObserved { get; set; }
        public bool BoundResponseConsumed { get; set; }
        public DialogueBox? BoundDialogue { get; set; }
        public bool MessageClickLogged { get; set; }
    }
}
