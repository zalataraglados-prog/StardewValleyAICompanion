using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static readonly FieldOfficePieceSpec[] FieldOfficePieceSpecs =
    {
        new(0, "skeleton_back_leg", "center_skeleton", "(O)823"),
        new(1, "skeleton_ribs", "center_skeleton", "(O)824"),
        new(2, "skeleton_front_leg", "center_skeleton", "(O)823"),
        new(3, "skeleton_tail", "center_skeleton", "(O)822"),
        new(4, "skeleton_spine", "center_skeleton", "(O)821"),
        new(5, "skeleton_skull", "center_skeleton", "(O)820"),
        new(6, "snake_tail", "snake", "(O)826"),
        new(7, "snake_spine", "snake", "(O)826"),
        new(8, "snake_skull", "snake", "(O)825"),
        new(9, "bat", "bat", "(O)827"),
        new(10, "frog", "frog", "(O)828")
    };

    private static readonly IReadOnlyDictionary<string, int[]> FieldOfficeNativeSlotOrder =
        new Dictionary<string, int[]>(StringComparer.Ordinal)
        {
            ["(O)820"] = new[] { 5 },
            ["(O)821"] = new[] { 4 },
            ["(O)822"] = new[] { 3 },
            ["(O)823"] = new[] { 0, 2 },
            ["(O)824"] = new[] { 1 },
            ["(O)825"] = new[] { 8 },
            ["(O)826"] = new[] { 7, 6 },
            ["(O)827"] = new[] { 9 },
            ["(O)828"] = new[] { 10 }
        };

    private static IslandFieldOfficeProgressRef? ReadIslandFieldOffice(
        IslandFieldOffice? office,
        Farmer? actor,
        StardewValley.Network.NetWorldState? world)
    {
        if (office is null || actor is null || world is null)
            return null;

        var donated = Enumerable.Range(0, IslandFieldOffice.totalPieces)
            .Select(index => index < office.piecesDonated.Count && office.piecesDonated[index])
            .ToArray();
        var rewards = FieldOfficeRewards(office.uncollectedRewards);
        var deskTiles = FieldOfficeActionTiles(office, "FieldOfficeDesk");
        var surveyTiles = FieldOfficeActionTiles(office, "FieldOfficeSurvey");
        var current = ReferenceEquals(Game1.currentLocation, office);
        var unlocked = actor.hasOrWillReceiveMail("islandNorthCaveOpened");
        var professorAvailable = office.getSafariGuy() is not null;
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp && !Game1.eventUp;
        var sharedStatus = !unlocked
            ? "field_office_locked"
            : current && !professorAvailable
                ? "field_office_professor_unavailable"
                : current && !menuClear
                    ? "field_office_menu_dialogue_or_event_not_clear"
                    : office.safariGuyMutex.IsLocked()
                        ? "field_office_mutex_locked"
                        : deskTiles.Length == 0
                            ? "field_office_desk_action_unavailable"
                            : current ? "ready" : "route_to_field_office_required";
        var allPieces = donated.All(value => value);
        var surveysComplete = office.plantsRestoredLeft.Value && office.plantsRestoredRight.Value;
        var surveyWalnutDebrisCount = CountFieldOfficeWalnutDebris(office);
        var surveyStatus = !unlocked
            ? "field_office_locked"
            : current && !professorAvailable
                ? "field_office_professor_unavailable"
                : office.hasFailedSurveyToday.Value
                    ? "field_office_survey_failed_today"
                    : surveysComplete
                        ? "field_office_surveys_complete"
                        : surveyWalnutDebrisCount > 0
                            ? "field_office_existing_walnut_debris_requires_pickup"
                        : current && !menuClear
                            ? "field_office_menu_dialogue_or_event_not_clear"
                            : office.safariGuyMutex.IsLocked()
                                ? "field_office_mutex_locked"
                                : surveyTiles.Length == 0
                                    ? "field_office_survey_action_unavailable"
                                    : current ? "ready" : "route_to_field_office_required";

        return new IslandFieldOfficeProgressRef
        {
            LocationId = office.NameOrUniqueName,
            IsCurrentLocation = current,
            NorthCaveOpened = unlocked,
            ProfessorAvailable = professorAvailable,
            IntroReceivedOrPending = actor.hasOrWillReceiveMail("safariGuyIntro"),
            MutexLocked = office.safariGuyMutex.IsLocked(),
            MenuClear = menuClear,
            DeskActionTiles = deskTiles,
            SurveyActionTiles = surveyTiles,
            Pieces = FieldOfficePieceSpecs.Select(spec => new IslandFieldOfficePieceRef
            {
                PieceIndex = spec.Index,
                PieceKind = spec.Kind,
                SetKind = spec.SetKind,
                QualifiedItemId = spec.QualifiedItemId,
                Donated = donated[spec.Index]
            }).ToArray(),
            DonatedPieceCount = donated.Count(value => value),
            CenterSkeletonRestored = office.centerSkeletonRestored.Value,
            SnakeRestored = office.snakeRestored.Value,
            BatRestored = office.batRestored.Value,
            FrogRestored = office.frogRestored.Value,
            PlantsRestoredLeft = office.plantsRestoredLeft.Value,
            PlantsRestoredRight = office.plantsRestoredRight.Value,
            HasFailedSurveyToday = office.hasFailedSurveyToday.Value,
            NextSurveyKind = office.plantsRestoredLeft.Value
                ? office.plantsRestoredRight.Value ? "complete" : "purple_starfish"
                : "purple_flower",
            NextSurveyAnswer = office.plantsRestoredLeft.Value
                ? office.plantsRestoredRight.Value ? null : 18
                : 22,
            FinaleReady = allPieces && surveysComplete,
            FinaleReceivedOrPending = actor.hasOrWillReceiveMail("fieldOfficeFinale"),
            GoldenWalnutsFound = world.GoldenWalnutsFound,
            UncollectedRewards = rewards,
            DonationCandidates = actor.Items
                .Select((item, slot) => new { item, slot })
                .Where(entry => entry.item is StardewValley.Object && entry.item.Stack > 0)
                .Select(entry => FieldOfficeDonationCandidate(
                    entry.item!, entry.slot, donated, rewards, office, world.GoldenWalnutsFound, sharedStatus))
                .Where(candidate => candidate is not null)
                .Cast<IslandFieldOfficeDonationCandidateRef>()
                .OrderBy(candidate => candidate.SlotIndex)
                .ThenBy(candidate => candidate.TargetPieceIndex)
                .ToArray(),
            SurveyCandidates = FieldOfficeSurveyCandidates(
                office,
                actor,
                donated,
                world.GoldenWalnutsFound,
                surveyStatus),
            ProjectionStatus = "exact_locked_base_1.6.15"
        };
    }

    private static IslandFieldOfficeSurveyCandidateRef[] FieldOfficeSurveyCandidates(
        IslandFieldOffice office,
        Farmer actor,
        bool[] donated,
        int goldenWalnutsFound,
        string actionStatus)
    {
        if (office.plantsRestoredLeft.Value && office.plantsRestoredRight.Value)
            return Array.Empty<IslandFieldOfficeSurveyCandidateRef>();

        var left = !office.plantsRestoredLeft.Value;
        var surveyKind = left ? "purple_flower" : "purple_starfish";
        var answer = left ? 22 : 18;
        var answerMinimum = left ? 18 : 11;
        var answerMaximum = left ? 24 : 18;
        var nutKey = left ? "IslandLeftPlantRestored" : "IslandRightPlantRestored";
        var debrisBefore = CountFieldOfficeWalnutDebris(office);
        var debrisSpawnCount = goldenWalnutsFound < 130 ? 1 : 0;
        var leftAfter = left || office.plantsRestoredLeft.Value;
        var rightAfter = !left || office.plantsRestoredRight.Value;
        var finaleReady = donated.All(value => value) && leftAfter && rightAfter;
        return new[]
        {
            new IslandFieldOfficeSurveyCandidateRef
            {
                SurveyKind = surveyKind,
                Answer = answer,
                AnswerMinimum = answerMinimum,
                AnswerMaximum = answerMaximum,
                PromptQuestionKey = "Survey",
                PromptResponseKey = "Yes",
                AnswerQuestionKey = left ? "PurpleFlowerSurvey" : "PurpleStarfishSurvey",
                AnswerResponseKey = "Correct",
                PlantRestoredBefore = false,
                PlantRestoredAfter = true,
                FailedSurveyTodayBefore = office.hasFailedSurveyToday.Value,
                FailedSurveyTodayAfter = false,
                ExpectedCollectedNutKey = nutKey,
                CollectedNutBefore = actor.team.collectedNutTracker.Contains(nutKey),
                WalnutDebrisCountBefore = debrisBefore,
                WalnutDebrisCountAfter = debrisBefore,
                WalnutDebrisSpawnCount = debrisSpawnCount,
                GoldenWalnutsFoundBefore = goldenWalnutsFound,
                GoldenWalnutsFoundAfter = goldenWalnutsFound + debrisSpawnCount,
                GoldenWalnutsFoundDelta = debrisSpawnCount,
                OutputDelivery = debrisSpawnCount == 1
                    ? "native_debris_spawn_then_magnet_pickup_to_golden_walnuts_found"
                    : "none_at_130_walnuts_found",
                ExpectedFinaleReadyAfter = finaleReady,
                ExpectedFinaleTriggerAfter = finaleReady && !actor.hasOrWillReceiveMail("fieldOfficeFinale"),
                ActionStatus = actionStatus
            }
        };
    }

    private static int CountFieldOfficeWalnutDebris(IslandFieldOffice office) =>
        office.debris.Count(debris =>
            string.Equals(
                debris.item?.QualifiedItemId ?? ItemRegistry.QualifyItemId(debris.itemId.Value) ?? debris.itemId.Value,
                "(O)73",
                StringComparison.Ordinal));

    private static IslandFieldOfficeDonationCandidateRef? FieldOfficeDonationCandidate(
        Item item,
        int inventorySlot,
        bool[] donated,
        IslandFieldOfficeRewardRef[] rewardsBefore,
        IslandFieldOffice office,
        int goldenWalnutsFound,
        string sharedStatus)
    {
        if (!FieldOfficeNativeSlotOrder.TryGetValue(item.QualifiedItemId, out var slots))
            return null;
        var target = slots.FirstOrDefault(index => !donated[index], -1);
        if (target < 0)
            return null;

        var after = donated.ToArray();
        after[target] = true;
        var spec = FieldOfficePieceSpecs[target];
        var completesSet = spec.SetKind switch
        {
            "center_skeleton" => !office.centerSkeletonRestored.Value && after.Take(6).All(value => value),
            "snake" => !office.snakeRestored.Value && after.Skip(6).Take(3).All(value => value),
            "bat" => !office.batRestored.Value && after[9],
            "frog" => !office.frogRestored.Value && after[10],
            _ => false
        };
        var newRewards = completesSet
            ? FieldOfficeSetRewards(spec.SetKind, goldenWalnutsFound)
            : Array.Empty<IslandFieldOfficeRewardRef>();
        var nutKey = completesSet ? spec.SetKind switch
        {
            "center_skeleton" => "IslandCenterSkeletonRestored",
            "snake" => "IslandSnakeRestored",
            "bat" => "IslandBatRestored",
            "frog" => "IslandFrogRestored",
            _ => string.Empty
        } : string.Empty;
        var expectedFinale = after.All(value => value) &&
            office.plantsRestoredLeft.Value && office.plantsRestoredRight.Value;
        return new IslandFieldOfficeDonationCandidateRef
        {
            SlotIndex = inventorySlot,
            ItemId = item.ItemId,
            QualifiedItemId = item.QualifiedItemId,
            RuntimeType = item.GetType().FullName ?? item.GetType().Name,
            StackBefore = item.Stack,
            StackAfter = item.Stack - 1,
            TargetPieceIndex = target,
            TargetPieceKind = spec.Kind,
            TargetSetKind = spec.SetKind,
            DonatedPieceCountBefore = donated.Count(value => value),
            DonatedPieceCountAfter = donated.Count(value => value) + 1,
            CompletesSet = completesSet,
            NewRewardItems = newRewards,
            UncollectedRewardsBefore = rewardsBefore,
            UncollectedRewardsAfter = rewardsBefore.Concat(newRewards).ToArray(),
            ExpectedCollectedNutKey = nutKey,
            CollectedNutBefore = !string.IsNullOrWhiteSpace(nutKey) &&
                Game1.player.team.collectedNutTracker.Contains(nutKey),
            ExpectedFinaleReadyAfter = expectedFinale,
            ActionStatus = sharedStatus
        };
    }

    private static IslandFieldOfficeRewardRef[] FieldOfficeSetRewards(string setKind, int goldenWalnutsFound)
    {
        var walnutAllowed = goldenWalnutsFound < 130;
        return setKind switch
        {
            "center_skeleton" => (walnutAllowed
                ? new[] { Reward("(O)73", 6), Reward("(O)69", 1) }
                : new[] { Reward("(O)69", 1) }),
            "snake" => (walnutAllowed
                ? new[] { Reward("(O)73", 3), Reward("(O)835", 1) }
                : new[] { Reward("(O)835", 1) }),
            "bat" => new[] { walnutAllowed ? Reward("(O)73", 1) : Reward("(O)TentKit", 1) },
            "frog" => new[] { walnutAllowed ? Reward("(O)73", 1) : Reward("(O)926", 1) },
            _ => Array.Empty<IslandFieldOfficeRewardRef>()
        };
    }

    private static IslandFieldOfficeRewardRef[] FieldOfficeRewards(IEnumerable<Item> rewards) =>
        rewards.Select(item => Reward(item.QualifiedItemId, item.Stack, item.Quality)).ToArray();

    private static IslandFieldOfficeRewardRef Reward(string qualifiedItemId, int stack, int quality = 0) => new()
    {
        QualifiedItemId = qualifiedItemId,
        Stack = stack,
        Quality = quality
    };

    private static IslandFieldOfficeActionTileRef[] FieldOfficeActionTiles(IslandFieldOffice office, string token)
    {
        var buildings = office.map?.GetLayer("Buildings");
        if (buildings is null)
            return Array.Empty<IslandFieldOfficeActionTileRef>();
        var result = new List<IslandFieldOfficeActionTileRef>();
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                var action = office.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (string.Equals(action?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), token, StringComparison.Ordinal))
                    result.Add(new IslandFieldOfficeActionTileRef { TileX = x, TileY = y, ActionRaw = action! });
            }
        }
        return result.ToArray();
    }

    private sealed record FieldOfficePieceSpec(int Index, string Kind, string SetKind, string QualifiedItemId);
}
