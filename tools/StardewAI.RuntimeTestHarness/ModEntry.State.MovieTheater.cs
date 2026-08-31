using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private enum MovieRuntimePhase
    {
        Move,
        WaitQuestion,
        WaitWarp,
        WaitConcessionShop,
        WaitConcessionReceipt,
        CloseInformationalDialogue,
        WaitScreeningStart,
        AdvanceScreening,
        VerifyScreening
    }

    private sealed class ActiveMovieTheater : INativeObjectInteractionMovement
    {
        public ActiveMovieTheater(
            PendingExecution pending,
            GameLocation location,
            MovieTheater theater,
            Point interaction,
            Point stand,
            List<Point> path,
            int maxMovementTiles,
            NPC? guest)
        {
            Pending = pending;
            Location = location;
            Theater = theater;
            Interaction = interaction;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            Guest = guest;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            TicketCountBefore = CountInventoryItems("(O)809");
            MoneyBefore = Game1.player.Money;
            PlayerLastSeenWeekBefore = Game1.player.lastSeenMovieWeek.Value;
            GuestLastSeenWeekBefore = guest?.lastSeenMovieWeek.Value;
            FriendshipBefore = guest is not null && Game1.player.friendshipData.TryGetValue(guest.Name, out var friendship)
                ? friendship.Points
                : null;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public MovieTheater Theater { get; }
        public NPC? Guest { get; }
        public Point Interaction { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => Pending.Request.MovieStage == "watch_movie_screening" ? 18000 : 2400;
        public string StartedAt { get; }
        public int TicketCountBefore { get; }
        public int MoneyBefore { get; }
        public int PlayerLastSeenWeekBefore { get; }
        public int? GuestLastSeenWeekBefore { get; }
        public int? FriendshipBefore { get; }
        public MovieRuntimePhase Phase { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int PhaseTicks { get; set; }
        public int InputCooldown { get; set; }
        public int ConcessionPrice { get; set; }
        public bool NativeCheckActionHandled { get; set; }
        public bool NativeReceiptObserved { get; set; }
        public bool SawScreeningEvent { get; set; }
        public bool SawRequestMovieEndReceipt { get; set; }
    }
}
