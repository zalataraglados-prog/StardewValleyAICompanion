namespace StardewAI.KnowledgeExporter;

public sealed class ExporterConfig
{
    public bool Enabled { get; set; } = true;

    public string OutputPath { get; set; } = string.Empty;

    public bool ExportDynamicStringAssets { get; set; } = true;
}
