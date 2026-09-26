using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StardewAI.Contracts.Execution
{
    public sealed class CommunityCenterDonationBindingProjection
    {
        public CommunityCenterDonationBindingProjection(
            string bundleDataKey,
            int bundleIngredientIndex,
            int requiredStack)
        {
            BundleDataKey = bundleDataKey;
            BundleIngredientIndex = bundleIngredientIndex;
            RequiredStack = requiredStack;
        }

        public string BundleDataKey { get; }
        public int BundleIngredientIndex { get; }
        public int RequiredStack { get; }
    }

    public sealed class CommunityCenterDonationExecutionProjection
    {
        public CommunityCenterDonationExecutionProjection(
            CommunityCenterDonationBindingProjection binding,
            int bundleId,
            int bundleAreaId,
            string qualifiedItemId)
        {
            Binding = binding;
            BundleId = bundleId;
            BundleAreaId = bundleAreaId;
            QualifiedItemId = qualifiedItemId;
        }

        public CommunityCenterDonationBindingProjection Binding { get; }
        public int BundleId { get; }
        public int BundleAreaId { get; }
        public string QualifiedItemId { get; }
    }

    public static class CommunityCenterDonationParameterProtocol
    {
        public const string BundleDataKey = "bundle_data_key";
        public const string BundleId = "bundle_id";
        public const string BundleAreaId = "bundle_area_id";
        public const string BundleIngredientIndex = "bundle_ingredient_index";
        public const string QualifiedItemId = "qualified_item_id";
        public const string RequiredStack = "required_stack";

        public static bool TryParseBinding(
            IEnumerable<SmallModelActionParameter>? parameters,
            string prefix,
            out CommunityCenterDonationBindingProjection projection)
        {
            projection = null!;
            if (!TryReadUniqueString(
                    parameters,
                    prefix + BundleDataKey,
                    out var bundleDataKey) ||
                !TryReadUniqueInt(
                    parameters,
                    prefix + BundleIngredientIndex,
                    out var ingredientIndex) ||
                ingredientIndex < 0 ||
                !TryReadUniqueInt(
                    parameters,
                    prefix + RequiredStack,
                    out var requiredStack) ||
                requiredStack < 1)
            {
                return false;
            }

            projection = new CommunityCenterDonationBindingProjection(
                bundleDataKey,
                ingredientIndex,
                requiredStack);
            return true;
        }

        public static bool TryParseExecution(
            IEnumerable<SmallModelActionParameter>? parameters,
            out CommunityCenterDonationExecutionProjection projection)
        {
            projection = null!;
            if (!TryParseBinding(parameters, string.Empty, out var binding) ||
                !TryReadUniqueInt(parameters, BundleId, out var bundleId) ||
                bundleId < 0 ||
                !TryReadUniqueInt(parameters, BundleAreaId, out var areaId) ||
                areaId < 0 ||
                !TryReadUniqueString(
                    parameters,
                    QualifiedItemId,
                    out var qualifiedItemId))
            {
                return false;
            }

            projection = new CommunityCenterDonationExecutionProjection(
                binding,
                bundleId,
                areaId,
                qualifiedItemId);
            return true;
        }

        private static bool TryReadUniqueString(
            IEnumerable<SmallModelActionParameter>? parameters,
            string name,
            out string value)
        {
            value = string.Empty;
            var matches = (parameters ?? Array.Empty<SmallModelActionParameter>())
                .Where(parameter => parameter is not null && string.Equals(
                    parameter.Name,
                    name,
                    StringComparison.Ordinal))
                .Select(parameter => parameter!.Value)
                .ToArray();
            if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0]))
                return false;
            value = matches[0];
            return true;
        }

        private static bool TryReadUniqueInt(
            IEnumerable<SmallModelActionParameter>? parameters,
            string name,
            out int value)
        {
            value = 0;
            return TryReadUniqueString(parameters, name, out var text) &&
                int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value);
        }
    }
}
