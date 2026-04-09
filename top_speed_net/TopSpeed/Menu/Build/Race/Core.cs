using TopSpeed.Core;

namespace TopSpeed.Menu
{
    internal sealed partial class MenuRegistry
    {
        private void PrepareMode(RaceMode mode)
        {
            _setup.Mode = mode;
            _setup.ClearSelection();
        }

        private static string TrackMenuId(RaceMode mode, TrackCategory category)
        {
            var prefix = mode == RaceMode.TimeTrial ? "time_trial" : "single_race";
            return category switch
            {
                TrackCategory.RaceTrack => $"{prefix}_tracks_race",
                TrackCategory.StreetAdventure => $"{prefix}_tracks_adventure",
                _ => $"{prefix}_tracks_custom"
            };
        }

        private static string VehicleMenuId(RaceMode mode)
        {
            return mode == RaceMode.TimeTrial ? "time_trial_vehicles" : "single_race_vehicles";
        }

        private static string CustomVehicleMenuId(RaceMode mode)
        {
            return mode == RaceMode.TimeTrial ? "time_trial_vehicles_custom" : "single_race_vehicles_custom";
        }

        private static string TransmissionMenuId(RaceMode mode)
        {
            return mode == RaceMode.TimeTrial ? "time_trial_transmission" : "single_race_transmission";
        }
    }
}
