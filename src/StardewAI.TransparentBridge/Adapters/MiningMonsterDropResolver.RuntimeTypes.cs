using StardewValley;
using System.Globalization;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace StardewAI.TransparentBridge.Adapters;

internal static partial class MiningMonsterDropResolver
{
    private static void AddRuntimeTypeExtraDrops(
        Monster monster,
        HashSet<string> guaranteed,
        HashSet<string> conditional,
        List<string[]> guaranteedOneOf,
        List<string> unresolved)
    {
        switch (monster)
        {
            case Bat bat:
                AddBatExtraDrops(bat, conditional);
                break;
            case BigSlime bigSlime when bigSlime.heldItem.Value is not null:
                guaranteed.Add(bigSlime.heldItem.Value.QualifiedItemId);
                break;
            case Bug bug when bug.isArmoredBug.Value:
                conditional.Add("(O)874");
                break;
            case DinoMonster:
                guaranteedOneOf.Add(new[] { "(O)107", "(O)580", "(O)583", "(O)584" });
                break;
            case Ghost when Game1.player.team.SpecialOrderActive("Wizard") && !Game1.MasterPlayer.hasOrWillReceiveMail("ectoplasmDrop"):
                conditional.Add("(O)875");
                break;
            case GreenSlime slime:
                AddGreenSlimeExtraDrops(slime, guaranteed, conditional);
                break;
            case MetalHead metalHead when (Game1.stats.getMonstersKilled(metalHead.Name) + 1 + (int)Game1.uniqueIDForThisGame) % 100 == 0:
                guaranteed.Add("(H)51");
                break;
            case Mummy:
            case Serpent:
                conditional.Add("(O)485");
                break;
            case RockGolem rockGolem:
                AddRockGolemExtraDrops(rockGolem, conditional);
                break;
            case Skeleton:
                conditional.Add("(W)5");
                break;
        }
        if (monster.GetType().Assembly != typeof(Monster).Assembly)
        {
            unresolved.Add("custom_monster_getExtraDropItems_override_not_introspected");
        }
    }

    private static void AddBatExtraDrops(Bat bat, HashSet<string> conditional)
    {
        if (!bat.hauntedSkull.Value)
        {
            return;
        }
        if (bat.cursedDoll.Value)
        {
            conditional.UnionWith(new[]
            {
                "(P)10", "(S)1004", "(S)1014", "(S)1263", "(S)1262", "(P)12", "(W)2", "(O)288",
                "(O)534", "(O)531", "(O)768", "(O)769", "(O)581", "(O)582", "(O)725", "(O)86",
                Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccVault") ? "(O)275" : "(O)749"
            });
        }
        if (Game1.IsWinter)
        {
            conditional.Add("(O)273");
        }
        conditional.Add("(M)CursedMannequinMale");
        conditional.Add("(M)CursedMannequinFemale");
        conditional.Add("(O)279");
    }

    private static void AddGreenSlimeExtraDrops(GreenSlime slime, HashSet<string> guaranteed, HashSet<string> conditional)
    {
        if (slime.prismatic.Value)
        {
            guaranteed.Add("(O)876");
            return;
        }

        var name = slime.Name;
        var color = slime.color.Value;
        if (!string.Equals(name, "Tiger Slime", StringComparison.Ordinal))
        {
            if (color.R >= 50 && color.R <= 100 && color.G >= 25 && color.G <= 50 && color.B <= 25)
            {
                guaranteed.Add("(O)388");
                conditional.Add("(O)709");
            }
            else if (color.R < 80 && color.G < 80 && color.B < 80)
            {
                guaranteed.Add("(O)382");
                conditional.Add("(O)553");
                conditional.Add("(O)539");
            }
            else if (color.R > 200 && color.G > 180 && color.B < 50)
            {
                guaranteed.Add("(O)384");
            }
            else if (color.R > 220 && color.G > 90 && color.G < 150 && color.B < 50)
            {
                guaranteed.Add("(O)378");
            }
            else if (color.R > 230 && color.G > 230 && color.B > 230)
            {
                guaranteed.Add(color.R % 2 == 1 ? "(O)338" : "(O)380");
                if (color.R % 2 == 0 && color.G % 2 == 0 && color.B % 2 == 0 || color.Equals(Microsoft.Xna.Framework.Color.White))
                {
                    guaranteed.Add("(O)72");
                }
            }
            else if (color.R > 150 && color.G > 150 && color.B > 150)
            {
                guaranteed.Add("(O)390");
            }
            else if (color.R > 150 && color.B > 180 && color.G < 50 && slime.specialNumber.Value % (slime.firstGeneration.Value ? 4 : 2) == 0)
            {
                guaranteed.Add("(O)386");
                if (slime.firstGeneration.Value)
                {
                    conditional.Add("(O)485");
                }
            }
        }

        if (Game1.MasterPlayer.mailReceived.Contains("slimeHutchBuilt") && slime.specialNumber.Value == 1)
        {
            var specialDrop = name switch
            {
                "Green Slime" => "(O)680",
                "Frost Jelly" => "(O)413",
                "Tiger Slime" => "(O)857",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(specialDrop))
            {
                guaranteed.Add(specialDrop);
            }
        }

        if (string.Equals(name, "Tiger Slime", StringComparison.Ordinal))
        {
            conditional.UnionWith(new[] { "(H)91", "(O)831", "(O)829", "(O)833", "(O)835" });
        }
    }

