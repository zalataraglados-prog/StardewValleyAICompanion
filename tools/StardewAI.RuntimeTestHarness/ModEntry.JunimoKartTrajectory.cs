using System.Reflection;
using Microsoft.Xna.Framework;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly FieldInfo MineCartSpeedMultiplierField =
        typeof(MineCart.MineCartCharacter).GetField(
            "_speedMultiplier",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart.MineCartCharacter).FullName, "_speedMultiplier");
    private static readonly FieldInfo MineCartJumpMomentumThresholdField =
        typeof(MineCart.MineCartCharacter).GetField(
            "_jumpMomentumThreshhold",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart.MineCartCharacter).FullName, "_jumpMomentumThreshhold");

    private static bool TryPlanJunimoKartJump(
        MineCart game,
        MineCart.PlayerMineCartCharacter player,
        float minimumLandingX,
        out JunimoKartJumpPlan plan)
    {
        var bubbles = GetJunimoKartBubbles(game)
            .Select(bubble =>
            {
                var bounds = bubble.GetBounds();
                return new JunimoKartBubbleProjection(
                    bubble.position.X,
                    bubble.position.Y,
                    bubble._normalizedVelocity.X * bubble.moveSpeed,
                    bubble._normalizedVelocity.Y * bubble.moveSpeed,
                    bounds.X - (int)bubble.position.X,
                    bounds.Y - (int)bubble.position.Y,
                    bounds.Width,
                    bounds.Height);
            })
            .ToArray();
        var fallingBoulders = GetJunimoKartFallingBoulderProjections(game);
        var speedMultiplier = (int)MineCartThemeField.GetValue(game)! == 5
            ? 1f
            : (float)MineCartSpeedMultiplierField.GetValue(player)!;
        var momentumThreshold = (float)MineCartJumpMomentumThresholdField.GetValue(player)!;
        JunimoKartJumpPlan? best = null;

        for (var candidateHoldTicks = 1; candidateHoldTicks <= 90; candidateHoldTicks++)
        {
            if (!TrySimulateJunimoKartJump(
                    game,
                    player,
                    minimumLandingX,
                    candidateHoldTicks,
                    speedMultiplier,
                    momentumThreshold,
                    bubbles,
                    fallingBoulders,
                    out var candidate))
            {
                continue;
            }

            if (best is null || candidate.SafetyScore > best.Value.SafetyScore)
            {
                best = candidate;
            }
        }

        plan = best ?? default;
        return best.HasValue;
    }

    private static bool TrySimulateJunimoKartJump(
        MineCart game,
        MineCart.PlayerMineCartCharacter player,
        float minimumLandingX,
        int holdTicks,
        float speedMultiplier,
        float momentumThreshold,
        IReadOnlyList<JunimoKartBubbleProjection> bubbles,
        IReadOnlyList<JunimoKartFallingBoulderProjection> fallingBoulders,
        out JunimoKartJumpPlan plan)
    {
        const float tickSeconds = 1f / 60f;
        var x = player.position.X;
        var y = player.position.Y;
        var velocityY = -player.jumpStrength;
        var gravity = 0f;
        var jumpFloatAge = 0f;
        var jumping = true;

        for (var tick = 1; tick <= 240; tick++)
        {
            if (velocityY >= 0f &&
                TryFindJunimoKartLandingTrack(game, x, y, out var landingTrack))
            {
                y = landingTrack.GetYAtPoint(x);
                if (x < minimumLandingX)
                {
                    plan = default;
                    return false;
                }

                var landingSpeedMultiplier = AdvanceJunimoKartGroundedSpeedMultiplier(
                    speedMultiplier,
                    landingTrack.trackType,
                    (int)MineCartThemeField.GetValue(game)!,
                    tickSeconds);
                var observedLandingX = x +
                    tickSeconds * player.velocity.X * landingSpeedMultiplier;
                var forwardRunway = MeasureJunimoKartForwardRunway(
                    game,
                    observedLandingX,
                    landingTrack.GetYAtPoint(observedLandingX));
                var tileCenter = landingTrack.position.X + game.tileSize / 2f;
                var centerMargin = Math.Max(
                    0f,
                    game.tileSize / 2f - Math.Abs(observedLandingX - tileCenter));
                var safetyScore = forwardRunway * 100f +
                    centerMargin * 10f -
                    Math.Abs(velocityY) * 0.05f -
                    holdTicks * 0.001f;
                plan = new JunimoKartJumpPlan(
                    holdTicks,
                    observedLandingX,
                    y,
                    forwardRunway,
                    centerMargin,
                    safetyScore);
                return true;
            }

            if (jumping && tick > holdTicks)
            {
                jumping = false;
                gravity = 0f;
                if (velocityY < momentumThreshold)
                {
                    velocityY = momentumThreshold;
                }
            }

            if (jumping)
            {
                jumpFloatAge += tickSeconds;
                if (jumpFloatAge < player.jumpFloatDuration)
                {
                    gravity = 0f;
                    velocityY = -player.jumpStrength *
                        (jumpFloatAge / player.jumpFloatDuration);
                }
                else if (velocityY <= momentumThreshold * 2f)
                {
                    gravity += tickSeconds * player.jumpGravity;
                }
                else
                {
                    velocityY = momentumThreshold;
                    gravity = 0f;
                    jumping = false;
                }
            }
            else
            {
                gravity += tickSeconds * player.fallGravity;
            }

            velocityY += tickSeconds * gravity;
            x += tickSeconds * player.velocity.X * speedMultiplier;
            y += tickSeconds * velocityY;
            if (velocityY > 0f)
            {
                jumping = false;
            }
            velocityY = Math.Min(velocityY, player.GetMaxFallSpeed());

            var elapsedSeconds = tick * tickSeconds;
            if (JunimoKartSimulatedBubbleCollision(bubbles, elapsedSeconds, x, y) ||
                JunimoKartSimulatedFallingBoulderCollision(fallingBoulders, elapsedSeconds, x, y) ||
                JunimoKartSimulatedObstacleCollision(game, x, y))
            {
                break;
            }
        }

        plan = default;
        return false;
    }

    private static bool TryFindJunimoKartLandingTrack(
        MineCart game,
        float x,
        float y,
        out MineCart.Track track)
    {
        foreach (var offset in new[] { 0f, 4f, -4f })
        {
            var testPosition = new Vector2(x + offset, y);
            var tracks = game.GetTracksForXPosition(testPosition.X);
            if (tracks is null)
            {
                continue;
            }
            foreach (var candidate in tracks)
            {
                if (candidate.CanLandHere(testPosition))
                {
                    track = candidate;
                    return true;
                }
            }
        }

        track = null!;
        return false;
    }

    private static float AdvanceJunimoKartGroundedSpeedMultiplier(
        float current,
        MineCart.Track.TrackType trackType,
        int theme,
        float elapsedSeconds)
    {
        if (theme == 5)
        {
            current = 1f;
        }

        return trackType switch
        {
            MineCart.Track.TrackType.SlimeUpSlope => 0.5f,
            MineCart.Track.TrackType.IceDownSlope => MoveTowards(
                current,
                3f,
                elapsedSeconds * 2f),
            _ => MoveTowards(current, 1f, elapsedSeconds * 6f)
        };
    }

    private static float MoveTowards(float current, float target, float maximumDelta)
    {
        if (Math.Abs(target - current) <= maximumDelta)
        {
            return target;
        }
        return current + Math.Sign(target - current) * maximumDelta;
    }

    private static float MeasureJunimoKartForwardRunway(
        MineCart game,
        float landingX,
        float landingY)
    {
        const float maximumRunway = 96f;
        var pathY = landingY;
        for (var offset = 4f; offset <= maximumRunway; offset += 4f)
        {
            var x = landingX + offset;
            var tracks = game.GetTracksForXPosition(x);
            if (tracks is null || tracks.Count == 0)
            {
                return offset - 4f;
            }

            var nearest = tracks
                .OrderBy(track => Math.Abs(track.GetYAtPoint(x) - pathY))
                .First();
            var nextY = nearest.GetYAtPoint(x);
            if (Math.Abs(nextY - pathY) > 8f)
            {
                return offset - 4f;
            }

            var groundedBounds = new Rectangle((int)x - 4, nextY - 12, 8, 12);
            if (nearest.obstacle is not null &&
                nearest.obstacle.enabled &&
                groundedBounds.Intersects(nearest.obstacle.GetBounds()))
            {
                return offset - 4f;
            }
            pathY = nextY;
        }

        return maximumRunway;
    }

    private readonly record struct JunimoKartJumpPlan(
        int HoldTicks,
        float LandingX,
        float LandingY,
        float ForwardRunway,
        float CenterMargin,
        float SafetyScore);
}
