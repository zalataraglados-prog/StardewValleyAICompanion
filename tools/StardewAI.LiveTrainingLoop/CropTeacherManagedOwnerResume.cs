using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;

namespace StardewAI.LiveTrainingLoop;

/// <summary>Reopens retained evidence through the original owner; never installs a new owner.</summary>
public static class CropTeacherManagedOwnerResume
{
    public static async Task<CropTeacherManagedSeedOwner> OpenAsync(string ownerDirectory, string runId,
        string executorVersion, string backendUrl, string portfolioJournalDirectory,
        CropTeacherNativeSaveBinding saveBinding, Func<string, string, Task> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(saveBinding);
        if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(executorVersion))
            throw new ArgumentException("Retained owner resume requires the expected run and executor identity.");

        // A durable reopen must not trust a caller's cached header or original inputs.
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        var directory = Path.GetFullPath(ownerDirectory);
        RequireExisting(directory, directory: true);
        var headerPath = Path.Combine(directory, "owner-intent.json");
        RequireExisting(headerPath, directory: false);
        var headerHash = PortfolioMutationRequestJournal.HashFile(headerPath);
        var headerText = ContentAddressedJsonArtifactReader.ReadAllText(headerPath);
        var header = JsonSerializer.Deserialize<JsonObject>(headerText, CropTeacherManagedSeedOwner.JsonOptions)
            ?? throw new InvalidDataException("Retained owner header is empty.");
        var backend = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(backendUrl);
        var journal = Path.GetFullPath(portfolioJournalDirectory);
        var binding = CropTeacherManagedSeedOwner.NormalizeNativeSaveBinding(saveBinding)!;
        var retainedBinding = header["native_save_binding"]?.Deserialize<CropTeacherNativeSaveBinding>(CropTeacherManagedSeedOwner.JsonOptions);
        Require(Text(header, "schema_version") == "crop_teacher_managed_seed_owner.v1" &&
                Text(header, "run_id") == runId && Text(header, "executor_version") == executorVersion &&
                Text(header, "backend_url") == backend &&
                SameAbsolutePath(Text(header, "portfolio_journal_directory"), journal) &&
                retainedBinding is not null && SameAbsolutePath(retainedBinding.SaveIsolationPath, binding.SaveIsolationPath) &&
                retainedBinding.SaveSlot == binding.SaveSlot,
            "Retained owner differs from the expected run, executor, backend, journal or native save binding.");
        Require(header["selected_refill_resource_effects"]?.GetValue<bool>() == true &&
                header["execution_authorized"]?.GetValue<bool>() == false &&
                header["formal_product_training_authorized"]?.GetValue<bool>() == false,
            "Retained owner resume requires selected resource effects and no execution or training authorization.");

        var options = new CropTeacherManagedSeedOwnerOptions(directory, runId, executorVersion, backend,
            InputPath(header, "native_run_plan_path"), InputPath(header, "initial_snapshot_path"),
            InputPath(header, "initial_ledger_path"), journal)
        {
            UseSelectedRefillResourceEffects = true,
            NativeSaveBinding = binding
        };
        ValidateHeader();
        var owner = await CropTeacherManagedSeedOwner.OpenAsync(options, (path, json) =>
        {
            // If the retained header disappears during open, do not let the installation branch replace it.
            if (SameAbsolutePath(path, headerPath))
                throw new InvalidDataException("Retained owner header disappeared; resume cannot install it.");
            return write(path, json);
        }).ConfigureAwait(false);
        try
        {
            ValidateHeader();
            return owner;
        }
        catch
        {
            await owner.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        void ValidateHeader()
        {
            RequireExisting(directory, directory: true);
            RequireExisting(headerPath, directory: false);
            Require(PortfolioMutationRequestJournal.HashFile(headerPath) == headerHash &&
                    ContentAddressedJsonArtifactReader.ReadAllText(headerPath) == headerText &&
                    PortfolioMutationRequestJournal.HashFile(headerPath) == headerHash,
                "Retained owner header changed while reopening.");
        }
    }

    private static string InputPath(JsonObject header, string name)
    {
        var path = Text(header, name);
        Require(Path.IsPathFullyQualified(path), "Retained owner input must be absolute: " + name);
        path = Path.GetFullPath(path);
        RequireExisting(path, directory: false);
        return path;
    }

    private static string Text(JsonObject value, string name) => value[name]?.GetValue<string>()
        ?? throw new InvalidDataException("Retained owner header lacks " + name + ".");

    private static bool SameAbsolutePath(string first, string second) => Path.IsPathFullyQualified(first) &&
        (OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
        .Equals(Path.GetFullPath(first), Path.GetFullPath(second));

    private static void RequireExisting(string path, bool directory)
    {
        Require(directory ? Directory.Exists(path) : File.Exists(path), "Retained owner path is missing or has the wrong type: " + path);
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0,
                "Retained owner paths must not cross reparse points: " + current);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
