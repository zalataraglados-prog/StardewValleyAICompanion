using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

internal static partial class NativeClearanceToolObservationHooks
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        Rewrite(instructions, original);

    private static List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        // Fingerprints are ORIGINAL method-body IL bytes from the pinned actual vanilla1.6.15 DLL.
        // They deliberately reject another build rather than treating a similar pattern as evidence.
        string fingerprint = original.DeclaringType == typeof(Axe)
            ? "ffa3a1b9d40a09c93e6af3c1ae60ec00f6eac9781f85ac19646c3c330260569f"
            : original.DeclaringType == typeof(Pickaxe)
                ? "4f3e19cb09c988e9dd29652e45cb8e26f9cf27f591233344f6292d0168ff7033"
                : throw new InvalidOperationException("Unsupported native tool method.");
        var body = original.GetMethodBody() ?? throw new InvalidOperationException("Native tool body unavailable.");
        if (original.Name != nameof(Tool.DoFunction) || body.ExceptionHandlingClauses.Count != 0 ||
            Convert.ToHexString(SHA256.HashData(body.GetILAsByteArray()!)).ToLowerInvariant() != fingerprint)
            throw new InvalidOperationException("Native tool IL does not match the pinned vanilla body.");

        var source = instructions.ToList();
        var expected = PatchProcessor.GetOriginalInstructions(original);
        ValidateStream(source, expected);
        var code = source.Select(instruction => new CodeInstruction(instruction)).ToList();
        var baseMethod = AccessTools.DeclaredMethod(typeof(Tool), nameof(Tool.DoFunction));
        int baseCall = SingleIndex(code, instruction => instruction.opcode == OpCodes.Call && Equals(instruction.operand, baseMethod));
        int input = baseCall + (original.DeclaringType == typeof(Pickaxe) ? 5 : 1);
        if (original.DeclaringType == typeof(Pickaxe) &&
            (code[baseCall + 1].opcode != OpCodes.Ldarg_S || Convert.ToInt32(code[baseCall + 1].operand) != 5 ||
             code[baseCall + 2].opcode != OpCodes.Ldfld || !Equals(code[baseCall + 2].operand, AccessTools.Field(typeof(Farmer), nameof(Farmer.toolPower))) ||
             code[baseCall + 3].opcode != OpCodes.Callvirt || code[baseCall + 4].opcode != OpCodes.Starg_S ||
             Convert.ToInt32(code[baseCall + 4].operand) != 4))
            throw new InvalidOperationException("Native Pickaxe power-copy boundary changed.");
        if (code[input].opcode != OpCodes.Ldarg_0 || code[input + 1].opcode != OpCodes.Ldfld ||
            !Equals(code[input + 1].operand, AccessTools.Field(typeof(Tool), nameof(Tool.isEfficient))) ||
            code[input + 2].opcode != OpCodes.Callvirt || code[input + 3].opcode != OpCodes.Brtrue_S ||
            code[input + 3].operand is not Label mergeLabel)
            throw new InvalidOperationException("Native Efficient boundary changed.");
        int setter = SingleIndex(code, instruction => instruction.opcode == OpCodes.Callvirt &&
            Equals(instruction.operand, AccessTools.PropertySetter(typeof(Farmer), nameof(Farmer.Stamina))));
        int merge = SingleIndex(code, instruction => instruction.labels.Contains(mergeLabel));
        if (setter <= input + 3 || merge != setter + 1)
            throw new InvalidOperationException("Native stamina setter/conditional merge changed.");

        var result = new List<CodeInstruction>(code.Count + 13);
        for (int index = 0; index < code.Count; index++)
        {
            if (index == input) AddCall(result, code[index], nameof(CaptureInput), withPower: true);
            // Transfer the Efficient branch target onto CompleteDebit, NOT AfterSetter.
            if (index == merge) AddCall(result, code[index], nameof(CompleteDebit), withPower: false);
            result.Add(code[index]);
            if (index == setter) AddCall(result, null, nameof(AfterSetter), withPower: false);
        }
        return result;
    }

    private static int SingleIndex(List<CodeInstruction> code, Func<CodeInstruction, bool> predicate)
    {
        var matches = code.Select((instruction, index) => (instruction, index)).Where(row => predicate(row.instruction)).ToArray();
        return matches.Length == 1 ? matches[0].index : throw new InvalidOperationException("Native tool IL boundary is not unique.");
    }

    private static void AddCall(List<CodeInstruction> output, CodeInstruction? target, string name, bool withPower)
    {
        var first = new CodeInstruction(OpCodes.Ldarg_0);
        if (target is not null)
        {
            first.labels.AddRange(target.labels);
            target.labels.Clear();
        }
        output.Add(first);
        output.Add(new CodeInstruction(OpCodes.Ldarg_1));
        output.Add(new CodeInstruction(OpCodes.Ldarg_S, (byte)5));
        if (withPower) output.Add(new CodeInstruction(OpCodes.Ldarg_S, (byte)4));
        output.Add(new CodeInstruction(OpCodes.Call, Hook(name)));
    }

    private static void ValidateStream(List<CodeInstruction> actual, List<CodeInstruction> expected)
    {
        if (actual.Count != expected.Count || actual.Any(instruction => instruction.blocks.Count != 0))
            throw new InvalidOperationException("Native tool instruction/exception structure was modified.");
        int LabelIndex(List<CodeInstruction> code, Label label) => SingleIndex(code, instruction => instruction.labels.Contains(label));
        bool SameOperand(object? a, object? b)
        {
            if (a is Label left && b is Label right) return LabelIndex(actual, left) == LabelIndex(expected, right);
            if (a is Label[] lefts && b is Label[] rights) return lefts.Length == rights.Length &&
                lefts.Select(label => LabelIndex(actual, label)).SequenceEqual(rights.Select(label => LabelIndex(expected, label)));
            if (a is LocalBuilder localA && b is LocalBuilder localB)
                return localA.LocalIndex == localB.LocalIndex && localA.LocalType == localB.LocalType;
            return Equals(a, b);
        }
        for (int index = 0; index < actual.Count; index++)
            if (actual[index].opcode != expected[index].opcode || !SameOperand(actual[index].operand, expected[index].operand))
                throw new InvalidOperationException("Native tool instruction stream differs from its original body.");
    }
}
