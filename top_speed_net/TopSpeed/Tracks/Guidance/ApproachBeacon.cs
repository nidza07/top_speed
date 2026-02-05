using System;
using System.Collections.Generic;
using System.Numerics;
using TopSpeed.Tracks.Map;
using TopSpeed.Tracks.Geometry;
using TopSpeed.Tracks.Topology;
using TopSpeed.Tracks.Volumes;

namespace TopSpeed.Tracks.Guidance
{
    internal readonly struct TrackApproachCue
    {
        public TrackApproachCue(
            string sectorId,
            TrackApproachSide side,
            string portalId,
            Vector3 portalPosition,
            Vector3 beaconPosition,
            float targetHeadingDegrees,
            float deltaDegrees,
            float distanceMeters,
            float? widthMeters,
            float? lengthMeters,
            float? toleranceDegrees,
            bool passed)
        {
            SectorId = sectorId;
            Side = side;
            PortalId = portalId;
            PortalPosition = portalPosition;
            BeaconPosition = beaconPosition;
            TargetHeadingDegrees = targetHeadingDegrees;
            DeltaDegrees = deltaDegrees;
            DistanceMeters = distanceMeters;
            WidthMeters = widthMeters;
            LengthMeters = lengthMeters;
            ToleranceDegrees = toleranceDegrees;
            Passed = passed;
        }

        public string SectorId { get; }
        public TrackApproachSide Side { get; }
        public string PortalId { get; }
        public Vector3 PortalPosition { get; }
        public Vector3 BeaconPosition { get; }
        public float TargetHeadingDegrees { get; }
        public float DeltaDegrees { get; }
        public float DistanceMeters { get; }
        public float? WidthMeters { get; }
        public float? LengthMeters { get; }
        public float? ToleranceDegrees { get; }
        public bool Passed { get; }
    }

    internal sealed class TrackApproachBeacon
    {
        private readonly TrackPortalManager _portalManager;
        private readonly TrackApproachManager _approachManager;
        private readonly Dictionary<string, GeometryDefinition> _geometriesById;
        private readonly float _rangeMeters;

