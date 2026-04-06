using System;
using System.IO;
using System.Linq;
using TopSpeed.Data;
using Xunit;

namespace TopSpeed.Tests;

[Trait("Category", "Behavior")]
public sealed class TrackWeatherBehaviorTests
{
    [Fact]
    public void Parser_ShouldLoad_Default_And_Segment_Weather_Profiles()
    {
        using var temp = new TemporaryTrackFile(
            """
            [meta]
            name = Weather Test
            version = 1
            weather = clear
            ambience = noambience

            [weather:clear]
            kind = sunny
            longitudinal_wind_mps = 0
            lateral_wind_mps = 0
            air_density = 1.225
            drafting_factor = 1
            temperature_c = 21
            humidity = 0.4
            pressure_kpa = 101.325
            visibility_m = 20000
            rain_gain = 0
            wind_gain = 0
            storm_gain = 0

            [weather:stormfront]
            kind = storm
            longitudinal_wind_mps = 7
            lateral_wind_mps = 12
            air_density = 1.24
            drafting_factor = 0.95
            temperature_c = 14
            humidity = 1
            pressure_kpa = 99.7
            visibility_m = 2200
            rain_gain = 0.2
            wind_gain = 0.4
            storm_gain = 1

            [segment:one]
            type = straight
            surface = asphalt
            noise = none
            length = 100

            [segment:two]
            type = right
            surface = asphalt
            noise = crowd
            length = 120
            weather = stormfront
            weather_transition_seconds = 2.5
            """);

        var loaded = TrackTsmParser.TryLoadFromFile(temp.Path, out var track, out var issues);

        loaded.Should().BeTrue(string.Join(Environment.NewLine, issues.Select(issue => issue.ToString())));
        track.DefaultWeatherProfileId.Should().Be("clear");
        track.WeatherProfiles.Should().ContainKey("clear");
        track.WeatherProfiles.Should().ContainKey("stormfront");
        track.ResolveWeatherProfile("stormfront").LongitudinalWindMps.Should().Be(7f);
        track.ResolveWeatherProfile("stormfront").StormGain.Should().Be(1f);
        track.Definitions[1].WeatherProfileId.Should().Be("stormfront");
        track.Definitions[1].WeatherTransitionSeconds.Should().Be(2.5f);
    }

    [Fact]
    public void Parser_ShouldReject_Legacy_Meta_Weather_Enum_Syntax()
    {
        using var temp = new TemporaryTrackFile(
            """
            [meta]
            weather = sunny
            ambience = noambience

            [segment:one]
            type = straight
            surface = asphalt
            noise = none
            length = 100
            """);

        var loaded = TrackTsmParser.TryLoadFromFile(temp.Path, out _, out var issues);

        loaded.Should().BeFalse();
        issues.Should().Contain(issue => issue.Message.Contains("default weather profile 'sunny'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TrackCatalog_BuiltIns_ShouldExpose_Default_Weather_Profiles()
    {
        TrackCatalog.BuiltIn.Should().NotBeEmpty();
        foreach (var pair in TrackCatalog.BuiltIn)
        {
            pair.Value.DefaultWeatherProfileId.Should().Be(TrackWeatherProfile.DefaultProfileId);
            pair.Value.WeatherProfiles.Should().ContainKey(TrackWeatherProfile.DefaultProfileId);
            pair.Value.DefaultWeatherProfile.Kind.Should().Be(pair.Value.Weather);
        }
    }

    [Fact]
    public void WeatherProfile_Blend_ShouldInterpolate_Environment_And_Audio_Values()
    {
        var calm = TrackWeatherProfile.CreatePreset("calm", TrackWeather.Sunny);
        var storm = new TrackWeatherProfile(
            "storm",
            TrackWeather.Storm,
            longitudinalWindMps: 8f,
            lateralWindMps: 10f,
            airDensityKgPerM3: 1.24f,
            draftingFactor: 0.92f,
            temperatureC: 12f,
            humidity: 1f,
            pressureKpa: 99.5f,
            visibilityM: 1500f,
            rainGain: 0.3f,
            windGain: 0.5f,
            stormGain: 1f);

        var blended = TrackWeatherProfile.Blend(calm, storm, 0.5f);

        blended.LongitudinalWindMps.Should().Be(4f);
        blended.LateralWindMps.Should().Be(5f);
        blended.AirDensityKgPerM3.Should().BeApproximately(1.2325f, 0.0001f);
        blended.RainGain.Should().Be(0.15f);
        blended.StormGain.Should().Be(0.5f);
    }

    private sealed class TemporaryTrackFile : IDisposable
    {
        private readonly string _directory;
        public TemporaryTrackFile(string content)
        {
            _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "topspeed-track-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "track.tsm");
            File.WriteAllText(Path, content.Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }
}
