namespace StardewAI.KnowledgeCompiler;

internal sealed class HandlerOperationCatalogBuilder
{
    public HandlerOperationCatalog Build(HandlerOperationIndex index)
    {
        var fieldReads = Catalog(index.Rules.SelectMany(row => row.FieldReads));
        var fieldWrites = Catalog(index.Rules.SelectMany(row => row.FieldWrites));
        var propertyReads = Catalog(index.Rules.SelectMany(row => row.PropertyReads));
        var propertyWrites = Catalog(index.Rules.SelectMany(row => row.PropertyWrites));
        var directCalls = Catalog(index.Rules.SelectMany(row => row.DirectCalls));
        var dynamicDispatches = Catalog(index.Rules.SelectMany(row => row.DynamicDispatches));
        var externalCalls = Catalog(index.Rules.SelectMany(row => row.ExternalCalls));
        var reflectionCalls = Catalog(index.Rules.SelectMany(row => row.ReflectionCalls));
        var randomSources = Catalog(index.Rules.SelectMany(row => row.RandomSources));

        var rules = index.Rules.Select((row, ruleId) => new CatalogedHandlerOperationRule(
            ruleId,
            row.Identity,
            row.Families,
            row.Keys,
            row.UsageCount,
            row.SourceExamples,
            row.AssemblyName,
            row.AssemblySha256,
            row.ModuleVersionId,
            row.DeclaringType,
            row.MethodName,
            row.MetadataToken,
            row.IlSha256,
            row.SourceCandidates,
            row.Completeness,
            row.ClosureMethodCount,
            Ids(row.FieldReads, fieldReads.Ids),
            Ids(row.FieldWrites, fieldWrites.Ids),
            Ids(row.PropertyReads, propertyReads.Ids),
            Ids(row.PropertyWrites, propertyWrites.Ids),
            Ids(row.DirectCalls, directCalls.Ids),
            Ids(row.DynamicDispatches, dynamicDispatches.Ids),
            Ids(row.ExternalCalls, externalCalls.Ids),
            Ids(row.ReflectionCalls, reflectionCalls.Ids),
            Ids(row.RandomSources, randomSources.Ids),
            row.DecodeFailures)).ToArray();

        return new(
            new OperationStringCatalogs(
                fieldReads.Values,
                fieldWrites.Values,
                propertyReads.Values,
                propertyWrites.Values,
                directCalls.Values,
                dynamicDispatches.Values,
                externalCalls.Values,
                reflectionCalls.Values,
                randomSources.Values),
            rules);
    }

    private static StringCatalog Catalog(IEnumerable<string> values)
    {
        var rows = values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return new(rows, rows.Select((value, index) => (value, index))
            .ToDictionary(row => row.value, row => row.index, StringComparer.Ordinal));
    }

    private static IReadOnlyList<int> Ids(IEnumerable<string> values, IReadOnlyDictionary<string, int> ids) =>
        values.Select(value => ids[value]).Distinct().Order().ToArray();
}

internal sealed record HandlerOperationCatalog(
    OperationStringCatalogs OperationCatalogs,
    IReadOnlyList<CatalogedHandlerOperationRule> Rules);

internal sealed record OperationStringCatalogs(
    IReadOnlyList<string> FieldReads,
    IReadOnlyList<string> FieldWrites,
    IReadOnlyList<string> PropertyReads,
    IReadOnlyList<string> PropertyWrites,
    IReadOnlyList<string> DirectCalls,
    IReadOnlyList<string> DynamicDispatches,
    IReadOnlyList<string> ExternalCalls,
    IReadOnlyList<string> ReflectionCalls,
    IReadOnlyList<string> RandomSources);

internal sealed record CatalogedHandlerOperationRule(
    int RuleId,
    string Identity,
    IReadOnlyList<string> Families,
    IReadOnlyList<string> Keys,
    int UsageCount,
    IReadOnlyList<string> SourceExamples,
    string AssemblyName,
    string AssemblySha256,
    string ModuleVersionId,
    string DeclaringType,
    string MethodName,
    string MetadataToken,
    string IlSha256,
    IReadOnlyList<string> SourceCandidates,
    string Completeness,
    int ClosureMethodCount,
    IReadOnlyList<int> FieldReadIds,
    IReadOnlyList<int> FieldWriteIds,
    IReadOnlyList<int> PropertyReadIds,
    IReadOnlyList<int> PropertyWriteIds,
    IReadOnlyList<int> DirectCallIds,
    IReadOnlyList<int> DynamicDispatchIds,
    IReadOnlyList<int> ExternalCallIds,
    IReadOnlyList<int> ReflectionCallIds,
    IReadOnlyList<int> RandomSourceIds,
    IReadOnlyList<string> DecodeFailures);

internal sealed record StringCatalog(
    IReadOnlyList<string> Values,
    IReadOnlyDictionary<string, int> Ids);
