using System;
using System.Collections.Generic;
using System.Numerics;
using TopSpeed.Tracks.Areas;
using TopSpeed.Tracks.Map;
using TopSpeed.Tracks.Geometry;

namespace TopSpeed.Tracks.Surfaces
{
    internal sealed class TrackSurfaceSystem
    {
        private readonly List<TrackSurfaceMesh> _surfaces;
        private readonly SurfaceCellIndex _surfaceIndex;

        public TrackSurfaceSystem(TrackMap map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            _surfaces = new List<TrackSurfaceMesh>();
            _surfaceIndex = new SurfaceCellIndex(Math.Max(0.5f, map.SurfaceResolutionMeters));

            var geometriesById = BuildGeometryLookup(map.Geometries);
            var profilesById = BuildProfileLookup(map.Profiles);
            var banksById = BuildBankLookup(map.Banks);
            var surfaceDefaults = BuildSurfaceDefaults(map.Areas, map.DefaultWidthMeters);

            var defaultResolution = Math.Max(0.25f, map.SurfaceResolutionMeters);
            foreach (var surface in map.Surfaces)
            {
                if (surface == null)
                    continue;
                if (string.IsNullOrWhiteSpace(surface.GeometryId))
                    continue;
                if (!geometriesById.TryGetValue(surface.GeometryId, out var geometry))
                    continue;

                profilesById.TryGetValue(surface.ProfileId ?? string.Empty, out var profile);
                banksById.TryGetValue(surface.BankId ?? string.Empty, out var bank);

                var baseHeight = map.BaseHeightMeters;
                SurfaceDefaults defaults;
                if (SurfaceParameterParser.TryGetFloat(surface.Metadata, out var baseValue, "base_height", "elevation", "height", "y"))
                    baseHeight = baseValue;
                else if (surfaceDefaults.TryGetValue(surface.Id, out defaults) && defaults.BaseHeight.HasValue)
                    baseHeight = defaults.BaseHeight.Value;

                var defaultWidth = map.DefaultWidthMeters;
                if (surfaceDefaults.TryGetValue(surface.Id, out defaults) && defaults.Width.HasValue)
                    defaultWidth = defaults.Width.Value;

                var surfaceResolution = surface.ResolutionMeters ??
                                        (surfaceDefaults.TryGetValue(surface.Id, out defaults) && defaults.Resolution.HasValue
                                            ? defaults.Resolution.Value
                                            : defaultResolution);

                var profileEvaluator = SurfaceProfileEvaluator.Create(profile, surfaceResolution);
                var bankEvaluator = SurfaceBankEvaluator.Create(bank);

                var mesh = SurfaceMeshBuilder.BuildSurface(
                    surface,
                    geometry,
                    profileEvaluator,
                    bankEvaluator,
                    baseHeight,
                    defaultWidth,
                    surfaceResolution);

                if (mesh == null)
                    continue;

                _surfaces.Add(mesh);
                _surfaceIndex.AddSurface(_surfaces.Count - 1, mesh.Bounds);
            }
        }

        public IReadOnlyList<TrackSurfaceMesh> Surfaces => _surfaces;

        public bool TrySample(Vector3 position, out TrackSurfaceSample sample, TrackSurfaceQueryOptions? options = null)
        {
            sample = default;
            if (_surfaces.Count == 0)
                return false;

            if (!_surfaceIndex.TryGetSurfaces(position.X, position.Z, out var surfaceIndices))
                return false;

            var found = false;
            TrackSurfaceSample best = default;
            var minLayer = options?.MinLayer;
            var maxLayer = options?.MaxLayer;
            var preferLayer = options?.PreferHighestLayer ?? true;
            var preferHeight = options?.PreferHighestHeight ?? true;

            foreach (var index in surfaceIndices)
            {
                if ((uint)index >= (uint)_surfaces.Count)
                    continue;
                var surface = _surfaces[index];
                if (minLayer.HasValue && surface.Layer < minLayer.Value)
                    continue;
                if (maxLayer.HasValue && surface.Layer > maxLayer.Value)
                    continue;
                if (!surface.TrySample(position.X, position.Z, out var hit))
                    continue;

                if (!found || IsBetter(hit, best, preferLayer, preferHeight))
                {
                    found = true;
                    best = hit;
                }
            }

            if (!found)
                return false;

            sample = best;
            return true;
        }

