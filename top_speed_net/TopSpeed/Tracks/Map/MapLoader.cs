using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using TopSpeed.Core;
using TopSpeed.Data;
using TopSpeed.Tracks.Materials;
using TopSpeed.Tracks.Rooms;
using TopSpeed.Tracks.Areas;
using TopSpeed.Tracks.Geometry;
using TopSpeed.Tracks.Topology;
using TopSpeed.Tracks.Surfaces;
using TopSpeed.Tracks.Walls;

namespace TopSpeed.Tracks.Map
{
    internal static class TrackMapLoader
    {
        private const string MapExtension = ".tsm";

        public static bool LooksLikeMap(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
                return false;
            if (Path.HasExtension(nameOrPath))
                return string.Equals(Path.GetExtension(nameOrPath), MapExtension, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public static bool TryResolvePath(string nameOrPath, out string path)
        {
            path = string.Empty;
            if (string.IsNullOrWhiteSpace(nameOrPath))
                return false;

            if (nameOrPath.IndexOfAny(new[] { '\\', '/' }) >= 0)
            {
                path = nameOrPath;
                return File.Exists(path) && LooksLikeMap(path);
            }

            if (!Path.HasExtension(nameOrPath))
            {
                path = Path.Combine(AssetPaths.Root, "Tracks", nameOrPath + MapExtension);
                return File.Exists(path);
            }

            path = Path.Combine(AssetPaths.Root, "Tracks", nameOrPath);
            return File.Exists(path) && LooksLikeMap(path);
        }

        public static TrackMap Load(string nameOrPath)
        {
            var path = ResolvePath(nameOrPath);
            if (!File.Exists(path))
                throw new FileNotFoundException("Track map not found.", path);

            var definition = TrackMapFormat.Parse(path);

            var map = new TrackMap(definition.Metadata.Name, definition.Metadata.CellSizeMeters)
            {
                Weather = definition.Metadata.Weather,
                Ambience = definition.Metadata.Ambience,
                DefaultMaterialId = definition.Metadata.DefaultMaterialId,
                DefaultNoise = definition.Metadata.DefaultNoise,
                DefaultWidthMeters = definition.Metadata.DefaultWidthMeters,
                BaseHeightMeters = definition.Metadata.BaseHeightMeters ?? 0f,
                DefaultAreaHeightMeters = definition.Metadata.DefaultAreaHeightMeters ?? 0f,
                DefaultCeilingHeightMeters = definition.Metadata.DefaultCeilingHeightMeters,
                MinX = definition.Metadata.MinX,
                MinZ = definition.Metadata.MinZ,
                MaxX = definition.Metadata.MaxX,
                MaxZ = definition.Metadata.MaxZ,
                StartX = definition.Metadata.StartX,
                StartZ = definition.Metadata.StartZ,
                StartHeadingDegrees = definition.Metadata.StartHeadingDegrees,
                StartHeading = definition.Metadata.StartHeading,
                SurfaceResolutionMeters = definition.Metadata.SurfaceResolutionMeters
            };

            foreach (var sector in definition.Sectors)
                map.AddSector(sector);
            foreach (var area in definition.Areas)
                map.AddArea(area);
            foreach (var geometry in definition.Geometries)
                map.AddGeometry(geometry);
            foreach (var volume in definition.Volumes)
                map.AddVolume(volume);
            foreach (var portal in definition.Portals)
                map.AddPortal(portal);
            foreach (var link in definition.Links)
                map.AddLink(link);
            foreach (var beacon in definition.Beacons)
                map.AddBeacon(beacon);
            foreach (var marker in definition.Markers)
                map.AddMarker(marker);
            foreach (var approach in definition.Approaches)
                map.AddApproach(approach);
            foreach (var branch in definition.Branches)
                map.AddBranch(branch);
            foreach (var material in definition.Materials)
                map.AddMaterial(material);
            foreach (var room in definition.Rooms)
                map.AddRoom(room);
            foreach (var profile in definition.Profiles)
                map.AddProfile(profile);
            foreach (var bank in definition.Banks)
                map.AddBank(bank);
            foreach (var surface in definition.Surfaces)
                map.AddSurface(surface);

            AddExplicitWalls(map, definition);
            AddPresetMaterials(map, definition);
            AddPresetRooms(map, definition);
            ApplyStartFromAreas(map, definition);
            ApplyFinishFromAreas(map, definition);

            return map;
        }

        private static string ResolvePath(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
                return nameOrPath;
            if (nameOrPath.IndexOfAny(new[] { '\\', '/' }) >= 0)
                return nameOrPath;
            if (!Path.HasExtension(nameOrPath))
                return Path.Combine(AssetPaths.Root, "Tracks", nameOrPath + MapExtension);
            return Path.Combine(AssetPaths.Root, "Tracks", nameOrPath);
        }

        private static void AddPresetMaterials(TrackMap map, TrackMapDefinition definition)
        {
            if (map == null || definition == null)
                return;

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var material in map.Materials)
            {
                if (material == null)
                    continue;
                existing.Add(material.Id);
            }

            foreach (var area in map.Areas)
            {
                if (area == null || string.IsNullOrWhiteSpace(area.MaterialId))
                    continue;
                var id = area.MaterialId!.Trim();
                if (existing.Contains(id))
                    continue;
                if (TrackMaterialLibrary.TryGetPreset(id, out var preset))
                {
                    map.AddMaterial(preset);
                    existing.Add(id);
                }
            }

            foreach (var wall in map.Walls)
            {
                if (wall == null || string.IsNullOrWhiteSpace(wall.MaterialId))
                    continue;
                var id = wall.MaterialId!.Trim();
                if (existing.Contains(id))
                    continue;
                if (TrackMaterialLibrary.TryGetPreset(id, out var preset))
                {
                    map.AddMaterial(preset);
                    existing.Add(id);
                }
            }
        }

        private static void AddPresetRooms(TrackMap map, TrackMapDefinition definition)
        {
            if (map == null || definition == null)
                return;

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in map.Rooms)
            {
                if (room == null)
                    continue;
                existing.Add(room.Id);
            }

            foreach (var area in map.Areas)
            {
                if (area == null || string.IsNullOrWhiteSpace(area.RoomId))
                    continue;
                var id = area.RoomId!.Trim();
                if (existing.Contains(id))
                    continue;
                if (TrackRoomLibrary.TryGetPreset(id, out var preset))
                {
                    map.AddRoom(preset);
                    existing.Add(id);
                }
            }
        }

