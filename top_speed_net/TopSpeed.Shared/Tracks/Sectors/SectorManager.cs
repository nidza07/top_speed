using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TopSpeed.Tracks.Areas;
using TopSpeed.Tracks.Geometry;
using TopSpeed.Tracks.Topology;
using TopSpeed.Tracks.Volumes;

namespace TopSpeed.Tracks.Sectors
{
    public sealed class TrackSectorManager
    {
        private readonly Dictionary<string, TrackSectorDefinition> _sectorsById;
        private readonly TrackAreaManager _areaManager;
        private readonly TrackPortalManager _portalManager;

        public TrackSectorManager(
            IEnumerable<TrackSectorDefinition> sectors,
            TrackAreaManager areaManager,
            TrackPortalManager portalManager)
        {
            if (areaManager == null)
                throw new ArgumentNullException(nameof(areaManager));
            if (portalManager == null)
                throw new ArgumentNullException(nameof(portalManager));

            _areaManager = areaManager;
            _portalManager = portalManager;
            _sectorsById = new Dictionary<string, TrackSectorDefinition>(StringComparer.OrdinalIgnoreCase);

            if (sectors != null)
            {
                foreach (var sector in sectors)
                    AddSector(sector);
            }
        }

        public IReadOnlyCollection<TrackSectorDefinition> Sectors => _sectorsById.Values;

        public bool TryGetSector(string id, out TrackSectorDefinition sector)
        {
            sector = null!;
            if (string.IsNullOrWhiteSpace(id))
                return false;
            return _sectorsById.TryGetValue(id.Trim(), out sector!);
        }

        public IReadOnlyList<TrackSectorDefinition> FindSectorsContaining(Vector2 position)
        {
            if (_sectorsById.Count == 0)
                return Array.Empty<TrackSectorDefinition>();

            var hits = new List<TrackSectorDefinition>();
            foreach (var sector in _sectorsById.Values)
            {
                if (Contains(sector, position))
                    hits.Add(sector);
            }
            return hits;
        }

        public IReadOnlyList<TrackSectorDefinition> FindSectorsContaining(Vector3 position)
        {
            if (_sectorsById.Count == 0)
                return Array.Empty<TrackSectorDefinition>();

            var hits = new List<TrackSectorDefinition>();
            foreach (var sector in _sectorsById.Values)
            {
                if (Contains(sector, position))
                    hits.Add(sector);
            }
            return hits;
        }

        public bool TryLocate(
            Vector2 position,
            float? headingDegrees,
            out TrackSectorDefinition sector,
            out PortalDefinition? portal,
            out float? portalDeltaDegrees)
        {
            sector = null!;
            portal = null;
            portalDeltaDegrees = null;

            var candidates = FindSectorsContaining(position);
            if (candidates.Count == 0)
                return false;

            if (!headingDegrees.HasValue || candidates.Count == 1)
            {
                sector = candidates[0];
                portal = FindBestPortal(sector.Id, headingDegrees, out portalDeltaDegrees);
                return true;
            }

            var bestScore = float.MaxValue;
            TrackSectorDefinition? bestSector = null;
            PortalDefinition? bestPortal = null;
            float? bestDelta = null;

            foreach (var candidate in candidates)
            {
                var candidatePortal = FindBestPortal(candidate.Id, headingDegrees, out var delta);
                var score = delta ?? 180f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestSector = candidate;
                    bestPortal = candidatePortal;
                    bestDelta = delta;
                }
            }

            if (bestSector == null)
                return false;

            sector = bestSector;
            portal = bestPortal;
            portalDeltaDegrees = bestDelta;
            return true;
        }

