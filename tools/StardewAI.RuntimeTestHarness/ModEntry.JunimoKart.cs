using System.Globalization;
using System.Reflection;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Minigames;
using StardewValley.SpecialOrders.Objectives;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly FieldInfo MineCartScoreField =
        typeof(MineCart).GetField("score", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart).FullName, "score");
    private static readonly FieldInfo MineCartModeField =
        typeof(MineCart).GetField("gameMode", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart).FullName, "gameMode");
    private static readonly FieldInfo MineCartPlayerField =
        typeof(MineCart).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart).FullName, "player");
    private static readonly FieldInfo MineCartThemeField =
        typeof(MineCart).GetField("currentTheme", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart).FullName, "currentTheme");
    private static readonly FieldInfo MineCartEntitiesField =
        typeof(MineCart).GetField("_entities", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MineCart).FullName, "_entities");

    private sealed class ActiveJunimoKart
    {
        public ActiveJunimoKart(
            PendingExecution pending,
            MineCart game,
            JKScoreObjective objective)
        {
            Pending = pending;
            Game = game;
            Objective = objective;
            ProgressBefore = objective.GetCount();
            TargetScore = pending.Request.MinigameTargetScore!.Value;
            MaxAttempts = pending.Request.MinigameMaxAttempts!.Value;
        }

        public PendingExecution Pending { get; }
        public MineCart Game { get; }
        public JKScoreObjective Objective { get; }
        public int ProgressBefore { get; }
        public int TargetScore { get; }
        public int MaxAttempts { get; }
        public int Attempts { get; set; } = 1;
        public int LastScore { get; set; }
        public int PeakScore { get; set; }
        public int ElapsedTicks { get; set; }
        public int InputTransitions { get; set; }
        public int JumpPresses { get; set; }
        public int PlannerFallbacks { get; set; }
        public int DeathSubmissions { get; set; }
        public int JumpHoldTicksRemaining { get; set; }
        public bool DeathObserved { get; set; }
        public bool InputPressed { get; set; }
        public bool AwaitingSubmissionDeath { get; set; }
        public bool SawIngame { get; set; }
        public float LastLiveX { get; set; }
        public float LastLiveY { get; set; }
        public float LastLiveVelocityY { get; set; }
        public int LastLiveBubbleCount { get; set; }
        public int LastLiveFallingBoulderCount { get; set; }
        public List<string> AttemptTrace { get; } = new();
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 180000;
    }

    private void StartJunimoKart(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (!string.Equals(request.MinigameId, "MineCart", StringComparison.Ordinal))
        {
            reasons.Add("junimo_kart_minigame_id_mismatch");
        }
        if (request.MinigameMode != MineCart.infiniteMode)
        {
            reasons.Add("junimo_kart_endless_mode_required");
        }
        if (!request.MinigameTargetScore.HasValue || request.MinigameTargetScore.Value <= 0)
        {
            reasons.Add("junimo_kart_positive_target_score_required");
        }
        if (!request.MinigameMaxAttempts.HasValue || request.MinigameMaxAttempts.Value <= 0)
        {
            reasons.Add("junimo_kart_positive_max_attempts_required");
        }
        if (!Game1.player.hasSkullKey)
        {
            reasons.Add("junimo_kart_skull_key_required");
        }
        if (Game1.currentMinigame is not MineCart game)
        {
            reasons.Add("junimo_kart_native_minigame_not_active");
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "play_junimo_kart",
                JunimoKartRequestedEffect(request),
                JunimoKartObservedEffect(null),
                reasons.ToArray()));
            return;
        }
        if ((int)MineCartModeField.GetValue(game)! != MineCart.infiniteMode)
        {
            reasons.Add("junimo_kart_live_mode_not_endless");
        }

        var objective = ResolveJunimoKartObjective(request);
        if (objective is null)
        {
            reasons.Add("junimo_kart_exact_objective_not_found");
        }
        else if (objective.GetCount() != request.QuestExpectedCurrentCount ||
                 objective.GetMaxCount() != request.QuestExpectedTargetCount ||
                 objective.GetMaxCount() != request.MinigameTargetScore)
        {
            reasons.Add("junimo_kart_objective_projection_drifted");
        }

        if (reasons.Count > 0 || objective is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "play_junimo_kart",
                JunimoKartRequestedEffect(request),
                JunimoKartObservedEffect(game),
                reasons.ToArray()));
            return;
        }

        activeJunimoKart = new ActiveJunimoKart(pending, game, objective);
    }

    private static JKScoreObjective? ResolveJunimoKartObjective(
        TrainingExecutionRequest request)
    {
        if (!string.Equals(request.QuestFamily, "special_order", StringComparison.Ordinal) ||
            !string.Equals(request.QuestRuntimeType, "SpecialOrder", StringComparison.Ordinal) ||
            !request.QuestObjectiveIndex.HasValue)
        {
            return null;
        }

        var order = Game1.player.team.specialOrders.SingleOrDefault(candidate =>
            string.Equals(candidate.questKey.Value, request.QuestKey, StringComparison.Ordinal));
        var index = request.QuestObjectiveIndex.Value;
        return order is not null && index >= 0 && index < order.objectives.Count
            ? order.objectives[index] as JKScoreObjective
            : null;
    }

    private bool ApplyJunimoKartInput(
        ActiveJunimoKart active,
        out string reason)
    {
        reason = string.Empty;
        if (!ReferenceEquals(Game1.currentMinigame, active.Game))
        {
            return true;
        }

        var pressed = ShouldPressJunimoKartInput(active);
        if (pressed != active.InputPressed)
        {
            active.InputTransitions++;
            if (pressed)
            {
                active.JumpPresses++;
            }
        }
        active.InputPressed = pressed;
        return TryApplySmapiLeftButtonOverride(pressed, out reason);
    }

    private static bool ShouldPressJunimoKartInput(ActiveJunimoKart active)
    {
        if (active.AwaitingSubmissionDeath)
        {
            return false;
        }

        if (active.Game.gameState != MineCart.GameStates.Ingame)
        {
            return active.ElapsedTicks % 30 == 1;
        }

        active.SawIngame = true;
        var player = MineCartPlayerField.GetValue(active.Game) as MineCart.PlayerMineCartCharacter;
        if (player is null || !player.enabled)
        {
            return false;
        }
        if (!player.IsGrounded())
        {
            if (active.JumpHoldTicksRemaining > 0 && player.velocity.Y < 0f)
            {
                active.JumpHoldTicksRemaining--;
                return true;
            }
            return false;
        }

        active.JumpHoldTicksRemaining = 0;
        var playerX = player.position.X;
        var playerY = player.position.Y;
        var speed = Math.Max(80f, Math.Abs(player.velocity.X));
        var triggerDistance = Math.Clamp(speed * 0.34f, 30f, 52f);
        var lookahead = Math.Clamp(speed * 0.75f, 64f, 112f);
        var firstGap = float.MaxValue;
        var firstObstacle = float.MaxValue;
        var firstBubble = FindJunimoKartBubbleHazardDistance(active.Game, player, lookahead);
        var fallingBoulders = GetJunimoKartFallingBoulderProjections(active.Game);
        var firstFallingBoulder = FindJunimoKartFallingBoulderHazardDistance(
            active.Game,
            player,
            lookahead,
            fallingBoulders);
        var pathY = playerY;

        for (var offset = 8f; offset <= lookahead; offset += 4f)
        {
            var tracks = active.Game.GetTracksForXPosition(playerX + offset);
            if (tracks is null || tracks.Count == 0)
            {
                firstGap = Math.Min(firstGap, offset);
                break;
            }
            var nearest = tracks
                .OrderBy(track => Math.Abs(track.GetYAtPoint(playerX + offset) - pathY))
                .First();
            var trackY = nearest.GetYAtPoint(playerX + offset);
            if (Math.Abs(trackY - pathY) > 8f)
            {
                firstGap = Math.Min(firstGap, offset);
                break;
            }
            pathY = trackY;

            if (nearest.obstacle is not null && nearest.obstacle.enabled)
            {
                var obstacleDistance = nearest.obstacle.GetBounds().Left - player.GetBounds().Right;
                if (obstacleDistance >= -2f)
                {
                    firstObstacle = Math.Min(firstObstacle, obstacleDistance);
                }
            }
        }

        var hazardDistance = Math.Min(
            Math.Min(firstBubble, firstFallingBoulder),
            Math.Min(firstObstacle, firstGap));
        if (hazardDistance > triggerDistance)
        {
            return false;
        }

        var minimumLandingX = playerX + Math.Max(24f, hazardDistance + 18f);
        if (!TryPlanJunimoKartJump(active.Game, player, minimumLandingX, out var holdTicks))
        {
            active.PlannerFallbacks++;
            holdTicks = 90;
        }
        active.JumpHoldTicksRemaining = Math.Max(0, holdTicks - 1);
        return true;
    }

    private static bool TryPlanJunimoKartJump(
        MineCart game,
        MineCart.PlayerMineCartCharacter player,
        float minimumLandingX,
        out int holdTicks)
    {
        const float tickSeconds = 1f / 60f;
        var horizontalSpeed = Math.Max(80f, Math.Abs(player.velocity.X));
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
        for (var candidateHoldTicks = 2; candidateHoldTicks <= 90; candidateHoldTicks += 2)
        {
            var x = player.position.X;
            var y = player.position.Y;
            var velocityY = -player.jumpStrength;
            var gravity = 0f;
            var jumpFloatAge = 0f;
            var jumping = true;
            for (var tick = 1; tick <= 240; tick++)
            {
                if (jumping && tick > candidateHoldTicks)
                {
                    jumping = false;
                    gravity = 0f;
                    velocityY = Math.Max(velocityY, -30f);
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
                    else if (velocityY <= -60f)
                    {
                        gravity += tickSeconds * player.jumpGravity;
                    }
                    else
                    {
                        velocityY = -30f;
                        jumping = false;
                    }
                }
                else
                {
                    gravity += tickSeconds * player.fallGravity;
                }

                velocityY += tickSeconds * gravity;
                x += tickSeconds * horizontalSpeed;
                y += tickSeconds * velocityY;
                velocityY = Math.Min(velocityY, player.GetMaxFallSpeed());

                if (JunimoKartSimulatedBubbleCollision(bubbles, tick * tickSeconds, x, y))
                {
                    break;
                }
                if (JunimoKartSimulatedFallingBoulderCollision(
                        fallingBoulders,
                        tick * tickSeconds,
                        x,
                        y))
                {
                    break;
                }
                if (JunimoKartSimulatedObstacleCollision(game, x, y))
                {
                    break;
                }
                if (velocityY < 0f || x < minimumLandingX)
                {
                    continue;
                }

                var tracks = game.GetTracksForXPosition(x);
                if (tracks is not null && tracks.Any(track =>
                        track.CanLandHere(new Microsoft.Xna.Framework.Vector2(x, y))))
                {
                    holdTicks = candidateHoldTicks;
                    return true;
                }
            }
        }

        holdTicks = 0;
        return false;
    }

    private static float FindJunimoKartBubbleHazardDistance(
        MineCart game,
        MineCart.PlayerMineCartCharacter player,
        float lookahead)
    {
        var firstHazard = float.MaxValue;
        var playerBounds = player.GetBounds();
        foreach (var bubble in GetJunimoKartBubbles(game))
        {
            var bubbleBounds = bubble.GetBounds();
            var distance = bubbleBounds.Left - playerBounds.Right;
            if (distance < -bubbleBounds.Width || distance > lookahead)
            {
                continue;
            }

            var closingSpeed = Math.Max(
                1f,
                Math.Abs(player.velocity.X) - bubble._normalizedVelocity.X * bubble.moveSpeed);
            var secondsUntilContact = Math.Max(0f, distance) / closingSpeed;
            var contactX = player.position.X + Math.Abs(player.velocity.X) * secondsUntilContact;
            var contactTracks = game.GetTracksForXPosition(contactX);
            if (contactTracks is null || contactTracks.Count == 0)
            {
                continue;
            }

            var groundY = contactTracks
                .OrderBy(track => Math.Abs(track.GetYAtPoint(contactX) - player.position.Y))
                .First()
                .GetYAtPoint(contactX);
            var projectedBubbleY = bubble.position.Y +
                bubble._normalizedVelocity.Y * bubble.moveSpeed * secondsUntilContact;
            var projectedBubbleBounds = new Microsoft.Xna.Framework.Rectangle(
                bubbleBounds.X + (int)(bubble._normalizedVelocity.X * bubble.moveSpeed * secondsUntilContact),
                bubbleBounds.Y + (int)(projectedBubbleY - bubble.position.Y),
                bubbleBounds.Width,
                bubbleBounds.Height);
            var projectedGroundedPlayerBounds = new Microsoft.Xna.Framework.Rectangle(
                (int)contactX - 4,
                groundY - 12,
                8,
                12);
            if (projectedGroundedPlayerBounds.Intersects(projectedBubbleBounds))
            {
                firstHazard = Math.Min(firstHazard, Math.Max(0f, distance));
            }
        }

        return firstHazard;
    }

    private static IEnumerable<MineCart.Bubble> GetJunimoKartBubbles(MineCart game) =>
        ((IEnumerable<MineCart.Entity>)MineCartEntitiesField.GetValue(game)!)
            .OfType<MineCart.Bubble>()
            .Where(bubble => bubble.enabled);

    private static bool JunimoKartSimulatedBubbleCollision(
        IReadOnlyList<JunimoKartBubbleProjection> bubbles,
        float elapsedSeconds,
        float playerX,
        float playerY)
    {
        var playerBounds = new Microsoft.Xna.Framework.Rectangle(
            (int)playerX - 4,
            (int)playerY - 12,
            8,
            12);
        return bubbles.Any(bubble => playerBounds.Intersects(new Microsoft.Xna.Framework.Rectangle(
            (int)(bubble.X + bubble.VelocityX * elapsedSeconds) + bubble.LocalBoundsX,
            (int)(bubble.Y + bubble.VelocityY * elapsedSeconds) + bubble.LocalBoundsY,
            bubble.Width,
            bubble.Height)));
    }

    private readonly record struct JunimoKartBubbleProjection(
        float X,
        float Y,
        float VelocityX,
        float VelocityY,
        int LocalBoundsX,
        int LocalBoundsY,
        int Width,
        int Height);

    private static bool JunimoKartSimulatedObstacleCollision(
        MineCart game,
        float x,
        float y)
    {
        var playerBounds = new Microsoft.Xna.Framework.Rectangle(
            (int)x - 4,
            (int)y - 12,
            8,
            12);
        var tracks = game.GetTracksForXPosition(x);
        return tracks is not null && tracks.Any(track =>
            track.obstacle is not null &&
            track.obstacle.enabled &&
            playerBounds.Intersects(track.obstacle.GetBounds()));
    }

    private void TickJunimoKart()
    {
        var active = activeJunimoKart;
        if (active is null)
        {
            return;
        }

        active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteBlockedJunimoKart(active, "junimo_kart_timeout");
            return;
        }
        if (!ReferenceEquals(Game1.currentMinigame, active.Game))
        {
            CompleteBlockedJunimoKart(active, "junimo_kart_minigame_closed_before_verified_submission");
            return;
        }

        var score = (int)MineCartScoreField.GetValue(active.Game)!;
        active.PeakScore = Math.Max(active.PeakScore, score);
        var player = MineCartPlayerField.GetValue(active.Game) as MineCart.PlayerMineCartCharacter;
        if (player is not null &&
            active.Game.gameState == MineCart.GameStates.Ingame &&
            !player.enabled &&
            player.position.X > 0f &&
            score > 0 &&
            !active.DeathObserved)
        {
            active.DeathObserved = true;
            active.AttemptTrace.Add(
                "attempt=" + active.Attempts +
                ",score=" + score +
                ",theme=" + MineCartThemeField.GetValue(active.Game) +
                ",x=" + Math.Round(active.LastLiveX, 1).ToString(CultureInfo.InvariantCulture) +
                ",y=" + Math.Round(active.LastLiveY, 1).ToString(CultureInfo.InvariantCulture) +
                ",vy=" + Math.Round(active.LastLiveVelocityY, 1).ToString(CultureInfo.InvariantCulture) +
                ",bubbles=" + active.LastLiveBubbleCount +
                ",falling_boulders=" + active.LastLiveFallingBoulderCount);
        }
        else if (player is not null && player.enabled)
        {
            active.DeathObserved = false;
            active.LastLiveX = player.position.X;
            active.LastLiveY = player.position.Y;
            active.LastLiveVelocityY = player.velocity.Y;
            active.LastLiveBubbleCount = GetJunimoKartBubbles(active.Game).Count();
            active.LastLiveFallingBoulderCount = GetJunimoKartFallingBoulders(active.Game).Count();
        }
        if (score < active.LastScore)
        {
            active.Attempts++;
            active.DeathSubmissions++;
        }
        active.LastScore = score;
        if (score >= active.TargetScore)
        {
            active.AwaitingSubmissionDeath = true;
        }

        var progress = active.Objective.GetCount();
        if (progress >= active.TargetScore)
        {
            active.Game.QuitGame();
            CompleteJunimoKart(active, progress);
            return;
        }
        if (active.Attempts > active.MaxAttempts)
        {
            CompleteBlockedJunimoKart(active, "junimo_kart_attempt_limit_exhausted");
        }
    }

    private void CompleteJunimoKart(ActiveJunimoKart active, int progressAfter)
    {
        activeJunimoKart = null;
        ReleaseSmapiLeftButtonOverride();
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "play_junimo_kart",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "native_MineCart_endless_mode_observed",
                "native_score_submission_callback_observed",
                "matching_JKScoreObjective_progress_reached_target",
                "no_score_track_collision_or_objective_mutation"
            },
            RequestedEffect = JunimoKartRequestedEffect(request),
            ObservedEffect = JunimoKartObservedEffect(active.Game),
            QuestProgressBefore = active.ProgressBefore,
            QuestProgressAfter = progressAfter,
            QuestTargetCount = active.TargetScore,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "quests.special_orders.jk_score.current_count", Before = active.ProgressBefore.ToString(CultureInfo.InvariantCulture), After = progressAfter.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "minigame.junimo_kart.peak_score", Before = "0", After = active.PeakScore.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "minigame.junimo_kart.attempts", Before = "0", After = active.Attempts.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "minigame.junimo_kart.input_transitions", Before = "0", After = active.InputTransitions.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "minigame.junimo_kart.attempt_trace", Before = "", After = string.Join(";", active.AttemptTrace) }
            }
        });
    }

    private void CompleteBlockedJunimoKart(
        ActiveJunimoKart active,
        params string[] reasons)
    {
        activeJunimoKart = null;
        ReleaseSmapiLeftButtonOverride();
        if (ReferenceEquals(Game1.currentMinigame, active.Game))
        {
            active.Game.QuitGame();
        }
        var result = BlockedWithPrimitive(
            active.Pending.Request,
            "play_junimo_kart",
            JunimoKartRequestedEffect(active.Pending.Request),
            JunimoKartObservedEffect(active.Game) +
                ";peak_score=" + active.PeakScore +
                ";attempts=" + active.Attempts +
                ";jump_presses=" + active.JumpPresses +
                ";planner_fallbacks=" + active.PlannerFallbacks +
                ";input_transitions=" + active.InputTransitions +
                ";death_submissions=" + active.DeathSubmissions +
                ";attempt_trace=" + string.Join("|", active.AttemptTrace),
            reasons);
        result.QuestProgressBefore = active.ProgressBefore;
        result.QuestProgressAfter = active.Objective.GetCount();
        result.QuestTargetCount = active.TargetScore;
        active.Pending.Completion.SetResult(result);
    }

    private static string JunimoKartRequestedEffect(TrainingExecutionRequest request) =>
        "minigame=MineCart;mode=2;target_score=" + request.MinigameTargetScore +
        ";quest_key=" + request.QuestKey +
        ";objective_index=" + request.QuestObjectiveIndex;

    private static string JunimoKartObservedEffect(MineCart? game) =>
        "current_minigame=" + (Game1.currentMinigame?.minigameId() ?? "none") +
        ";game_state=" + (game?.gameState.ToString() ?? "none") +
        ";mode=" + (game is null ? "none" : MineCartModeField.GetValue(game)?.ToString() ?? "none") +
        ";score=" + (game is null ? "none" : MineCartScoreField.GetValue(game)?.ToString() ?? "none");
}