        private static void AddExplicitWalls(TrackMap map, TrackMapDefinition definition)
        {
            if (map == null || definition == null)
                return;

            foreach (var wall in definition.Walls)
            {
                if (wall == null)
                    continue;
                var resolvedMaterialId = string.IsNullOrWhiteSpace(wall.MaterialId)
                    ? map.DefaultMaterialId
                    : wall.MaterialId!;
                var collisionMaterial = ResolveCollisionMaterial(map, resolvedMaterialId);
                var resolvedWall = new TrackWallDefinition(
                    wall.Id,
                    wall.GeometryId,
                    wall.WidthMeters,
                    wall.ElevationMeters,
                    collisionMaterial,
                    wall.CollisionMode,
                    wall.Name,
                    wall.Metadata,
                    wall.HeightMeters,
                    resolvedMaterialId);
                map.AddWall(resolvedWall);
            }
        }

        private static TrackWallMaterial ResolveCollisionMaterial(TrackMap map, string? materialId)
        {
            if (map == null)
                return TrackWallMaterial.Hard;

            var trimmed = materialId?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                trimmed = map.DefaultMaterialId?.Trim();

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                var material = FindMaterial(map, trimmed!);
                if (material == null && TrackMaterialLibrary.TryGetPreset(trimmed!, out var preset))
                {
                    map.AddMaterial(preset);
                    material = preset;
                }

                if (material != null)
                    return material.CollisionMaterial;
            }

            return TrackWallMaterial.Hard;
        }

        private static TrackMaterialDefinition? FindMaterial(TrackMap map, string materialId)
        {
            foreach (var material in map.Materials)
            {
                if (material != null && string.Equals(material.Id, materialId, StringComparison.OrdinalIgnoreCase))
                    return material;
            }
            return null;
        }