        public TrackApproachBeacon(TrackMap map, float rangeMeters = 50f)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            _portalManager = map.BuildPortalManager();
            _approachManager = new TrackApproachManager(map.Sectors, map.Approaches, _portalManager);
            _geometriesById = new Dictionary<string, GeometryDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var geometry in map.Geometries)
            {
                if (geometry == null || string.IsNullOrWhiteSpace(geometry.Id))
                    continue;
                _geometriesById[geometry.Id.Trim()] = geometry;
            }
            _rangeMeters = Math.Max(1f, rangeMeters);
        }

        public float RangeMeters => _rangeMeters;

        public bool TryGetCue(Vector3 worldPosition, float headingDegrees, out TrackApproachCue cue)
        {
            cue = default;
            if (_approachManager.Approaches.Count == 0)
                return false;

            var position = worldPosition;
            var position2D = new Vector2(worldPosition.X, worldPosition.Z);
            var best = default(Candidate);
            var hasBest = false;

            foreach (var approach in _approachManager.Approaches)
            {
                if (approach == null)
                    continue;

                var range = GetApproachRange(approach, _rangeMeters);
                if (IsSideEnabled(approach, TrackApproachSide.Entry))
                {
                    if (TryBuildCandidate(approach, TrackApproachSide.Entry, position, range, ref best, ref hasBest))
                        continue;
                }
                if (IsSideEnabled(approach, TrackApproachSide.Exit))
                    TryBuildCandidate(approach, TrackApproachSide.Exit, position, range, ref best, ref hasBest);
            }

            if (!hasBest)
                return false;

            var delta = DeltaDegrees(headingDegrees, best.TargetHeadingDegrees);
            var forward = HeadingToVector(best.TargetHeadingDegrees);
            var toPlayer = position2D - new Vector2(best.PortalPosition.X, best.PortalPosition.Z);
            var passed = Vector2.Dot(forward, toPlayer) > 0f;
            var beaconPosition = best.PortalPosition;
            if (best.UseHeadingBeacon)
                beaconPosition = ResolveBeaconPosition(position, best.BeaconHeadingDegrees, best.BeaconGeometry);

            var sectorId = best.SectorId ?? string.Empty;
            var portalId = best.PortalId ?? string.Empty;
            cue = new TrackApproachCue(
                sectorId,
                best.Side,
                portalId,
                best.PortalPosition,
                beaconPosition,
                best.TargetHeadingDegrees,
                delta,
                best.DistanceMeters,
                best.WidthMeters,
                best.LengthMeters,
                best.ToleranceDegrees,
                passed);
            return true;
        }

        private bool TryBuildCandidate(
            TrackApproachDefinition approach,
            TrackApproachSide side,
            Vector3 position,
            float rangeMeters,
            ref Candidate best,
            ref bool hasBest)
        {
            var portalId = side == TrackApproachSide.Entry ? approach.EntryPortalId : approach.ExitPortalId;
            var heading = side == TrackApproachSide.Entry ? approach.EntryHeadingDegrees : approach.ExitHeadingDegrees;
            if (!heading.HasValue || string.IsNullOrWhiteSpace(portalId))
                return false;
            if (!_portalManager.TryGetPortal(portalId!, out var portal))
                return false;
            if (!IsWithinVolume(approach, portal, position.Y))
                return false;

            var portalPos = new Vector3(portal.X, portal.Y, portal.Z);
            var portalDistance = Vector3.Distance(position, portalPos);
            var rangeDistance = portalDistance;
            var hasGeometry = TryGetBeaconGeometry(approach, out var beaconGeometry);
            if (hasGeometry)
                rangeDistance = DistanceToGeometry(beaconGeometry, position, approach.WidthMeters);
            if (rangeDistance > rangeMeters)
                return false;

            if (!hasBest || portalDistance < best.DistanceMeters)
            {
                var beaconHeading = ResolveBeaconHeading(approach, heading.Value);
                var useHeadingBeacon = ResolveBeaconPlacement(approach);
                var candidateGeometry = hasGeometry ? beaconGeometry : null;
                best = new Candidate
                {
                    SectorId = approach.SectorId,
                    Side = side,
                    PortalId = portal.Id,
                    PortalPosition = portalPos,
                    TargetHeadingDegrees = heading.Value,
                    BeaconHeadingDegrees = beaconHeading,
                    DistanceMeters = portalDistance,
                    WidthMeters = approach.WidthMeters,
                    LengthMeters = approach.LengthMeters,
                    ToleranceDegrees = approach.AlignmentToleranceDegrees,
                    UseHeadingBeacon = useHeadingBeacon,
                    BeaconGeometry = candidateGeometry
                };
                hasBest = true;
            }

            return true;
        }

        private static float GetApproachRange(TrackApproachDefinition approach, float defaultRange)
        {
            if (approach?.Metadata == null || approach.Metadata.Count == 0)
                return Math.Max(0f, defaultRange);

            if (TryGetFloat(approach.Metadata, out var range, "approach_range", "beacon_range", "range"))
                return Math.Max(0f, range);

            return Math.Max(0f, defaultRange);
        }

        private static bool IsSideEnabled(TrackApproachDefinition approach, TrackApproachSide side)
        {
            if (approach?.Metadata == null || approach.Metadata.Count == 0)
                return true;

            if (TryGetBool(approach.Metadata, out var enabled, "enabled") && !enabled)
                return false;
            if (TryGetBool(approach.Metadata, out var beaconEnabled, "beacon_enabled") && !beaconEnabled)
                return false;

            if (TryGetString(approach.Metadata, out var raw, "approach_side", "approach_sides", "side"))
            {
                var trimmed = raw.Trim().ToLowerInvariant();
                if (trimmed.Contains("none") || trimmed.Contains("off") || trimmed.Contains("disabled"))
                    return false;
                var hasEntry = trimmed.Contains("entry");
                var hasExit = trimmed.Contains("exit");
                if (hasEntry || hasExit)
                    return side == TrackApproachSide.Entry ? hasEntry : hasExit;
            }

            if (TryGetBool(approach.Metadata, out var entryEnabled, "approach_entry", "entry_enabled", "entry_beacon"))
            {
                if (side == TrackApproachSide.Entry)
                    return entryEnabled;
            }
            if (TryGetBool(approach.Metadata, out var exitEnabled, "approach_exit", "exit_enabled", "exit_beacon"))
            {
                if (side == TrackApproachSide.Exit)
                    return exitEnabled;
            }

            return true;
        }

        private static bool ResolveBeaconPlacement(TrackApproachDefinition approach)
        {
            var useHeadingBeacon = true;

            if (approach?.Metadata == null || approach.Metadata.Count == 0)
                return useHeadingBeacon;

            if (TryGetString(approach.Metadata, out var mode, "beacon_mode", "beacon_position", "beacon_style"))
            {
                var trimmed = mode.Trim().ToLowerInvariant();
                if (trimmed.Contains("portal") || trimmed.Contains("point"))
                    useHeadingBeacon = false;
                else if (trimmed.Contains("heading") || trimmed.Contains("direction"))
                    useHeadingBeacon = true;
            }

            return useHeadingBeacon;
        }

        private static float ResolveBeaconHeading(TrackApproachDefinition approach, float fallbackHeading)
        {
            if (approach?.Metadata == null || approach.Metadata.Count == 0)
                return fallbackHeading;

            if (TryGetHeadingValue(approach.Metadata, out var heading, "beacon_heading", "beacon_heading_deg", "beacon_orientation", "beacon_dir", "beacon_direction"))
                return NormalizeDegrees(heading);

            return fallbackHeading;
        }

        private static Vector3 ResolveBeaconPosition(Vector3 position, float headingDegrees, GeometryDefinition? geometry)
        {
            var forward = HeadingToVector3(headingDegrees);
            if (geometry == null)
                return position + forward;

            var points3D = geometry.Points;
            switch (geometry.Type)
            {
                case GeometryType.Polyline:
                case GeometryType.Spline:
                    return ClosestPointOnPolyline(position, points3D);
                case GeometryType.Polygon:
                    return ClosestPointOnPolygon(position, points3D);
                default:
                    return position + forward;
            }
        }

        private static Vector3 ClosestPointOnPolyline(Vector3 position, IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0)
                return position;
            if (points.Count == 1)
                return points[0];

            var best = points[0];
            var bestDist = float.MaxValue;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var candidate = ClosestPointOnSegment(points[i], points[i + 1], position);
                var dist = Vector3.DistanceSquared(candidate, position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }
            return best;
        }

        private static Vector3 ClosestPointOnPolygon(Vector3 position, IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0)
                return position;
            if (IsPointInPolygon(ProjectToXZ(points), new Vector2(position.X, position.Z)))
            {
                if (TryGetPolygonPlane(points, out var planePoint, out var planeNormal))
                    return ProjectToPlane(position, planePoint, planeNormal);
                var avgY = ResolveAverageY(points);
                return new Vector3(position.X, avgY, position.Z);
            }

            var best = points[0];
            var bestDist = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var candidate = ClosestPointOnSegment(a, b, position);
                var dist = Vector3.DistanceSquared(candidate, position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }
            return best;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            var ap = p - a;
            var abLenSq = Vector3.Dot(ab, ab);
            if (abLenSq <= float.Epsilon)
                return a;

            var t = Vector3.Dot(ap, ab) / abLenSq;
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;

            return a + (ab * t);
        }

        private bool TryGetBeaconGeometry(TrackApproachDefinition approach, out GeometryDefinition geometry)
        {
            geometry = null!;
            if (approach?.Metadata == null || approach.Metadata.Count == 0)
                return false;

            if (!TryGetString(approach.Metadata, out var geometryId, "beacon_geometry", "centerline_geometry", "beacon_zone", "approach_geometry"))
                return false;

            return _geometriesById.TryGetValue(geometryId.Trim(), out geometry!);
        }

        private static float DistanceToGeometry(GeometryDefinition geometry, Vector3 position, float? widthOverride)
        {
            if (geometry == null)
                return float.MaxValue;

            var points3D = geometry.Points;
            switch (geometry.Type)
            {
                case GeometryType.Polyline:
                case GeometryType.Spline:
                    var width = widthOverride ?? 0f;
                    return Math.Max(0f, DistanceToPolyline(points3D, position) - (width * 0.5f));
                case GeometryType.Polygon:
                    return DistanceToPolygon(points3D, position);
                case GeometryType.Mesh:
                case GeometryType.Undefined:
                default:
                    return DistanceToPoints(points3D, position);
            }
        }

        private static float DistanceToPoints(IReadOnlyList<Vector3> points, Vector3 position)
        {
            if (points == null || points.Count == 0)
                return float.MaxValue;
            var best = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var dist = Vector3.Distance(points[i], position);
                if (dist < best)
                    best = dist;
            }
            return best;
        }

        private static float DistanceToPolyline(IReadOnlyList<Vector3> points, Vector3 position)
        {
            if (points == null || points.Count == 0)
                return float.MaxValue;
            if (points.Count == 1)
                return Vector3.Distance(points[0], position);

            var best = float.MaxValue;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var distance = DistanceToSegment(points[i], points[i + 1], position);
                if (distance < best)
                    best = distance;
            }
            return best;
        }

        private static float DistanceToPolygon(IReadOnlyList<Vector3> points, Vector3 position)
        {
            if (points == null || points.Count == 0)
                return float.MaxValue;

            var points2D = ProjectToXZ(points);
            var position2D = new Vector2(position.X, position.Z);
            if (IsPointInPolygon(points2D, position2D))
            {
                if (TryGetPolygonPlane(points, out var planePoint, out var planeNormal))
                    return Math.Abs(Vector3.Dot(position - planePoint, planeNormal));
                return Math.Abs(position.Y - ResolveAverageY(points));
            }

            var best = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var distance = DistanceToSegment(a, b, position);
                if (distance < best)
                    best = distance;
            }
            return best;
        }

        private static List<Vector2> ProjectToXZ(IReadOnlyList<Vector3> points)
        {
            var projected = new List<Vector2>();
            if (points == null || points.Count == 0)
                return projected;

            projected.Capacity = points.Count;
            foreach (var point in points)
                projected.Add(new Vector2(point.X, point.Z));
            return projected;
        }

        private static bool IsPointInPolygon(IReadOnlyList<Vector2> points, Vector2 position)
        {
            var inside = false;
            var j = points.Count - 1;
            for (var i = 0; i < points.Count; i++)
            {
                var pi = points[i];
                var pj = points[j];
                var intersect = ((pi.Y > position.Y) != (pj.Y > position.Y)) &&
                                (position.X < (pj.X - pi.X) * (position.Y - pi.Y) / (pj.Y - pi.Y + float.Epsilon) + pi.X);
                if (intersect)
                    inside = !inside;
                j = i;
            }
            return inside;
        }

        private static float DistanceToSegment(Vector3 a, Vector3 b, Vector3 position)
        {
            var ab = b - a;
            var ap = position - a;
            var abLenSq = Vector3.Dot(ab, ab);
            if (abLenSq <= float.Epsilon)
                return Vector3.Distance(a, position);
            var t = Vector3.Dot(ap, ab) / abLenSq;
            if (t <= 0f)
                return Vector3.Distance(a, position);
            if (t >= 1f)
                return Vector3.Distance(b, position);
            var closest = a + (ab * t);
            return Vector3.Distance(closest, position);
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

        private static bool TryGetBool(
            IReadOnlyDictionary<string, string> metadata,
            out bool value,
            params string[] keys)
        {
            value = false;
            if (metadata == null || metadata.Count == 0)
                return false;
            foreach (var key in keys)
            {
                if (!metadata.TryGetValue(key, out var raw))
                    continue;
                if (TryParseBool(raw, out value))
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

        private static bool TryParseBool(string raw, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            var trimmed = raw.Trim().ToLowerInvariant();
            switch (trimmed)
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    value = true;
                    return true;
                case "0":
                case "false":
                case "no":
                case "off":
                    value = false;
                    return true;
            }
            return bool.TryParse(raw, out value);
        }

        private static float NormalizeDegrees(float degrees)
        {
            var result = degrees % 360f;
            if (result < 0f)
                result += 360f;
            return result;
        }

        private static bool TryGetHeadingValue(
            IReadOnlyDictionary<string, string> metadata,
            out float headingDegrees,
            params string[] keys)
        {
            headingDegrees = 0f;
            if (metadata == null || metadata.Count == 0)
                return false;
            foreach (var key in keys)
            {
                if (!metadata.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                    continue;
                if (TryParseCompassHeading(raw, out headingDegrees))
                    return true;
                if (float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out headingDegrees))
                    return true;
            }
            return false;
        }

        private static bool TryParseCompassHeading(string value, out float headingDegrees)
        {
            headingDegrees = 0f;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim().ToLowerInvariant()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty);

            switch (trimmed)
            {
                case "n":
                case "north":
                    headingDegrees = 0f;
                    return true;
                case "nne":
                case "northnortheast":
                    headingDegrees = 22.5f;
                    return true;
                case "ne":
                case "northeast":
                    headingDegrees = 45f;
                    return true;
                case "ene":
                case "eastnortheast":
                    headingDegrees = 67.5f;
                    return true;
                case "e":
                case "east":
                    headingDegrees = 90f;
                    return true;
                case "ese":
                case "eastsoutheast":
                    headingDegrees = 112.5f;
                    return true;
                case "se":
                case "southeast":
                    headingDegrees = 135f;
                    return true;
                case "sse":
                case "southsoutheast":
                    headingDegrees = 157.5f;
                    return true;
                case "s":
                case "south":
                    headingDegrees = 180f;
                    return true;
                case "ssw":
                case "southsouthwest":
                    headingDegrees = 202.5f;
                    return true;
                case "sw":
                case "southwest":
                    headingDegrees = 225f;
                    return true;
                case "wsw":
                case "westsouthwest":
                    headingDegrees = 247.5f;
                    return true;
                case "w":
                case "west":
                    headingDegrees = 270f;
                    return true;
                case "wnw":
                case "westnorthwest":
                    headingDegrees = 292.5f;
                    return true;
                case "nw":
                case "northwest":
                    headingDegrees = 315f;
                    return true;
                case "nnw":
                case "northnorthwest":
                    headingDegrees = 337.5f;
                    return true;
            }

            return false;
        }

        private static float DeltaDegrees(float current, float target)
        {
            var diff = Math.Abs(NormalizeDegrees(current - target));
            return diff > 180f ? 360f - diff : diff;
        }

        private static Vector2 HeadingToVector(float headingDegrees)
        {
            var radians = headingDegrees * (float)Math.PI / 180f;
            return new Vector2((float)Math.Sin(radians), (float)Math.Cos(radians));
        }

        private static Vector3 HeadingToVector3(float headingDegrees)
        {
            var radians = headingDegrees * (float)Math.PI / 180f;
            return new Vector3((float)Math.Sin(radians), 0f, (float)Math.Cos(radians));
        }

        private static float ResolveAverageY(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0)
                return 0f;
            var sum = 0f;
            for (var i = 0; i < points.Count; i++)
                sum += points[i].Y;
            return sum / points.Count;
        }

        private static bool TryGetPolygonPlane(IReadOnlyList<Vector3> points, out Vector3 planePoint, out Vector3 planeNormal)
        {
            planePoint = Vector3.Zero;
            planeNormal = Vector3.UnitY;
            if (points == null || points.Count < 3)
                return false;

            for (var i = 0; i < points.Count - 2; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                for (var j = i + 2; j < points.Count; j++)
                {
                    var c = points[j];
                    var ab = b - a;
                    var ac = c - a;
                    var normal = Vector3.Cross(ab, ac);
                    if (normal.LengthSquared() <= 0.000001f)
                        continue;
                    planePoint = a;
                    planeNormal = Vector3.Normalize(normal);
                    return true;
                }
            }

            return false;
        }

        private static Vector3 ProjectToPlane(Vector3 position, Vector3 planePoint, Vector3 planeNormal)
        {
            var toPoint = position - planePoint;
            var distance = Vector3.Dot(toPoint, planeNormal);
            return position - (planeNormal * distance);
        }

        private static bool IsWithinVolume(TrackApproachDefinition approach, PortalDefinition portal, float y)
        {
            if (HasVolume(approach))
            {
                if (!VolumeBounds.TryResolve(
                        portal.Y,
                        approach.VolumeMode,
                        approach.VolumeOffsetMode,
                        approach.VolumeOffsetSpace,
                        approach.VolumeMinMaxSpace,
                        approach.VolumeThicknessMeters,
                        approach.VolumeOffsetMeters,
                        approach.VolumeMinY,
                        approach.VolumeMaxY,
                        out var minY,
                        out var maxY))
                {
                    return true;
                }

                return y >= minY && y <= maxY;
            }

            if (HasVolume(portal))
            {
                if (!VolumeBounds.TryResolve(
                        portal.Y,
                        portal.VolumeMode,
                        portal.VolumeOffsetMode,
                        portal.VolumeOffsetSpace,
                        portal.VolumeMinMaxSpace,
                        portal.VolumeThicknessMeters,
                        portal.VolumeOffsetMeters,
                        portal.VolumeMinY,
                        portal.VolumeMaxY,
                        out var minY,
                        out var maxY))
                {
                    return true;
                }

                return y >= minY && y <= maxY;
            }

            return true;
        }

        private static bool HasVolume(TrackApproachDefinition approach)
        {
            if (approach == null)
                return false;
            return approach.VolumeThicknessMeters.HasValue ||
                   approach.VolumeOffsetMeters.HasValue ||
                   approach.VolumeMinY.HasValue ||
                   approach.VolumeMaxY.HasValue;
        }

        private static bool HasVolume(PortalDefinition portal)
        {
            if (portal == null)
                return false;
            return portal.VolumeThicknessMeters.HasValue ||
                   portal.VolumeOffsetMeters.HasValue ||
                   portal.VolumeMinY.HasValue ||
                   portal.VolumeMaxY.HasValue;
        }

        private struct Candidate
        {
            public string? SectorId;
            public TrackApproachSide Side;
            public string? PortalId;
            public Vector3 PortalPosition;
            public float TargetHeadingDegrees;
            public float BeaconHeadingDegrees;
            public float DistanceMeters;
            public float? WidthMeters;
            public float? LengthMeters;
            public float? ToleranceDegrees;
            public bool UseHeadingBeacon;
            public GeometryDefinition? BeaconGeometry;
        }
    }
}
