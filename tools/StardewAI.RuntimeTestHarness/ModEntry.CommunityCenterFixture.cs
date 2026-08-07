using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupCommunityCenterDonationFixture(TrainingExecutionRequest request)
    {
        if (request.CommunityCenterFixtureCase is not ("ordinary" or "complete_bundle" or "complete_area" or "complete_all_areas" or "complete_bulletin_area"))
        {
            return BlockedWithPrimitive(request, "debug_setup_community_center_donation", "community_center.fixture=ready", "fixture_case=invalid", "community_center_fixture_case_invalid");
        }
        if (Game1.getLocationFromName("CommunityCenter") is not CommunityCenter communityCenter)
        {
            return BlockedWithPrimitive(request, "debug_setup_community_center_donation", "community_center.fixture=ready", "location=missing", "community_center_fixture_location_missing");
        }
        if (Game1.activeClickableMenu is not null)
        {
            Game1.exitActiveMenu();
        }
        foreach (var mutex in communityCenter.bundleMutexes)
        {
            if (mutex.IsLockHeld())
            {
                mutex.ReleaseLock();
            }
        }

        var desiredArea = request.CommunityCenterFixtureCase == "complete_bulletin_area" ? 5 : 1;
        var target = FindCommunityCenterFixtureTarget(communityCenter, desiredArea);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_community_center_donation", "community_center.fixture=ready", "target=unavailable", "community_center_fixture_dynamic_target_unavailable");
        }

        foreach (var pair in communityCenter.bundles.Pairs)
        {
            for (var index = 0; index < pair.Value.Length; index++)
            {
                communityCenter.bundles.FieldDict[pair.Key][index] = false;
            }
            communityCenter.bundleRewards[pair.Key] = false;
        }
        for (var area = 0; area < communityCenter.areasComplete.Count; area++)
        {
            communityCenter.areasComplete[area] = false;
        }

        var targetBits = communityCenter.bundles.FieldDict[target.BundleId];
        if (request.CommunityCenterFixtureCase != "ordinary")
        {
            var needed = target.RequiredSlots - 1;
            for (var index = 0; index < targetBits.Count && needed > 0; index++)
            {
                if (index == target.IngredientIndex)
                {
                    continue;
                }
                targetBits[index] = true;
                needed--;
            }
            if (needed != 0)
            {
                return BlockedWithPrimitive(request, "debug_setup_community_center_donation", "community_center.fixture=ready", "target_bits=insufficient", "community_center_fixture_required_slots_unavailable");
            }
        }
        if (request.CommunityCenterFixtureCase is "complete_area" or "complete_all_areas" or "complete_bulletin_area")
        {
            foreach (var bundleId in RuntimeCommunityCenterAreaBundleIds(target.AreaId).Where(id => id != target.BundleId))
            {
                if (!communityCenter.bundles.FieldDict.TryGetValue(bundleId, out var bits))
                {
                    return BlockedWithPrimitive(request, "debug_setup_community_center_donation", "community_center.fixture=ready", "area_bundle=missing", "community_center_fixture_area_bundle_missing");
                }
                for (var index = 0; index < bits.Count; index++)
                {
                    bits[index] = true;
                }
            }
        }
        if (request.CommunityCenterFixtureCase == "complete_all_areas")
        {
            foreach (var pair in communityCenter.bundles.FieldDict.Where(pair => !RuntimeCommunityCenterAreaBundleIds(target.AreaId).Contains(pair.Key)))
            {
                for (var index = 0; index < pair.Value.Count; index++)
                {
                    pair.Value[index] = true;
                }
            }
            for (var area = 0; area < communityCenter.areasComplete.Count; area++)
            {
                communityCenter.areasComplete[area] = area != target.AreaId;
            }
        }
        while (!communityCenter.shouldNoteAppearInArea(target.AreaId))
        {
            var additional = communityCenter.bundles.FieldDict
                .Where(pair => !RuntimeCommunityCenterAreaBundleIds(target.AreaId).Contains(pair.Key))
                .FirstOrDefault(pair => pair.Value.Any(value => !value));
            if (additional.Value is null)
            {
                return BlockedWithPrimitive(request, "debug_setup_community_center_donation", "community_center.fixture=ready", "note_threshold=unavailable", "community_center_fixture_note_threshold_unavailable");
            }
            for (var index = 0; index < additional.Value.Count; index++)
            {
                additional.Value[index] = true;
            }
        }

        foreach (var mailId in new[] { "JojaMember", "ccIsComplete", "ccPantry", "ccCraftsRoom", "ccFishTank", "ccBoilerRoom", "ccVault", "ccBulletin", "ccBulletinThankYou" })
        {
            Game1.player.mailReceived.Remove(mailId);
            Game1.MasterPlayer.mailReceived.Remove(mailId);
            RemovePendingCommunityCenterMail(Game1.player, mailId);
            if (!ReferenceEquals(Game1.player, Game1.MasterPlayer))
            {
                RemovePendingCommunityCenterMail(Game1.MasterPlayer, mailId);
            }
        }
        Game1.player.mailReceived.Add("canReadJunimoText");

        if (!communityCenter.isJunimoNoteAtArea(target.AreaId))
        {
            communityCenter.addJunimoNote(target.AreaId);
        }
        var noteTile = CommunityCenterNoteTileRuntime(communityCenter, target.AreaId);
        var interactionTile = CommunityCenterInteractionTileRuntime(communityCenter, target.AreaId, noteTile);
        var standTile = interactionTile.HasValue ? CommunityCenterFixtureStandTile(communityCenter, interactionTile.Value) : null;
        var slot = request.InventorySlotIndex ?? 11;
        if (!noteTile.HasValue || !interactionTile.HasValue || !standTile.HasValue || slot < 0 || slot >= Game1.player.Items.Count ||
            !communityCenter.shouldNoteAppearInArea(target.AreaId) || !communityCenter.isJunimoNoteAtArea(target.AreaId))
        {
            return BlockedWithPrimitive(request, "debug_setup_community_center_donation", "community_center.fixture=ready", "note_or_slot=unavailable", "community_center_fixture_note_or_slot_unavailable");
        }

        Game1.player.Items[slot] = ItemRegistry.Create(target.QualifiedItemId, target.RequiredStack + 2, target.Quality);
        Game1.currentLocation = communityCenter;
        Game1.player.currentLocation = communityCenter;
        Game1.player.Position = standTile.Value.ToVector2() * Game1.tileSize;
        Game1.player.forceCanMove();
        Game1.player.CurrentToolIndex = slot;

        var installedItem = Game1.player.Items[slot];
        var verified = installedItem is not null && installedItem.QualifiedItemId == target.QualifiedItemId &&
            installedItem.Stack == target.RequiredStack + 2 && !communityCenter.bundles[target.BundleId][target.IngredientIndex] &&
            ReferenceEquals(Game1.currentLocation, communityCenter) && Game1.player.TilePoint == standTile.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "debug_setup_community_center_donation",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "dynamic_live_BundleData_fixture_installed", "case=" + request.CommunityCenterFixtureCase, "bundle=" + target.BundleId, "area=" + target.AreaId, "ingredient=" + target.IngredientIndex }
                : new[] { "community_center_fixture_projection_mismatch" },
            RequestedEffect = "community_center.fixture=ready",
            ObservedEffect = "case=" + request.CommunityCenterFixtureCase + ";bundle=" + target.BundleId + ";area=" + target.AreaId + ";slot=" + slot,
            TargetLocation = communityCenter.NameOrUniqueName,
            TargetTileX = interactionTile.Value.X,
            TargetTileY = interactionTile.Value.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "community_center_fixture_projection_mismatch" }
        };
    }

    private static CommunityCenterFixtureTarget? FindCommunityCenterFixtureTarget(CommunityCenter communityCenter, int desiredArea)
    {
        return Game1.netWorldState.Value.BundleData
            .Select(pair => TryCreateCommunityCenterFixtureTarget(pair.Key, pair.Value, communityCenter))
            .Where(target => target?.AreaId == desiredArea)
            .OrderBy(target => target!.BundleId)
            .FirstOrDefault();
    }

    private static CommunityCenterFixtureTarget? TryCreateCommunityCenterFixtureTarget(
        string dataKey,
        string raw,
        CommunityCenter communityCenter)
    {
        var keyParts = dataKey.Split('/');
        var fields = raw.Split('/');
        if (keyParts.Length < 2 || fields.Length < Bundle.FieldCount || !int.TryParse(keyParts[1], out var bundleId) ||
            !communityCenter.bundles.TryGetValue(bundleId, out var liveBits))
        {
            return null;
        }
        var areaId = CommunityCenter.getAreaNumberFromName(keyParts[0]);
        if (areaId < 0 || areaId >= Math.Min(6, communityCenter.areasComplete.Count))
        {
            return null;
        }
        var parts = ArgUtility.SplitBySpace(fields[Bundle.IngredientsIndex]);
        if (parts.Length % 3 != 0 || liveBits.Length < parts.Length / 3)
        {
            return null;
        }
        var ingredients = new List<BundleIngredientDescription>();
        for (var index = 0; index < parts.Length / 3; index++)
        {
            if (!int.TryParse(parts[index * 3 + 1], out var stack) || stack < 1 ||
                !int.TryParse(parts[index * 3 + 2], out var quality) || quality < 0)
            {
                return null;
            }
            ingredients.Add(new BundleIngredientDescription(parts[index * 3], stack, quality, completed: false));
        }
        var requiredSlots = ArgUtility.GetInt(fields, Bundle.NumberOfSlotsIndex, ingredients.Count);
        if (requiredSlots < 2 || requiredSlots > ingredients.Count)
        {
            return null;
        }
        var matcher = new Bundle(fields[Bundle.NameIndex], fields[Bundle.DisplayNameIndex], ingredients, new bool[ingredients.Count], fields[Bundle.RewardIndex]);
        for (var ingredientIndex = 0; ingredientIndex < ingredients.Count; ingredientIndex++)
        {
            var ingredient = ingredients[ingredientIndex];
            foreach (var itemId in Game1.objectData.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                Item candidate;
                try
                {
                    candidate = ItemRegistry.Create("(O)" + itemId, ingredient.stack + 2, ingredient.quality);
                }
                catch
                {
                    continue;
                }
                if (matcher.GetBundleIngredientDescriptionIndexForItem(candidate) == ingredientIndex)
                {
                    return new CommunityCenterFixtureTarget(bundleId, areaId, ingredientIndex, candidate.QualifiedItemId, ingredient.stack, ingredient.quality, requiredSlots);
                }
            }
        }
        return null;
    }

    private static Point? CommunityCenterFixtureStandTile(CommunityCenter communityCenter, Point noteTile)
    {
        foreach (var tile in new[]
        {
            new Point(noteTile.X, noteTile.Y - 1),
            new Point(noteTile.X + 1, noteTile.Y),
            new Point(noteTile.X, noteTile.Y + 1),
            new Point(noteTile.X - 1, noteTile.Y)
        })
        {
            if (IsTileOnMap(communityCenter, tile) && IsTileWalkable(communityCenter, tile) && !IsTileOccupiedByCharacter(communityCenter, tile))
            {
                return tile;
            }
        }
        return null;
    }

    private static void RemovePendingCommunityCenterMail(Farmer farmer, string mailId)
    {
        foreach (var value in farmer.mailForTomorrow.Where(value =>
            string.Equals(value, mailId, StringComparison.Ordinal) ||
            value.StartsWith(mailId + "%&NL&%", StringComparison.Ordinal)).ToArray())
        {
            farmer.mailForTomorrow.Remove(value);
        }
    }

    private sealed record CommunityCenterFixtureTarget(
        int BundleId,
        int AreaId,
        int IngredientIndex,
        string QualifiedItemId,
        int RequiredStack,
        int Quality,
        int RequiredSlots);
}
