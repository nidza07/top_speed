using System;
using TopSpeed.Physics.Powertrain;

namespace TopSpeed.Data
{
    public readonly struct TrackWeatherProfile
    {
        public const string DefaultProfileId = "default";

        public TrackWeatherProfile(
            string id,
            TrackWeather kind,
            float longitudinalWindMps,
            float lateralWindMps,
            float airDensityKgPerM3,
            float draftingFactor,
            float temperatureC,
            float humidity,
            float pressureKpa,
            float visibilityM,
            float rainGain,
            float windGain,
            float stormGain)
        {
            var trimmedId = id?.Trim();
            Id = string.IsNullOrWhiteSpace(trimmedId) ? DefaultProfileId : trimmedId!;
            Kind = kind;
            LongitudinalWindMps = longitudinalWindMps;
            LateralWindMps = lateralWindMps;
            AirDensityKgPerM3 = airDensityKgPerM3 > 0f ? airDensityKgPerM3 : 1.225f;
            DraftingFactor = draftingFactor < 0.1f ? 0.1f : draftingFactor;
            TemperatureC = temperatureC;
            Humidity = Clamp(humidity, 0f, 1f);
            PressureKpa = pressureKpa > 0f ? pressureKpa : 101.325f;
            VisibilityM = visibilityM > 0f ? visibilityM : 20000f;
            RainGain = Clamp(rainGain, 0f, 4f);
            WindGain = Clamp(windGain, 0f, 4f);
            StormGain = Clamp(stormGain, 0f, 4f);
        }

        public string Id { get; }
        public TrackWeather Kind { get; }
        public float LongitudinalWindMps { get; }
        public float LateralWindMps { get; }
        public float AirDensityKgPerM3 { get; }
        public float DraftingFactor { get; }
        public float TemperatureC { get; }
        public float Humidity { get; }
        public float PressureKpa { get; }
        public float VisibilityM { get; }
        public float RainGain { get; }
        public float WindGain { get; }
        public float StormGain { get; }

        public ResistanceEnvironment ToResistanceEnvironment()
        {
            return new ResistanceEnvironment(
                airDensityKgPerM3: AirDensityKgPerM3,
                longitudinalWindMps: LongitudinalWindMps,
                lateralWindMps: LateralWindMps,
                draftingFactor: DraftingFactor);
        }

        public static TrackWeatherProfile Blend(in TrackWeatherProfile from, in TrackWeatherProfile to, float t)
        {
            var blend = Clamp(t, 0f, 1f);
            return new TrackWeatherProfile(
                to.Id,
                blend >= 1f ? to.Kind : from.Kind,
                Lerp(from.LongitudinalWindMps, to.LongitudinalWindMps, blend),
                Lerp(from.LateralWindMps, to.LateralWindMps, blend),
                Lerp(from.AirDensityKgPerM3, to.AirDensityKgPerM3, blend),
                Lerp(from.DraftingFactor, to.DraftingFactor, blend),
                Lerp(from.TemperatureC, to.TemperatureC, blend),
                Lerp(from.Humidity, to.Humidity, blend),
                Lerp(from.PressureKpa, to.PressureKpa, blend),
                Lerp(from.VisibilityM, to.VisibilityM, blend),
                Lerp(from.RainGain, to.RainGain, blend),
                Lerp(from.WindGain, to.WindGain, blend),
                Lerp(from.StormGain, to.StormGain, blend));
        }

        public static TrackWeatherProfile CreatePreset(string id, TrackWeather kind)
        {
            return kind switch
            {
                TrackWeather.Rain => new TrackWeatherProfile(
                    id,
                    kind,
                    longitudinalWindMps: 0f,
                    lateralWindMps: 0f,
                    airDensityKgPerM3: 1.23f,
                    draftingFactor: 1f,
                    temperatureC: 18f,
                    humidity: 0.9f,
                    pressureKpa: 100.6f,
                    visibilityM: 6000f,
                    rainGain: 1f,
                    windGain: 0f,
                    stormGain: 0f),
                TrackWeather.Wind => new TrackWeatherProfile(
                    id,
                    kind,
                    longitudinalWindMps: 2f,
                    lateralWindMps: 6f,
                    airDensityKgPerM3: 1.225f,
                    draftingFactor: 1f,
                    temperatureC: 19f,
                    humidity: 0.5f,
                    pressureKpa: 101f,
                    visibilityM: 16000f,
                    rainGain: 0f,
                    windGain: 1f,
                    stormGain: 0f),
                TrackWeather.Storm => new TrackWeatherProfile(
                    id,
                    kind,
                    longitudinalWindMps: 5f,
                    lateralWindMps: 10f,
                    airDensityKgPerM3: 1.24f,
                    draftingFactor: 1f,
                    temperatureC: 16f,
                    humidity: 1f,
                    pressureKpa: 99.8f,
                    visibilityM: 3500f,
                    rainGain: 0f,
                    windGain: 0f,
                    stormGain: 1f),
                _ => new TrackWeatherProfile(
                    id,
                    TrackWeather.Sunny,
                    longitudinalWindMps: 0f,
                    lateralWindMps: 0f,
                    airDensityKgPerM3: 1.225f,
                    draftingFactor: 1f,
                    temperatureC: 22f,
                    humidity: 0.45f,
                    pressureKpa: 101.325f,
                    visibilityM: 20000f,
                    rainGain: 0f,
                    windGain: 0f,
                    stormGain: 0f)
            };
        }

        private static float Lerp(float from, float to, float t) => from + ((to - from) * t);

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
