using System;
using System.Collections.Generic;
using TopSpeed.Common;
using TopSpeed.Core;
using TopSpeed.Data;
using TopSpeed.Localization;
using TopSpeed.Vehicles;
using TopSpeed.Vehicles.Parsing;

namespace TopSpeed.Menu
{
    internal sealed partial class MenuRegistry
    {
        private void CompleteTransmission(RaceMode mode, TransmissionMode transmission)
        {
            if (!SupportsTransmissionMode(transmission))
            {
                _ui.SpeakMessage(LocalizationService.Mark("This vehicle does not support the selected transmission mode."));
                return;
            }

            _setup.Transmission = transmission;
            _race.QueueRaceStart(mode);
        }

        private MenuScreen BuildTransmissionMenu(string id, RaceMode mode)
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Automatic"), MenuAction.None, onActivate: () => CompleteTransmission(mode, TransmissionMode.Automatic)),
                new MenuItem(LocalizationService.Mark("Manual"), MenuAction.None, onActivate: () => CompleteTransmission(mode, TransmissionMode.Manual)),
                new MenuItem(LocalizationService.Mark("Random"), MenuAction.None, onActivate: () => CompleteTransmission(mode, PickRandomSupportedTransmissionMode()))
            };
            return BackMenu(id, items, LocalizationService.Mark("Select transmission mode"));
        }

        private TransmissionMode PickRandomSupportedTransmissionMode()
        {
            if (!TryGetSelectedTransmissionOptions(out _, out var supportedTypes))
                return TransmissionMode.Automatic;

            var supportsAutomatic = TransmissionSelect.SupportsAutomatic(supportedTypes);
            var supportsManual = TransmissionSelect.SupportsManual(supportedTypes);
            if (supportsAutomatic && supportsManual)
                return Algorithm.RandomInt(2) == 0 ? TransmissionMode.Automatic : TransmissionMode.Manual;
            if (supportsManual)
                return TransmissionMode.Manual;
            return TransmissionMode.Automatic;
        }

        private bool SupportsTransmissionMode(TransmissionMode mode)
        {
            if (!TryGetSelectedTransmissionOptions(out var primaryType, out var supportedTypes))
                return false;

            return TransmissionSelect.TryResolveRequested(
                automaticRequested: mode == TransmissionMode.Automatic,
                primary: primaryType,
                supported: supportedTypes,
                out _);
        }

        private bool TryResolveSingleTransmissionMode(out TransmissionMode mode)
        {
            mode = TransmissionMode.Automatic;
            if (!TryGetSelectedTransmissionOptions(out var primaryType, out var supportedTypes))
                return false;

            if (!TransmissionSelect.TryResolveSingleMode(primaryType, supportedTypes, out var automatic))
                return false;

            mode = automatic ? TransmissionMode.Automatic : TransmissionMode.Manual;
            return true;
        }

        private bool TryGetSelectedTransmissionOptions(out TransmissionType primaryType, out TransmissionType[] supportedTypes)
        {
            primaryType = TransmissionType.Atc;
            supportedTypes = new[] { TransmissionType.Atc };

            if (string.IsNullOrWhiteSpace(_setup.VehicleFile))
            {
                var index = _setup.VehicleIndex ?? 0;
                index = Math.Max(0, Math.Min(VehicleCatalog.VehicleCount - 1, index));
                var vehicle = VehicleCatalog.Vehicles[index];
                primaryType = vehicle.PrimaryTransmissionType;
                supportedTypes = vehicle.SupportedTransmissionTypes ?? supportedTypes;
                return true;
            }

            if (!VehicleTsvParser.TryLoadFromFile(_setup.VehicleFile!, out var parsed, out _))
                return false;

            primaryType = parsed.PrimaryTransmissionType;
            supportedTypes = parsed.SupportedTransmissionTypes;
            return true;
        }
    }
}
