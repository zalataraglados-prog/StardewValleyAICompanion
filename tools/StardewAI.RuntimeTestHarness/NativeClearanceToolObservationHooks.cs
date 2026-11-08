using System.Reflection;
using HarmonyLib;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

internal interface INativeClearanceToolCallSink
{
    void Record(NativeClearanceToolCall call);
    void Reject(string diagnostic);
}

internal sealed record NativeClearanceToolCall(
    int Ordinal, Tool Tool, GameLocation Location, Farmer Farmer, int X, int Y,
    int ArgumentPower, int NativePower, long BeforeGameTick, long InputGameTick,
    long DebitGameTick, long AfterGameTick, float StaminaAtEntry, float StaminaBeforeDebit,
    float StaminaAfterDebit, float StaminaAtExit, int MaxStamina, int EffectiveSkillLevel,
    bool IsEfficient, bool HasStaminaDecreaseProtection, bool StaminaSetterInvoked);

// Explicit-request owner installs/opens this helper. It never patches at startup or changes tool logic.
internal static partial class NativeClearanceToolObservationHooks
{
    private const int MaximumCalls = 256;
    private static readonly object InstallationLock = new();
    private static string? installedOwner;
    private static bool installationPoisoned;
    private static Scope? active;
    private static readonly MethodInfo[] Targets =
    {
        AccessTools.DeclaredMethod(typeof(Axe), nameof(Axe.DoFunction)),
        AccessTools.DeclaredMethod(typeof(Pickaxe), nameof(Pickaxe.DoFunction))
    };
    private static MethodInfo Hook(string name) => AccessTools.DeclaredMethod(typeof(NativeClearanceToolObservationHooks), name);

    internal static void Install(Harmony harmony)
    {
        ArgumentNullException.ThrowIfNull(harmony);
        lock (InstallationLock)
        {
            if (installationPoisoned || Volatile.Read(ref active) is not null)
                throw new InvalidOperationException("Tool observation installation is unavailable or already in use.");
            if (installedOwner is not null)
            {
                if (installedOwner != harmony.Id) throw new InvalidOperationException("Another tool observation owner is installed.");
                ValidateInstalled();
                return;
            }
            ValidateDependenciesUnpatched();
            foreach (var target in Targets)
            {
                RequireNoPatches(target);
                // Preflight BOTH originals and rewrites before changing either method.
                Rewrite(PatchProcessor.GetOriginalInstructions(target), target);
            }
            ApplyAtomically(Targets, target =>
            {
                RequireNoPatches(target);
                harmony.Patch(target, prefix: new HarmonyMethod(Hook(nameof(Prefix))),
                    transpiler: new HarmonyMethod(Hook(nameof(Transpiler))),
                    finalizer: new HarmonyMethod(Hook(nameof(Finalizer))));
                ValidateTargetPatches(target, harmony.Id);
            }, target =>
            {
                // Remove only this helper's methods, never all patches owned by the caller.
                foreach (var name in new[] { nameof(Prefix), nameof(Transpiler), nameof(Finalizer) })
                    harmony.Unpatch(target, Hook(name));
            });
            installedOwner = harmony.Id;
        }
    }

    private static void ApplyAtomically(MethodInfo[] targets, Action<MethodInfo> patch, Action<MethodInfo> rollback)
    {
        try { foreach (var target in targets) patch(target); }
        catch (Exception original)
        {
            var failures = new List<Exception> { original };
            // Include the throwing target: Harmony may have changed metadata before throwing.
            foreach (var target in targets.Reverse())
                try { rollback(target); }
                catch (Exception error) { failures.Add(error); }
            if (failures.Count > 1)
            {
                installationPoisoned = true;
                throw new AggregateException("Tool observation rollback failed; admission is permanently disabled.", failures);
            }
            throw;
        }
    }