        public bool TryLocate(
            Vector3 position,
            float? headingDegrees,
            out TrackSectorDefinition sector,
            out PortalDefinition? portal,
            out float? portalDeltaDegrees)
        {
            sector = null!;
            portal = null;
            portalDeltaDegrees = null;

            var candidates = FindSectorsContaining(position);
            if (candidates.Count == 0)
                return false;

            if (!headingDegrees.HasValue || candidates.Count == 1)
            {
                sector = candidates[0];
                portal = FindBestPortal(sector.Id, headingDegrees, out portalDeltaDegrees);
                return true;
            }

            var bestScore = float.MaxValue;
            TrackSectorDefinition? bestSector = null;
            PortalDefinition? bestPortal = null;
            float? bestDelta = null;

            foreach (var candidate in candidates)
            {
                var candidatePortal = FindBestPortal(candidate.Id, headingDegrees, out var delta);
                var score = delta ?? 180f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestSector = candidate;
                    bestPortal = candidatePortal;
                    bestDelta = delta;
                }
            }

            if (bestSector == null)
                return false;

            sector = bestSector;
            portal = bestPortal;
            portalDeltaDegrees = bestDelta;
            return true;
        }

        public IReadOnlyList<PortalDefinition> GetPortalsForSector(string sectorId)
        {
            return _portalManager.GetPortalsForSector(sectorId);
        }

        public IReadOnlyList<string> GetConnectedSectorIds(string sectorId)
        {
            return _portalManager.GetConnectedSectorIds(sectorId);
        }

        private void AddSector(TrackSectorDefinition sector)
        {
            if (sector == null)
                throw new ArgumentNullException(nameof(sector));
            _sectorsById[sector.Id] = sector;
        }

        private bool Contains(TrackSectorDefinition sector, Vector2 position)
        {
            if (sector == null || string.IsNullOrWhiteSpace(sector.AreaId))
                return false;
            var areaId = sector.AreaId!.Trim();
            if (_areaManager.TryGetArea(areaId, out var area))
                return _areaManager.Contains(area, position);
            return _areaManager.ContainsGeometry(areaId, position);
        }

        private bool Contains(TrackSectorDefinition sector, Vector3 position)
        {
            if (sector == null || string.IsNullOrWhiteSpace(sector.AreaId))
                return false;
            var areaId = sector.AreaId!.Trim();
            if (_areaManager.TryGetArea(areaId, out var area))
                return _areaManager.Contains(area, position);
            if (!_areaManager.TryGetGeometry(areaId, out var geometry))
                return _areaManager.ContainsGeometry(areaId, new Vector2(position.X, position.Z));

            if (TryGetVolumeId(sector.Metadata, out var volumeId))
            {
                if (!_areaManager.TryGetVolume(volumeId, out var volume))
                    return false;
                if (!volume.Contains(position))
                    return false;
                if (TryResolveVolumeBounds(sector.Metadata, geometry, out var minBound, out var maxBound))
                    return position.Y >= minBound && position.Y <= maxBound;
                return true;
            }

            if (geometry.Type == GeometryType.Mesh)
            {
                var metadata = sector.Metadata;
                var volumeMode = GetVolumeMode(metadata);
                if (volumeMode != TrackAreaVolumeMode.ClosedMesh)
                    return false;
                if (!_areaManager.TryGetMeshContainment(geometry.Id, out var mesh) || !mesh.IsClosed)
                    return false;
                if (!mesh.Contains(position, MeshContainment.DefaultSurfaceEpsilon))
                    return false;
                if (TryResolveVolumeBounds(metadata, geometry, out var minBound, out var maxBound))
                    return position.Y >= minBound && position.Y <= maxBound;
                return true;
            }

            if (!_areaManager.ContainsGeometry(geometry.Id, new Vector2(position.X, position.Z)))
                return false;
            return IsWithinGeometryVolume(sector, geometry, position.Y);
        }

        private PortalDefinition? FindBestPortal(string sectorId, float? headingDegrees, out float? deltaDegrees)
        {
            deltaDegrees = null;
            if (!headingDegrees.HasValue)
                return null;

            var portals = _portalManager.GetPortalsForSector(sectorId);
            if (portals.Count == 0)
                return null;

            var heading = NormalizeDegrees(headingDegrees.Value);
            PortalDefinition? bestPortal = null;
            var bestDelta = float.MaxValue;

            foreach (var portal in portals)
            {
                if (!TryGetPortalHeading(portal, heading, out var delta))
                    continue;
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    bestPortal = portal;
                }
            }

            if (bestPortal == null)
                return null;

            deltaDegrees = bestDelta;
            return bestPortal;
        }

        private static bool TryGetPortalHeading(PortalDefinition portal, float heading, out float deltaDegrees)
        {
            deltaDegrees = 0f;
            if (portal == null)
                return false;

            switch (portal.Role)
            {
                case PortalRole.Entry:
                    return TryDelta(heading, portal.EntryHeadingDegrees, out deltaDegrees);
                case PortalRole.Exit:
                    return TryDelta(heading, portal.ExitHeadingDegrees, out deltaDegrees);
                case PortalRole.EntryExit:
                    var hasEntry = TryDelta(heading, portal.EntryHeadingDegrees, out var entryDelta);
                    var hasExit = TryDelta(heading, portal.ExitHeadingDegrees, out var exitDelta);
                    if (hasEntry && hasExit)
                    {
                        deltaDegrees = Math.Min(entryDelta, exitDelta);
                        return true;
                    }
                    if (hasEntry)
                    {
                        deltaDegrees = entryDelta;
                        return true;
                    }
                    if (hasExit)
                    {
                        deltaDegrees = exitDelta;
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private static bool TryDelta(float heading, float? target, out float deltaDegrees)
        {
            deltaDegrees = 0f;
            if (!target.HasValue)
                return false;
            var diff = Math.Abs(NormalizeDegrees(heading - target.Value));
            deltaDegrees = diff > 180f ? 360f - diff : diff;
            return true;
        }

        private static float NormalizeDegrees(float degrees)
        {
            var result = degrees % 360f;
            if (result < 0f)
                result += 360f;
            return result;
        }

        private static bool IsWithinGeometryVolume(TrackSectorDefinition sector, GeometryDefinition geometry, float y)
        {
            if (sector == null)
                return true;

            if (!TryResolveVolumeBounds(sector.Metadata, geometry, out var minBound, out var maxBound))
                return true;
            return y >= minBound && y <= maxBound;
        }

        private static bool TryResolveVolumeBounds(
            IReadOnlyDictionary<string, string> metadata,
            GeometryDefinition geometry,
            out float minBound,
            out float maxBound)
        {
            minBound = 0f;
            maxBound = 0f;
            if (metadata == null || metadata.Count == 0)
                return false;

            var hasVolume = TryGetFloat(metadata, out var thickness, "height", "thickness", "volume_thickness", "volume_height") ||
                            TryGetFloat(metadata, out _, "offset", "volume_offset", "volume_center") ||
                            TryGetFloat(metadata, out _, "min_y", "miny") ||
                            TryGetFloat(metadata, out _, "max_y", "maxy");
            if (!hasVolume)
                return false;

            var baseY = ResolveBaseY(metadata, geometry);
            var volumeThickness = TryGetFloat(metadata, out thickness, "height", "thickness", "volume_thickness", "volume_height")
                ? Math.Max(0.01f, thickness)
                : (float?)null;
            var volumeOffset = TryGetFloat(metadata, out var offsetValue, "offset", "volume_offset", "volume_center")
                ? offsetValue
                : (float?)null;
            var minY = TryGetFloat(metadata, out var minYValue, "min_y", "miny") ? minYValue : (float?)null;
            var maxY = TryGetFloat(metadata, out var maxYValue, "max_y", "maxy") ? maxYValue : (float?)null;
            var volumeMode = GetVolumeMode(metadata);
            var volumeOffsetMode = GetVolumeOffsetMode(metadata);
            var volumeOffsetSpace = GetVolumeSpace(metadata, "volume_offset_space", "offset_space");
            var volumeMinMaxSpace = GetVolumeSpace(metadata, "volume_minmax_space", "minmax_space", "bounds_space", "volume_bounds_space");

            return VolumeBounds.TryResolve(
                baseY,
                volumeMode,
                volumeOffsetMode,
                volumeOffsetSpace,
                volumeMinMaxSpace,
                volumeThickness,
                volumeOffset,
                minY,
                maxY,
                out minBound,
                out maxBound);
        }

        private static float ResolveBaseY(IReadOnlyDictionary<string, string> metadata, GeometryDefinition geometry)
        {
            if (TryGetFloat(metadata, out var baseY, "base_y", "base_height", "elevation", "y"))
                return baseY;

            if (geometry == null || geometry.Points == null || geometry.Points.Count == 0)
                return 0f;

            var sum = 0f;
            for (var i = 0; i < geometry.Points.Count; i++)
                sum += geometry.Points[i].Y;
            return sum / geometry.Points.Count;
        }

        private static bool TryGetFloat(
            IReadOnlyDictionary<string, string> metadata,
            out float value,
            params string[] keys)
        {
            value = 0f;
            if (metadata == null || metadata.Count == 0)
                return false;
            foreach (var key in keys)
            {
                if (!metadata.TryGetValue(key, out var raw))
                    continue;
                if (float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                    return true;
            }
            return false;
        }

        private static bool TryGetString(
            IReadOnlyDictionary<string, string> metadata,
            out string value,
            params string[] keys)
        {
            value = string.Empty;
            if (metadata == null || metadata.Count == 0)
                return false;
            foreach (var key in keys)
            {
                if (metadata.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    value = raw;
                    return true;
                }
            }
            return false;
        }

        private static TrackAreaVolumeMode GetVolumeMode(IReadOnlyDictionary<string, string> metadata)
        {
            if (!TryGetString(metadata, out var raw, "volume_mode"))
                return TrackAreaVolumeMode.LocalBand;

            var trimmed = raw.Trim().ToLowerInvariant();
            switch (trimmed)
            {
                case "world":
                case "world_band":
                case "world_y":
                case "worldy":
                    return TrackAreaVolumeMode.WorldBand;
                case "closed":
                case "closed_mesh":
                    return TrackAreaVolumeMode.ClosedMesh;
                case "local":
                case "local_band":
                case "band":
                default:
                    return TrackAreaVolumeMode.LocalBand;
            }
        }

        private static TrackAreaVolumeOffsetMode GetVolumeOffsetMode(IReadOnlyDictionary<string, string> metadata)
        {
            if (!TryGetString(metadata, out var raw, "volume_offset_mode", "offset_mode", "offset_anchor", "volume_offset_anchor", "offset_align", "volume_offset_align"))
                return TrackAreaVolumeOffsetMode.Bottom;

            var trimmed = raw.Trim().ToLowerInvariant();
            switch (trimmed)
            {
                case "center":
                case "centre":
                case "middle":
                case "mid":
                    return TrackAreaVolumeOffsetMode.Center;
                case "top":
                case "max":
                case "upper":
                case "end":
                    return TrackAreaVolumeOffsetMode.Top;
                case "bottom":
                case "min":
                case "lower":
                case "start":
                default:
                    return TrackAreaVolumeOffsetMode.Bottom;
            }
        }

        private static TrackAreaVolumeSpace GetVolumeSpace(IReadOnlyDictionary<string, string> metadata, params string[] keys)
        {
            if (!TryGetString(metadata, out var raw, keys))
                return TrackAreaVolumeSpace.Inherit;

            var trimmed = raw.Trim().ToLowerInvariant();
            switch (trimmed)
            {
                case "local":
                case "relative":
                case "elevation":
                case "area":
                    return TrackAreaVolumeSpace.Local;
                case "world":
                case "absolute":
                case "global":
                    return TrackAreaVolumeSpace.World;
                case "inherit":
                case "default":
                case "auto":
                default:
                    return TrackAreaVolumeSpace.Inherit;
            }
        }

        private static bool TryGetVolumeId(IReadOnlyDictionary<string, string> metadata, out string volumeId)
        {
            volumeId = string.Empty;
            if (metadata == null || metadata.Count == 0)
                return false;
            if (metadata.TryGetValue("volume", out var raw) && !string.IsNullOrWhiteSpace(raw))
            {
                volumeId = raw.Trim();
                return true;
            }
            if (metadata.TryGetValue("volume_id", out raw) && !string.IsNullOrWhiteSpace(raw))
            {
                volumeId = raw.Trim();
                return true;
            }
            return false;
        }
    }
}
