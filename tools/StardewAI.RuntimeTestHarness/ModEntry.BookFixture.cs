using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupBookFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var fixtureBranch = request.BookNativeBranch;
        var book = FindFixtureBook(fixtureBranch);
        if (book is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_book_fixture",
                "player.book_candidates[fixture].native_branch=" +
                    fixtureBranch,
                "book=unresolved",
                "book_fixture_branch_unavailable");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        Game1.exitActiveMenu();
        Game1.player.canMove = true;
        Game1.eventUp = false;
        EnsureFixtureInventoryCapacity(Game1.player);
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            if (Game1.player.Items[index] is StardewValley.Object existing &&
                existing.Category is StardewValley.Object.booksCategory or
                    StardewValley.Object.skillBooksCategory)
            {
                Game1.player.Items[index] = null;
            }
        }

        var repeated = fixtureBranch is
            "power_book_repeated_skill" or
            "power_book_repeated_all_skills";
        var wellRead = fixtureBranch == "power_book_first_read_well_read";
        if (repeated)
        {
            Game1.player.stats.Set(book.ItemId, 1u);
        }
        else if (!book.ItemId.StartsWith("SkillBook_", StringComparison.Ordinal) &&
                 book.ItemId != "PurpleBook")
        {
            Game1.player.stats.Set(book.ItemId, 0u);
        }

        if (wellRead)
        {
            foreach (var key in WellReadBookStatKeys)
            {
                Game1.player.stats.Set(
                    key,
                    string.Equals(key, book.ItemId, StringComparison.Ordinal)
                        ? 0u
                        : 1u);
            }
            Game1.player.achievements.Remove(35);
        }

        book.Stack = 1;
        var slot = InstallFixtureItem(Game1.player, book);
        var expectedBranch = wellRead
            ? "power_book_first_read"
            : fixtureBranch;
        var tags = book.GetContextTags().ToArray();
        var actualBranch = FixtureBookBranch(
            book,
            Game1.player.stats.Get(book.ItemId),
            tags);
        var verified =
            slot >= 0 &&
            string.Equals(
                actualBranch,
                expectedBranch,
                StringComparison.Ordinal) &&
            (!wellRead || !Game1.player.achievements.Contains(35));

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_book_fixture",
            PrimitiveVerificationStatus =
                verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_runtime_book_fixture_installed",
                    "book_branch=" + actualBranch,
                    "book_slot=" + slot
                }
                : new[] { "book_fixture_projection_mismatch" },
            RequestedEffect =
                "player.book_candidates[fixture].native_branch=" +
                expectedBranch,
            ObservedEffect =
                "slot_index=" + slot +
                ";qualified_item_id=" + book.QualifiedItemId +
                ";native_branch=" + actualBranch +
                ";already_read=" + Game1.player.stats.Get(book.ItemId),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "book_fixture_projection_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "player.inventory[" + slot + "]",
                        Before = "fixture_slot",
                        After = book.QualifiedItemId + "x1"
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static StardewValley.Object? FindFixtureBook(
        string fixtureBranch)
    {
        if (fixtureBranch == "skill_book")
        {
            return ItemRegistry.Create("(O)SkillBook_3") as
                StardewValley.Object;
        }
        if (fixtureBranch == "purple_book")
        {
            return ItemRegistry.Create("(O)PurpleBook") as
                StardewValley.Object;
        }
        if (fixtureBranch == "queen_of_sauce_first_read")
        {
            return ItemRegistry.Create("(O)Book_QueenOfSauce") as
                StardewValley.Object;
        }
        if (fixtureBranch is
            "power_book_first_read" or
            "power_book_first_read_well_read")
        {
            return ItemRegistry.Create("(O)Book_Trash") as
                StardewValley.Object;
        }

        foreach (var itemId in Game1.objectData.Keys
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            if (ItemRegistry.Create("(O)" + itemId) is not
                StardewValley.Object candidate ||
                candidate.Category is not (
                    StardewValley.Object.booksCategory or
                    StardewValley.Object.skillBooksCategory) ||
                candidate.ItemId.StartsWith(
                    "SkillBook_",
                    StringComparison.Ordinal) ||
                candidate.ItemId is
                    "PurpleBook" or
                    "Book_QueenOfSauce" or
                    "Book_PriceCatalogue" or
                    "Book_AnimalCatalogue")
            {
                continue;
            }

            var tagged = candidate.GetContextTags().Any(tag =>
                tag.StartsWith(
                    "book_xp_",
                    StringComparison.OrdinalIgnoreCase));
            if ((fixtureBranch == "power_book_repeated_skill" && tagged) ||
                (fixtureBranch == "power_book_repeated_all_skills" &&
                 !tagged))
            {
                return candidate;
            }
        }
        return null;
    }

    private static string FixtureBookBranch(
        StardewValley.Object book,
        uint alreadyRead,
        string[] tags)
    {
        if (book.ItemId.StartsWith("SkillBook_", StringComparison.Ordinal))
        {
            return "skill_book";
        }
        if (alreadyRead != 0 &&
            book.ItemId is not
                "Book_PriceCatalogue" and
                not "Book_AnimalCatalogue")
        {
            return tags.Any(tag => tag.StartsWith(
                "book_xp_",
                StringComparison.OrdinalIgnoreCase))
                ? "power_book_repeated_skill"
                : "power_book_repeated_all_skills";
        }
        if (book.ItemId == "PurpleBook")
        {
            return "purple_book";
        }
        if (book.ItemId == "Book_QueenOfSauce")
        {
            return "queen_of_sauce_first_read";
        }
        return "power_book_first_read";
    }
}