    private static void RequireNoPatches(MethodBase method)
    {
        var patches = Harmony.GetPatchInfo(method);
        if (patches is not null && patches.Owners.Any())
            throw new InvalidOperationException("Tool observation refuses patched dependency: " + method.Name);
    }

    private static void ValidateDependenciesUnpatched()
    {
        RequireNoPatches(AccessTools.DeclaredMethod(typeof(Tool), nameof(Tool.DoFunction)));
        foreach (var name in new[] { nameof(Farmer.Stamina), nameof(Farmer.MaxStamina), nameof(Farmer.ForagingLevel), nameof(Farmer.MiningLevel) })
            RequireNoPatches(AccessTools.PropertyGetter(typeof(Farmer), name));
        RequireNoPatches(AccessTools.PropertySetter(typeof(Farmer), nameof(Farmer.Stamina)));
        RequireNoPatches(AccessTools.Method(typeof(Farmer), nameof(Farmer.hasBuff), new[] { typeof(string) }));
    }

    private static void ValidateTargetPatches(MethodInfo target, string owner)
    {
        var info = Harmony.GetPatchInfo(target) ?? throw new InvalidOperationException("Tool hooks were not installed.");
        bool Exactly(IEnumerable<Patch> patches, string name) => patches.Count() == 1 &&
            patches.All(patch => patch.owner == owner && patch.PatchMethod == Hook(name));
        if (!Exactly(info.Prefixes, nameof(Prefix)) || !Exactly(info.Transpilers, nameof(Transpiler)) ||
            !Exactly(info.Finalizers, nameof(Finalizer)) || info.Postfixes.Count != 0)
            throw new InvalidOperationException("Tool observation refuses foreign or incomplete target patches.");
    }

    private static void ValidateInstalled()
    {
        if (installedOwner is null || installationPoisoned) throw new InvalidOperationException("Tool hooks are not ready.");
        ValidateDependenciesUnpatched();
        foreach (var target in Targets) ValidateTargetPatches(target, installedOwner);
    }

