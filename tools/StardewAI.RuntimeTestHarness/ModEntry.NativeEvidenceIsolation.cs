using System.Reflection;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void ApplyExternalNativeEvidenceIsolation()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("STARDEWAI_DISABLE_EXTERNAL_GOD_TOOL"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var godToolType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(
                "JunimoTestClient.GameTweaks.GodTool",
                throwOnError: false,
                ignoreCase: false))
            .FirstOrDefault(type => type is not null);
        if (godToolType is null)
        {
            Monitor.Log(
                "Native evidence isolation: no external GodTool type is loaded.",
                StardewModdingAPI.LogLevel.Trace);
            return;
        }

        var enabledProperty = godToolType.GetProperty(
            "Enabled",
            BindingFlags.Public | BindingFlags.Static);
        if (enabledProperty?.CanWrite != true ||
            enabledProperty.PropertyType != typeof(bool))
        {
            throw new InvalidOperationException(
                "Native evidence isolation cannot disable JunimoTestClient.GameTweaks.GodTool.");
        }

        enabledProperty.SetValue(null, false);
        if (!Equals(enabledProperty.GetValue(null), false))
        {
            throw new InvalidOperationException(
                "Native evidence isolation failed to disable JunimoTestClient.GameTweaks.GodTool.");
        }

        Monitor.Log(
            "Native evidence isolation disabled JunimoTestClient GodTool patches.",
            StardewModdingAPI.LogLevel.Info);
    }
}
