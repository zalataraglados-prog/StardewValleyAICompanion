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
        var skill = choiceId / 6;
        var branch = choiceId % 6;
        var level = branch < 2 ? 5 : 10;
        var player = Game1.player;
        for (var id = skill * 6; id < skill * 6 + 6; id++)
        {
            if (player.professions.Contains(id))
            {
                LevelUpMenu.removeImmediateProfessionPerk(id);
                player.professions.Remove(id);
            }
        }
        int? prerequisiteProfession = null;
        if (level == 10)
        {
            prerequisiteProfession = skill * 6 + (branch < 4 ? 0 : 1);
            player.professions.Add(prerequisiteProfession.Value);
        }

        player.newLevels.RemoveWhere(point => point.X == skill && point.Y == level);
        player.newLevels.Add(new Point(skill, level));
        player.health = player.maxHealth;
        player.stamina = player.MaxStamina;
        Game1.exitActiveMenu();
        var menu = new LevelUpMenu(skill, level);
        if (prerequisiteProfession.HasValue)
        {
            menu.getImmediateProfessionPerk(prerequisiteProfession.Value);
        }
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
}