        private static void ApplyStartFromAreas(TrackMap map, TrackMapDefinition definition)
        {
            if (map == null || definition == null)
                return;

            TrackAreaDefinition? startArea = null;
            foreach (var area in definition.Areas)
            {
                if (area != null && area.Type == TrackAreaType.Start)
                {
                    startArea = area;
                    break;
                }
            }

            if (startArea == null)
                return;

            map.StartAreaId = startArea.Id;

            if (TryGetStartPosition(startArea, out var startPos) ||
                TryGetAreaCenter(definition, startArea, out startPos))
            {
                map.StartX = startPos.X;
                map.StartZ = startPos.Y;
            }

            if (TryGetStartHeading(startArea, out var headingDegrees))
            {
                map.StartHeadingDegrees = MapMovement.NormalizeDegrees(headingDegrees);
                map.StartHeading = MapMovement.ToCardinal(map.StartHeadingDegrees);
            }
        }

        private static void ApplyFinishFromAreas(TrackMap map, TrackMapDefinition definition)
        {
            if (map == null || definition == null)
                return;

            foreach (var area in definition.Areas)
            {
                if (area != null && area.Type == TrackAreaType.Finish)
                {
                    map.FinishAreaId = area.Id;
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(map.StartAreaId))
            {
                map.FinishAreaId = map.StartAreaId;
                return;
            }

            foreach (var area in definition.Areas)
            {
                if (area != null && area.Type == TrackAreaType.Start)
                {
                    map.FinishAreaId = area.Id;
                    return;
                }
            }
        }

        private static bool TryGetStartPosition(TrackAreaDefinition area, out System.Numerics.Vector2 position)
        {
            position = default;
            if (area?.Metadata == null || area.Metadata.Count == 0)
                return false;

            if (!TryGetFloat(area.Metadata, out var x, "start_x", "spawn_x", "x") ||
                !TryGetFloat(area.Metadata, out var z, "start_z", "spawn_z", "z"))
                return false;

            position = new System.Numerics.Vector2(x, z);
            return true;
        }

        private static bool TryGetStartHeading(TrackAreaDefinition area, out float headingDegrees)
        {
            headingDegrees = 0f;
            if (area?.Metadata == null || area.Metadata.Count == 0)
                return false;

            if (!TryGetString(area.Metadata, out var raw, "start_heading", "heading", "grid_heading", "orientation"))
                return false;

            if (TryParseHeading(raw, out var parsed))
            {
                headingDegrees = MapMovement.HeadingFromDirection(parsed);
                return true;
            }

            if (TryParseDegrees(raw, out var degrees))
            {
                headingDegrees = MapMovement.NormalizeDegrees(degrees);
                return true;
            }

            return false;
        }

        private static bool TryGetAreaCenter(TrackMapDefinition definition, TrackAreaDefinition area, out System.Numerics.Vector2 center)
        {
            center = default;
            if (definition == null || area == null || string.IsNullOrWhiteSpace(area.GeometryId))
                return false;

            GeometryDefinition? geometry = null;
            foreach (var candidate in definition.Geometries)
            {
                if (candidate != null && string.Equals(candidate.Id, area.GeometryId, StringComparison.OrdinalIgnoreCase))
                {
                    geometry = candidate;
                    break;
                }
            }

            if (geometry == null || geometry.Points == null || geometry.Points.Count == 0)
                return false;

            float sumX = 0f;
            float sumZ = 0f;
            foreach (var point in geometry.Points)
            {
                sumX += point.X;
                sumZ += point.Z;
            }

            center = new System.Numerics.Vector2(sumX / geometry.Points.Count, sumZ / geometry.Points.Count);
            return true;
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
                    value = raw.Trim();
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseHeading(string raw, out MapDirection heading)
        {
            heading = MapDirection.North;
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "n":
                case "north":
                    heading = MapDirection.North;
                    return true;
                case "e":
                case "east":
                    heading = MapDirection.East;
                    return true;
                case "s":
                case "south":
                    heading = MapDirection.South;
                    return true;
                case "w":
                case "west":
                    heading = MapDirection.West;
                    return true;
            }
            return false;
        }

        private static bool TryParseDegrees(string raw, out float degrees)
        {
            return float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out degrees);
        }
    }
}
