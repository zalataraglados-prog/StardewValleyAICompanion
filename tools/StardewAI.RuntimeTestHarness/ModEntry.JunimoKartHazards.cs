using System.Reflection;
using Microsoft.Xna.Framework;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly FieldInfo FallingBoulderCurrentSpeedField =
        typeof(MineCart.FallingBoulder).GetField(
            "_currentFallSpeed",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart.FallingBoulder).FullName, "_currentFallSpeed");
    private static readonly FieldInfo FallingBoulderFallSpeedField =
        typeof(MineCart.FallingBoulder).GetField(
            "_fallSpeed",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart.FallingBoulder).FullName, "_fallSpeed");
    private static readonly FieldInfo FallingBoulderTracksField =
        typeof(MineCart.FallingBoulder).GetField(
            "_tracks",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart.FallingBoulder).FullName, "_tracks");

    private static IReadOnlyList<MineCart.FallingBoulder> GetJunimoKartFallingBoulders(
        MineCart game) =>
        GetJunimoKartEntities(game)
            .Where(entity => entity.GetType() == typeof(MineCart.FallingBoulder))
            .Cast<MineCart.FallingBoulder>()
            .Where(boulder => boulder.enabled)
            .ToArray();

    private static IReadOnlyList<JunimoKartFallingBoulderProjection>
        GetJunimoKartFallingBoulderProjections(MineCart game)
    {
        var projections = new List<JunimoKartFallingBoulderProjection>();
        foreach (var boulder in GetJunimoKartFallingBoulders(game))
        {
            var bounds = boulder.GetBounds();
            var tracks = FallingBoulderTracksField.GetValue(boulder) as IEnumerable<MineCart.Track>;
            projections.Add(new JunimoKartFallingBoulderProjection(
                ActivationSeconds: 0f,
                X: boulder.position.X,
                Y: boulder.position.Y,
                CurrentFallSpeed: (float)FallingBoulderCurrentSpeedField.GetValue(boulder)!,
                FallSpeed: (float)FallingBoulderFallSpeedField.GetValue(boulder)!,
                LocalBoundsX: bounds.X - (int)boulder.position.X,
                LocalBoundsY: bounds.Y - (int)boulder.position.Y,
                Width: bounds.Width,
                Height: bounds.Height,
                RemainingTrackY: tracks?
                    .Where(track => track is not null)
                    .Select(track => (float)track.GetYAtPoint(boulder.position.X))
                    .ToArray() ?? Array.Empty<float>()));
        }

        foreach (var spawner in GetJunimoKartEntities(game)
                     .OfType<MineCart.FallingBoulderSpawner>()
                     .Where(candidate => candidate.enabled))
        {
            var tracks = game.GetTracksForXPosition(spawner.position.X);
            projections.Add(new JunimoKartFallingBoulderProjection(
                ActivationSeconds: Math.Max(0f, spawner.period - spawner.currentTime),
                X: spawner.position.X,
                Y: spawner.position.Y,
                CurrentFallSpeed: 0f,
                FallSpeed: 96f,
                LocalBoundsX: -4,
                LocalBoundsY: -12,
                Width: 8,
                Height: 12,
                RemainingTrackY: tracks?
                    .Where(track => track is not null)
                    .Select(track => (float)track.GetYAtPoint(spawner.position.X))
                    .ToArray() ?? Array.Empty<float>()));
        }

        return projections;
    }

    private static IEnumerable<MineCart.Entity> GetJunimoKartEntities(MineCart game) =>
        (IEnumerable<MineCart.Entity>)MineCartEntitiesField.GetValue(game)!;

    private static float FindJunimoKartFallingBoulderHazardDistance(
        MineCart game,
        MineCart.PlayerMineCartCharacter player,
        float lookahead,
        IReadOnlyList<JunimoKartFallingBoulderProjection> boulders)
    {
        var firstHazard = float.MaxValue;
        var playerBounds = player.GetBounds();
        var speed = Math.Max(1f, Math.Abs(player.velocity.X));
        foreach (var boulder in boulders)
        {
            var distance = boulder.X + boulder.LocalBoundsX - playerBounds.Right;
            if (distance < -boulder.Width || distance > lookahead)
            {
                continue;
            }

            var secondsUntilContact = Math.Max(0f, distance) / speed;
            if (!TryProjectJunimoKartFallingBoulder(
                    boulder,
                    secondsUntilContact,
                    out var projectedBounds))
            {
                continue;
            }

            var contactX = player.position.X + speed * secondsUntilContact;
            var contactTracks = game.GetTracksForXPosition(contactX);
            if (contactTracks is null || contactTracks.Count == 0)
            {
                continue;
            }

            var groundY = contactTracks
                .OrderBy(track => Math.Abs(track.GetYAtPoint(contactX) - player.position.Y))
                .First()
                .GetYAtPoint(contactX);
            var groundedPlayerBounds = new Rectangle((int)contactX - 4, groundY - 12, 8, 12);
            if (groundedPlayerBounds.Intersects(projectedBounds))
            {
                firstHazard = Math.Min(firstHazard, Math.Max(0f, distance));
            }
        }

        return firstHazard;
    }

    private static bool JunimoKartSimulatedFallingBoulderCollision(
        IReadOnlyList<JunimoKartFallingBoulderProjection> boulders,
        float elapsedSeconds,
        float playerX,
        float playerY)
    {
        var playerBounds = new Rectangle((int)playerX - 4, (int)playerY - 12, 8, 12);
        return boulders.Any(boulder =>
            TryProjectJunimoKartFallingBoulder(boulder, elapsedSeconds, out var bounds) &&
            playerBounds.Intersects(bounds));
    }

    private static bool TryProjectJunimoKartFallingBoulder(
        JunimoKartFallingBoulderProjection projection,
        float elapsedSeconds,
        out Rectangle bounds)
    {
        bounds = default;
        var simulatedSeconds = elapsedSeconds - projection.ActivationSeconds;
        if (simulatedSeconds < 0f)
        {
            return false;
        }

        const float tickSeconds = 1f / 60f;
        var y = projection.Y;
        var speed = projection.CurrentFallSpeed;
        var trackIndex = 0;
        for (var elapsed = 0f; elapsed < simulatedSeconds; elapsed += tickSeconds)
        {
            var stepSeconds = Math.Min(tickSeconds, simulatedSeconds - elapsed);
            if (trackIndex < projection.RemainingTrackY.Count &&
                y >= projection.RemainingTrackY[trackIndex])
            {
                speed = -30f;
                trackIndex++;
            }
            if (speed < projection.FallSpeed)
            {
                speed = Math.Min(projection.FallSpeed, speed + 210f * stepSeconds);
            }
            y += speed * stepSeconds;
        }

        bounds = new Rectangle(
            (int)projection.X + projection.LocalBoundsX,
            (int)y + projection.LocalBoundsY,
            projection.Width,
            projection.Height);
        return true;
    }

    private readonly record struct JunimoKartFallingBoulderProjection(
        float ActivationSeconds,
        float X,
        float Y,
        float CurrentFallSpeed,
        float FallSpeed,
        int LocalBoundsX,
        int LocalBoundsY,
        int Width,
        int Height,
        IReadOnlyList<float> RemainingTrackY);
}