        private static bool IsBetter(TrackSurfaceSample candidate, TrackSurfaceSample current, bool preferLayer, bool preferHeight)
        {
            if (preferLayer && candidate.Layer != current.Layer)
                return candidate.Layer > current.Layer;
            if (preferHeight)
                return candidate.Position.Y > current.Position.Y;
            return false;
        }

        private static Dictionary<string, GeometryDefinition> BuildGeometryLookup(IEnumerable<GeometryDefinition> geometries)
        {
            var lookup = new Dictionary<string, GeometryDefinition>(StringComparer.OrdinalIgnoreCase);
            if (geometries == null)
                return lookup;
            foreach (var geometry in geometries)
            {
                if (geometry == null)
                    continue;
                lookup[geometry.Id] = geometry;
            }
            return lookup;
        }

        private static Dictionary<string, TrackProfileDefinition> BuildProfileLookup(IEnumerable<TrackProfileDefinition> profiles)
        {
            var lookup = new Dictionary<string, TrackProfileDefinition>(StringComparer.OrdinalIgnoreCase);
            if (profiles == null)
                return lookup;
            foreach (var profile in profiles)
            {
                if (profile == null)
                    continue;
                lookup[profile.Id] = profile;
            }
            return lookup;
        }

        private static Dictionary<string, TrackBankDefinition> BuildBankLookup(IEnumerable<TrackBankDefinition> banks)
        {
            var lookup = new Dictionary<string, TrackBankDefinition>(StringComparer.OrdinalIgnoreCase);
            if (banks == null)
                return lookup;
            foreach (var bank in banks)
            {
                if (bank == null)
                    continue;
                lookup[bank.Id] = bank;
            }
            return lookup;
        }

        private static Dictionary<string, SurfaceDefaults> BuildSurfaceDefaults(
            IEnumerable<TrackAreaDefinition> areas,
            float defaultWidth)
        {
            var defaults = new Dictionary<string, SurfaceDefaults>(StringComparer.OrdinalIgnoreCase);
            if (areas == null)
                return defaults;

            foreach (var area in areas)
            {
                if (area == null || string.IsNullOrWhiteSpace(area.SurfaceId))
                    continue;

                var surfaceId = area.SurfaceId.Trim();
                var width = area.WidthMeters ?? defaultWidth;
                var height = area.ElevationMeters;
                var resolution = TryGetAreaSurfaceResolution(area.Metadata);

                if (!defaults.TryGetValue(surfaceId, out var entry))
                {
                    entry = new SurfaceDefaults(width, height, resolution, true, true, true);
                    defaults[surfaceId] = entry;
                    continue;
                }

                var widthOk = entry.Width.HasValue && Math.Abs(entry.Width.Value - width) <= 0.001f;
                var heightOk = entry.BaseHeight.HasValue && Math.Abs(entry.BaseHeight.Value - height) <= 0.001f;
                var resolutionValue = entry.Resolution;
                if (resolution.HasValue)
                {
                    if (!resolutionValue.HasValue)
                        resolutionValue = resolution.Value;
                    else if (Math.Abs(resolutionValue.Value - resolution.Value) > 0.001f)
                        resolutionValue = null;
                }

                defaults[surfaceId] = new SurfaceDefaults(
                    widthOk ? entry.Width : null,
                    heightOk ? entry.BaseHeight : null,
                    resolutionValue,
                    widthOk,
                    heightOk,
                    true);
            }

            return defaults;
        }

        private static float? TryGetAreaSurfaceResolution(IReadOnlyDictionary<string, string> metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return null;

            if (SurfaceParameterParser.TryGetFloat(metadata, out var resolution, "surface_resolution", "surface_cell_size", "surface_grid"))
                return Math.Max(0.1f, resolution);

            return null;
        }

        private readonly struct SurfaceDefaults
        {
            public SurfaceDefaults(float? width, float? baseHeight, float? resolution, bool widthStable, bool heightStable, bool resolutionStable)
            {
                Width = widthStable ? width : null;
                BaseHeight = heightStable ? baseHeight : null;
                Resolution = resolutionStable ? resolution : null;
            }

            public float? Width { get; }
            public float? BaseHeight { get; }
            public float? Resolution { get; }
        }
    }
}
