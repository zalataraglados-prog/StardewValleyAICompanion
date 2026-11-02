using Microsoft.Xna.Framework;
using System.Reflection;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupLevelUpProfessionFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_level_up_profession", "menus.active_menu.type=LevelUpMenu", CloseMenuObservedEffect(), reasons.ToArray());
        }
        if (!request.ProfessionChoiceId.HasValue || request.ProfessionChoiceId.Value is < 0 or > 29)
        {
            return BlockedWithPrimitive(request, "debug_setup_level_up_profession", "profession_choice_id=0..29", CloseMenuObservedEffect(), "profession_choice_id_0_29_required");
        }

        var choiceId = request.ProfessionChoiceId.Value;
        if (!TryPreparePendingProfessionLevel(
                choiceId,
                out var skill,
                out var level))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_level_up_profession",
                "menus.active_menu.type=LevelUpMenu",
                CloseMenuObservedEffect(),
                "profession_choice_id_0_29_required");
        }
        var player = Game1.player;
        player.health = player.maxHealth;
        player.stamina = player.MaxStamina;
        Game1.exitActiveMenu();
        var menu = new LevelUpMenu(skill, level);
        Game1.activeClickableMenu = menu;
        menu.update(new GameTime(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500)));
        menu.update(new GameTime(TimeSpan.FromMilliseconds(516), TimeSpan.FromMilliseconds(16)));

        var offered = typeof(LevelUpMenu)
            .GetField("professionsToChoose", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(menu) is IEnumerable<int> choices
                ? choices.ToArray()
                : Array.Empty<int>();
        var verified = menu.isActive && menu.isProfessionChooser && menu.CanReceiveInput() &&
            offered.Length == 2 && offered.Contains(choiceId);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_level_up_profession",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_level_up_menu_offers_requested_profession" }
                : new[] { "native_level_up_menu_fixture_mismatch" },
            RequestedEffect = "skill=" + skill + ";level=" + level + ";profession_choice_id=" + choiceId,
            ObservedEffect = "menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";offered=" + string.Join(",", offered) +
                ";is_active=" + menu.isActive.ToString().ToLowerInvariant() +
                ";is_profession_chooser=" + menu.isProfessionChooser.ToString().ToLowerInvariant() +
                ";can_receive_input=" + menu.CanReceiveInput().ToString().ToLowerInvariant() +
                ";pending_level=" + player.newLevels.Contains(new Point(skill, level)).ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "native_level_up_menu_fixture_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "menus.active_menu.type", Before = "none", After = Game1.activeClickableMenu?.GetType().Name ?? "none" },
                new SimulatedFactChange { Path = "menus.menu_specific_state.profession_choices", Before = string.Empty, After = string.Join(",", offered) },
                new SimulatedFactChange { Path = "player.new_levels", Before = string.Empty, After = skill + ":" + level }
            }
        };
    }

    private static bool TryPreparePendingProfessionLevel(
        int choiceId,
        out int skill,
        out int level)
    {
        skill = -1;
        level = -1;
        if (choiceId is < 0 or > 29)
        {
            return false;
        }

        var preparedSkill = choiceId / 6;
        var branch = choiceId % 6;
        var preparedLevel = branch < 2 ? 5 : 10;
        var player = Game1.player;
        PreparePendingSkillLevel(player, preparedSkill, preparedLevel);

        if (preparedLevel == 10)
        {
            var prerequisiteProfession =
                preparedSkill * 6 + (branch < 4 ? 0 : 1);
            player.professions.Add(prerequisiteProfession);
            if (preparedSkill == Farmer.combatSkill)
            {
                LevelUpMenu.RevalidateHealth(player);
            }
        }

        skill = preparedSkill;
        level = preparedLevel;
        return true;
    }

    private static void PreparePendingSkillLevel(
        Farmer player,
        int skill,
        int level)
    {
        if (skill is < Farmer.farmingSkill or > Farmer.combatSkill ||
            level is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(skill),
                "Fixture skill must be a vanilla profession skill at level 1..10.");
        }

        for (var id = skill * 6; id < skill * 6 + 6; id++)
        {
            if (!player.professions.Contains(id))
            {
                continue;
            }

            LevelUpMenu.removeImmediateProfessionPerk(id);
            player.professions.Remove(id);
        }

        switch (skill)
        {
            case Farmer.farmingSkill:
                player.farmingLevel.Value = level;
                break;
            case Farmer.fishingSkill:
                player.fishingLevel.Value = level;
                break;
            case Farmer.foragingSkill:
                player.foragingLevel.Value = level;
                break;
            case Farmer.miningSkill:
                player.miningLevel.Value = level;
                break;
            case Farmer.combatSkill:
                player.combatLevel.Value = level;
                break;
        }

        player.experiencePoints[skill] =
            Farmer.getBaseExperienceForLevel(level);
        player.newLevels.RemoveWhere(point => point.X == skill);
        player.newLevels.Add(new Point(skill, level));
        if (skill == Farmer.combatSkill)
        {
            LevelUpMenu.RevalidateHealth(player);
        }
    }
}
