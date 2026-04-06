using System;
using System.Collections.Generic;

namespace TopSpeed.Data
{
    public static partial class TrackCatalog
    {
        public static IReadOnlyDictionary<string, TrackData> BuiltIn => BuiltInMap.Value;

        private static readonly Lazy<IReadOnlyDictionary<string, TrackData>> BuiltInMap =
            new Lazy<IReadOnlyDictionary<string, TrackData>>(() => new Dictionary<string, TrackData>(StringComparer.Ordinal)
            {
                ["america"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrAmerica!),
                ["austria"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrAustria!),
                ["belgium"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrBelgium!),
                ["brazil"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrBrazil!),
                ["china"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrChina!),
                ["england"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrEngland!),
                ["finland"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrFinland!),
                ["france"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrFrance!),
                ["germany"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrGermany!),
                ["ireland"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrIreland!),
                ["italy"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrItaly!),
                ["netherlands"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrNetherlands!),
                ["portugal"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrPortugal!),
                ["russia"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrRussia!),
                ["spain"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrSpain!),
                ["sweden"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrSweden!),
                ["switserland"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrSwitserland!),
                ["advHills"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrAdvHills!),
                ["advCoast"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrAdvCoast!),
                ["advCountry"] = BuiltInTrack(TrackWeather.Rain, TrackAmbience.NoAmbience, TrAdvCountry!),
                ["advAirport"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.Airport, TrAirport!),
                ["advDesert"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.Desert, TrDesert!),
                ["advRush"] = BuiltInTrack(TrackWeather.Sunny, TrackAmbience.NoAmbience, TrAdvRush!),
                ["advEscape"] = BuiltInTrack(TrackWeather.Wind, TrackAmbience.NoAmbience, TrAdvEscape!)
            });

        private static TrackData BuiltInTrack(TrackWeather weather, TrackAmbience ambience, TrackDefinition[] definitions)
        {
            return new TrackData(
                userDefined: false,
                defaultWeatherProfileId: TrackWeatherProfile.DefaultProfileId,
                weatherProfiles: new Dictionary<string, TrackWeatherProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    [TrackWeatherProfile.DefaultProfileId] = TrackWeatherProfile.CreatePreset(TrackWeatherProfile.DefaultProfileId, weather)
                },
                ambience: ambience,
                definitions: definitions);
        }
    }
}