    private static void AddRockGolemExtraDrops(RockGolem golem, HashSet<string> conditional)
    {
        if (string.Equals(golem.Name, "Wilderness Golem", StringComparison.Ordinal))
        {
            conditional.Add("(H)40");
            if (Game1.IsSpring)
            {
                conditional.Add("(O)273");
            }
            return;
        }
        if (!string.Equals(golem.Name, "Iridium Golem", StringComparison.Ordinal))
        {
            return;
        }

        conditional.Add(CurrentRaccoonSeedQualifiedItemId());
        conditional.Add("(O)386");
        for (var i = 0; i < 5; i++)
        {
            conditional.Add("(O)SkillBook_" + i);
        }
        conditional.Add("(O)527");
        conditional.Add("(H)40");
    }

    private static string CurrentRaccoonSeedQualifiedItemId()
    {
        var season = Game1.season;
        if (Game1.dayOfMonth > (season == Season.Spring ? 23 : 20))
        {
            season = (Season)(((int)season + 1) % 4);
        }
        return season switch
        {
            Season.Spring => "(O)CarrotSeeds",
            Season.Summer => "(O)SummerSquashSeeds",
            Season.Fall => "(O)BroccoliSeeds",
            _ => "(O)PowdermelonSeeds"
        };
    }

    private static object ReadRuntimeExtraDropRuleInputs(Monster monster, Farmer player)
    {
        return monster switch
        {
            Bat bat => new { runtime_type = "Bat", haunted_skull = bat.hauntedSkull.Value, cursed_doll = bat.cursedDoll.Value, is_winter = Game1.IsWinter, cc_vault_complete = Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccVault") },
            BigSlime bigSlime => new { runtime_type = "BigSlime", held_item_qualified_id = bigSlime.heldItem.Value?.QualifiedItemId ?? string.Empty },
            Bug bug => new { runtime_type = "Bug", is_armored_bug = bug.isArmoredBug.Value },
            DinoMonster => new { runtime_type = "DinoMonster", guaranteed_one_of = true },
            Ghost => new { runtime_type = "Ghost", wizard_order_active = player.team.SpecialOrderActive("Wizard"), ectoplasm_already_received = Game1.MasterPlayer.hasOrWillReceiveMail("ectoplasmDrop") },
            GreenSlime slime => new { runtime_type = "GreenSlime", name = slime.Name, color_r = slime.color.R, color_g = slime.color.G, color_b = slime.color.B, special_number = slime.specialNumber.Value, first_generation = slime.firstGeneration.Value, prismatic = slime.prismatic.Value, slime_hutch_built = Game1.MasterPlayer.mailReceived.Contains("slimeHutchBuilt") },
            MetalHead metalHead => new { runtime_type = "MetalHead", prior_kills = Game1.stats.getMonstersKilled(metalHead.Name), projected_kills_at_drop = Game1.stats.getMonstersKilled(metalHead.Name) + 1, unique_game_id = Game1.uniqueIDForThisGame },
            RockGolem golem => new { runtime_type = "RockGolem", name = golem.Name, season = Game1.season.ToString(), day_of_month = Game1.dayOfMonth, projected_raccoon_seed = CurrentRaccoonSeedQualifiedItemId() },
            Mummy => new { runtime_type = "Mummy" },
            Serpent => new { runtime_type = "Serpent" },
            Skeleton => new { runtime_type = "Skeleton" },
            _ => new { runtime_type = monster.GetType().FullName ?? monster.GetType().Name, vanilla_base_implementation = monster.GetType().Assembly == typeof(Monster).Assembly }
        };
    }

}