    internal static IDisposable Begin(INativeClearanceToolCallSink sink, Farmer actor, GameLocation location)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (InstallationLock)
        {
            var previous = Volatile.Read(ref active);
            if (previous is not null)
            {
                previous.Reject("tool_observation_nested_scope");
                SafeReject(sink, "tool_observation_nested_scope");
                return EmptyScope.Instance;
            }
            if (actor is null || location is null)
            { SafeReject(sink, "tool_observation_scope_identity_missing"); return EmptyScope.Instance; }
            try { ValidateInstalled(); }
            catch (Exception error)
            { SafeReject(sink, "tool_observation_hooks_unavailable:" + error.GetType().Name); return EmptyScope.Instance; }
            if (!ReferenceEquals(actor.currentLocation, location))
            { SafeReject(sink, "tool_observation_scope_location_mismatch"); return EmptyScope.Instance; }
            Volatile.Write(ref active, new Scope(sink, actor, location));
            return active!;
        }
    }

    private static void SafeReject(INativeClearanceToolCallSink sink, string diagnostic)
    {
        try { sink.Reject(diagnostic); }
        catch { /* An observer callback must never replace a native result/exception. */ }
    }

    private sealed class EmptyScope : IDisposable
    {
        internal static readonly EmptyScope Instance = new();
        public void Dispose() { }
    }

    private sealed class Scope : IDisposable
    {
        internal readonly INativeClearanceToolCallSink Sink;
        internal readonly Farmer Actor;
        internal readonly GameLocation Location;
        internal readonly int ThreadId = Environment.CurrentManagedThreadId;
        internal Frame? Current;
        internal int Count;
        internal bool InSink;
        private int failed;
        private int disposed;
        internal bool Failed => Volatile.Read(ref failed) != 0;
        internal Scope(INativeClearanceToolCallSink sink, Farmer actor, GameLocation location)
        { Sink = sink; Actor = actor; Location = location; }
        internal void Reject(string diagnostic)
        {
            if (Interlocked.Exchange(ref failed, 1) == 0) SafeReject(Sink, diagnostic);
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            if (ThreadId != Environment.CurrentManagedThreadId || Current is not null || !ReferenceEquals(Volatile.Read(ref active), this))
                Reject("tool_observation_scope_incomplete_or_wrong_thread");
            Interlocked.CompareExchange(ref active, null, this);
        }
    }

    private sealed class Frame
    {
        internal readonly Scope Owner;
        internal readonly Frame? Previous;
        internal readonly Tool Tool;
        internal readonly GameLocation Location;
        internal readonly Farmer Farmer;
        internal readonly int Ordinal, X, Y, ArgumentPower;
        internal long EntryTick, InputTick, DebitTick;
        internal float EntryStamina, InputStamina, DebitStamina;
        internal int NativePower, MaxStamina, Skill;
        internal bool Efficient, Protected, InputRead, SetterRead, Complete;
        internal Frame(Scope owner, Tool tool, GameLocation location, Farmer farmer, int x, int y, int argumentPower)
        {
            Owner = owner; Previous = owner.Current; Tool = tool; Location = location; Farmer = farmer;
            X = x; Y = y; ArgumentPower = argumentPower; Ordinal = ++owner.Count;
        }
    }

    private static Scope? CurrentScope()
    {
        var owner = Volatile.Read(ref active);
        if (owner is null || owner.Failed) return null; // No native reads or allocations outside a scope.
        if (owner.ThreadId != Environment.CurrentManagedThreadId)
        { owner.Reject("tool_observation_wrong_thread"); return null; }
        if (owner.InSink) { owner.Reject("tool_observation_sink_reentrancy"); return null; }
        return owner;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void Prefix(Tool __instance, GameLocation __0, int __1, int __2, int __3, Farmer __4, out Frame? __state)
    {
        __state = null;
        var owner = CurrentScope();
        if (owner is null) return;
        try
        {
            if (__instance is null || (__instance.GetType() != typeof(Axe) && __instance.GetType() != typeof(Pickaxe)) ||
                !ReferenceEquals(__0, owner.Location) || !ReferenceEquals(__4, owner.Actor) ||
                !ReferenceEquals(__4.currentLocation, __0))
            { owner.Reject("tool_observation_invocation_identity_mismatch"); return; }
            if (owner.Count >= MaximumCalls) { owner.Reject("tool_observation_call_limit_exceeded"); return; }
            __state = new Frame(owner, __instance, __0, __4, __1, __2, __3);
            owner.Current = __state;
            __state.EntryTick = Game1.ticks;
            __state.EntryStamina = __4.Stamina;
            if (!Finite(__state.EntryStamina)) owner.Reject("tool_observation_nonfinite_entry");
        }
        catch (Exception error) { owner.Reject("tool_observation_prefix_failed:" + error.GetType().Name); }
    }

    private static Frame? CurrentFrame(Tool tool, GameLocation location, Farmer farmer)
    {
        var owner = CurrentScope();
        if (owner is null) return null;
        var frame = owner.Current;
        if (frame is null || !ReferenceEquals(frame.Tool, tool) || !ReferenceEquals(frame.Location, location) ||
            !ReferenceEquals(frame.Farmer, farmer) || !ReferenceEquals(farmer.currentLocation, location))
        { owner.Reject("tool_observation_debit_context_mismatch"); return null; }
        return frame;
    }

    private static void CaptureInput(Tool tool, GameLocation location, Farmer farmer, int nativePower)
    {
        var owner = CurrentScope();
        if (owner is null) return;
        try
        {
            var frame = CurrentFrame(tool, location, farmer);
            if (frame is null) return;
            if (frame.InputRead) { owner.Reject("tool_observation_duplicate_input"); return; }
            frame.InputTick = Game1.ticks; frame.InputStamina = farmer.Stamina;
            frame.NativePower = nativePower; frame.MaxStamina = farmer.MaxStamina;
            frame.Skill = tool.GetType() == typeof(Axe) ? farmer.ForagingLevel : farmer.MiningLevel;
            frame.Efficient = tool.isEfficient.Value;
            frame.Protected = farmer.hasBuff("statue_of_blessings_2");
            frame.InputRead = true;
            if (!Finite(frame.InputStamina) || frame.InputTick < frame.EntryTick || frame.MaxStamina < 0 || frame.Skill < 0)
                owner.Reject("tool_observation_input_invalid");
        }
        catch (Exception error) { owner.Reject("tool_observation_input_failed:" + error.GetType().Name); }
    }

    private static void AfterSetter(Tool tool, GameLocation location, Farmer farmer)
    {
        var owner = CurrentScope();
        if (owner is null) return;
        try
        {
            var frame = CurrentFrame(tool, location, farmer);
            if (frame is null) return;
            if (!frame.InputRead || frame.SetterRead || frame.Complete || frame.Efficient)
            { owner.Reject("tool_observation_setter_order_invalid"); return; }
            frame.DebitTick = Game1.ticks; frame.DebitStamina = farmer.Stamina; frame.SetterRead = true;
            if (!Finite(frame.DebitStamina) || frame.DebitTick < frame.InputTick) owner.Reject("tool_observation_setter_value_invalid");
        }
        catch (Exception error) { owner.Reject("tool_observation_setter_failed:" + error.GetType().Name); }
    }

    private static void CompleteDebit(Tool tool, GameLocation location, Farmer farmer)
    {
        var owner = CurrentScope();
        if (owner is null) return;
        try
        {
            var frame = CurrentFrame(tool, location, farmer);
            if (frame is null) return;
            if (!frame.InputRead || frame.Complete || frame.SetterRead == frame.Efficient)
            { owner.Reject("tool_observation_debit_incomplete"); return; }
            if (frame.Efficient)
            {
                frame.DebitTick = Game1.ticks; frame.DebitStamina = farmer.Stamina;
                if (BitConverter.SingleToInt32Bits(frame.DebitStamina) != BitConverter.SingleToInt32Bits(frame.InputStamina))
                { owner.Reject("tool_observation_efficient_stamina_changed"); return; }
            }
            frame.Complete = true;
        }
        catch (Exception error) { owner.Reject("tool_observation_merge_failed:" + error.GetType().Name); }
    }

    // Void finalizer observes but never replaces/suppresses the native exception.
    private static void Finalizer(Exception? __exception, Frame? __state)
    {
        if (__state is null) return;
        var owner = __state.Owner;
        try
        {
            if (owner.Failed) return;
            if (__exception is not null) { owner.Reject("tool_observation_native_exception"); return; }
            if (!ReferenceEquals(CurrentScope(), owner) || !ReferenceEquals(owner.Current, __state) || !__state.Complete ||
                !ReferenceEquals(__state.Farmer.currentLocation, owner.Location))
            { owner.Reject("tool_observation_finalizer_incomplete"); return; }
            var tick = (long)Game1.ticks;
            var stamina = __state.Farmer.Stamina;
            if (!Finite(stamina) || tick < __state.DebitTick || __state.DebitTick < __state.InputTick)
            { owner.Reject("tool_observation_exit_invalid"); return; }
            owner.InSink = true;
            owner.Sink.Record(new NativeClearanceToolCall(__state.Ordinal, __state.Tool, __state.Location, __state.Farmer,
                __state.X, __state.Y, __state.ArgumentPower, __state.NativePower, __state.EntryTick, __state.InputTick,
                __state.DebitTick, tick, __state.EntryStamina, __state.InputStamina, __state.DebitStamina, stamina,
                __state.MaxStamina, __state.Skill, __state.Efficient, __state.Protected, __state.SetterRead));
        }
        catch (Exception error) { owner.Reject("tool_observation_finalizer_failed:" + error.GetType().Name); }
        finally { owner.InSink = false; owner.Current = __state.Previous; }
    }
}
