using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure;

public static partial class MasterAnglerWindowIntentValidator
{
    public static bool TryBuildRainTotemReserveIntent(
        SnapshotEnvelope snapshot,
        string windowIndexPath,
        string windowIndexSha256,
        out SmallModelActionParameter[] parameters,
        out MasterAnglerRainTotemReserveIntentValidation validation,
        out string rejectionReason)
    {
        parameters = [];
        validation = new();
        rejectionReason = string.Empty;
        if (!TryLoadArtifact(
                windowIndexPath,
                windowIndexSha256,
                out var artifact,
                out rejectionReason))
        {
            return false;
        }

        if (!TryProjectRainTotemPineTarReserve(
                snapshot,
                artifact,
                Path.GetFullPath(windowIndexPath),
                windowIndexSha256,
                out validation,
                out rejectionReason))
        {
            if (rejectionReason is not (
                    "master_angler_weather_reserve_pine_tar_inventory_already_sufficient" or
                    "master_angler_weather_reserve_existing_pine_tar_capacity_sufficient"))
            {
                return false;
            }
            if (TryBuildRainTotemTruffleOilReserveIntent(
                snapshot,
                artifact,
                Path.GetFullPath(windowIndexPath),
                windowIndexSha256,
                out parameters,
                out validation,
                out rejectionReason))
            {
                return true;
            }
            if (rejectionReason is not (
                    "master_angler_truffle_oil_inventory_already_sufficient" or
                    "master_angler_truffle_oil_existing_machine_output_sufficient"))
            {
                return false;
            }
            return TryBuildRainTotemHardwoodReserveIntent(
                snapshot,
                artifact,
                Path.GetFullPath(windowIndexPath),
                windowIndexSha256,
                out parameters,
                out validation,
                out rejectionReason);
        }

        parameters =
        [
            Parameter(WeatherReserveStatusParameter, WeatherReserveValidationStatus),
            Parameter(WeatherReserveTargetDayParameter, validation.TargetTotalDay.ToString(CultureInfo.InvariantCulture)),
            Parameter(WeatherReserveUseDayParameter, validation.UseTotalDay.ToString(CultureInfo.InvariantCulture)),
            Parameter(WeatherReserveTargetIdsParameter, JsonSerializer.Serialize(validation.TargetQualifiedItemIds)),
            Parameter(WeatherReserveAffectedContextParameter, validation.AffectedLocationContextId),
            Parameter(WeatherReserveWindowIndexPathParameter, validation.WindowIndexPath),
            Parameter(WeatherReserveWindowIndexSha256Parameter, validation.WindowIndexSha256),
            Parameter(WeatherReserveMaterialIdParameter, PineTarQualifiedItemId),
            Parameter(WeatherReserveRequiredQuantityParameter, PineTarRequiredQuantity.ToString(CultureInfo.InvariantCulture)),
            Parameter(WeatherReserveInventoryQuantityParameter, validation.InventoryPineTarQuantity.ToString(CultureInfo.InvariantCulture)),
            Parameter(WeatherReserveExistingOutputQuantityParameter, validation.ExistingTapperProjectedOutputQuantity.ToString(CultureInfo.InvariantCulture)),
            Parameter(WeatherReserveMachineIdParameter, TapperQualifiedItemId),
            Parameter(WeatherReserveSourceIdParameter, PineTapperSourceId),
            Parameter(WeatherReserveCycleDaysParameter, PineTapperCycleDays.ToString(CultureInfo.InvariantCulture)),
            Parameter(WeatherReserveOutputPerNewMachineParameter, validation.OutputQuantityPerNewTapper.ToString(CultureInfo.InvariantCulture)),
            Parameter(WeatherReserveRequiredMachineCountParameter, validation.RequiredAdditionalTapperCount.ToString(CultureInfo.InvariantCulture)),
            Parameter(WeatherReserveReplanRequiredParameter, "true")
        ];
        if (TryValidateRainTotemReserveIntent(
                snapshot,
                parameters,
                out validation,
                out rejectionReason))
        {
            return true;
        }

        parameters = [];
        return false;
    }
}
