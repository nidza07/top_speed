using System;
using System.Collections.Generic;
using System.Numerics;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TopSpeed.Tracks.Geometry;

namespace TopSpeed.Tracks.Surfaces
{
    internal static class SurfaceMeshBuilder
    {
        public static TrackSurfaceMesh? BuildSurface(
            TrackSurfaceDefinition surface,
            GeometryDefinition geometry,
            SurfaceProfileEvaluator profile,
            SurfaceBankEvaluator bank,
            float baseHeight,
            float defaultWidth,
            float defaultResolution)
        {
            if (surface == null || geometry == null)
                return null;

            var resolution = surface.ResolutionMeters ?? defaultResolution;
            resolution = Math.Max(0.25f, resolution);

            switch (surface.Type)
            {
                case TrackSurfaceType.Loft:
                    return BuildLoftSurface(surface, geometry, profile, bank, baseHeight, defaultWidth, resolution);
                case TrackSurfaceType.Polygon:
                    if (geometry.Type == GeometryType.Mesh)
                        return BuildMeshSurface(surface, geometry, profile, baseHeight, resolution);
                    return BuildPolygonSurface(surface, geometry, profile, baseHeight, resolution);
                case TrackSurfaceType.Mesh:
                    return BuildMeshSurface(surface, geometry, profile, baseHeight, resolution);
                default:
                    return null;
            }
        }

        private static TrackSurfaceMesh? BuildMeshSurface(
            TrackSurfaceDefinition surface,
            GeometryDefinition geometry,
            SurfaceProfileEvaluator profile,
            float baseHeight,
            float resolution)
        {
            if (geometry.Type != GeometryType.Mesh)
                return null;

            var points = geometry.Points;
            if (points == null || points.Count < 3)
                return null;

            var indices = geometry.TriangleIndices;
            var hasIndices = indices != null && indices.Count >= 3;
            if (!hasIndices)
            {
                if ((points.Count % 3) != 0)
                    return null;
                var generated = new List<int>(points.Count);
                for (var i = 0; i < points.Count; i++)
                    generated.Add(i);
                indices = generated;
            }

            if ((indices!.Count % 3) != 0)
                return null;

            var heightMode = ResolveMeshHeightMode(surface.Metadata);
            var yOffset = SurfaceParameterParser.TryGetFloat(surface.Metadata, out var offsetValue, "mesh_y_offset", "mesh_offset", "y_offset", "height_offset")
                ? offsetValue
                : 0f;
            var applyProfile = SurfaceParameterParser.TryGetBool(surface.Metadata, out var applyValue, "mesh_apply_profile", "apply_profile", "profile_on_mesh")
                ? applyValue
                : false;

            var triangles = new List<TrackSurfaceTriangle>(indices.Count / 3);
            for (var i = 0; i < indices.Count; i += 3)
            {
                var ia = indices[i];
                var ib = indices[i + 1];
                var ic = indices[i + 2];
                if ((uint)ia >= (uint)points.Count || (uint)ib >= (uint)points.Count || (uint)ic >= (uint)points.Count)
                    continue;

                var a = ApplyMeshHeight(points[ia], heightMode, baseHeight);
                var b = ApplyMeshHeight(points[ib], heightMode, baseHeight);
                var c = ApplyMeshHeight(points[ic], heightMode, baseHeight);

                if (applyProfile)
                {
                    var ap = new Vector2(a.X, a.Z);
                    var bp = new Vector2(b.X, b.Z);
                    var cp = new Vector2(c.X, c.Z);
                    a.Y += profile.EvaluateAt(ap);
                    b.Y += profile.EvaluateAt(bp);
                    c.Y += profile.EvaluateAt(cp);
                }

                if (Math.Abs(yOffset) > 0.0001f)
                {
                    a.Y += yOffset;
                    b.Y += yOffset;
                    c.Y += yOffset;
                }

                var tangent = b - a;
                triangles.Add(new TrackSurfaceTriangle(a, b, c, tangent));
            }

            if (triangles.Count == 0)
                return null;

            return new TrackSurfaceMesh(surface.Id, surface.MaterialId, surface.Layer, triangles.ToArray(), resolution);
        }

        private static TrackSurfaceMesh? BuildLoftSurface(
            TrackSurfaceDefinition surface,
            GeometryDefinition geometry,
            SurfaceProfileEvaluator profile,
            SurfaceBankEvaluator bank,
            float baseHeight,
            float defaultWidth,
            float resolution)
        {
            if (geometry.Type != GeometryType.Polyline && geometry.Type != GeometryType.Spline)
                return null;

            var path = BuildPathSamples(geometry, resolution, surface.Metadata);
            if (path.Count < 2)
                return null;

            var totalLength = path[path.Count - 1].Distance;
            if (totalLength <= 0.0001f && path.Count > 1)
            {
                totalLength = 0f;
                for (var i = 1; i < path.Count; i++)
                    totalLength += Vector3.Distance(path[i - 1].Position, path[i].Position);
            }

            var (leftWidth, rightWidth) = ResolveSurfaceWidth(surface.Metadata, defaultWidth);
            if (leftWidth <= 0f && rightWidth <= 0f)
                return null;

            var triangles = new List<TrackSurfaceTriangle>();
            var closed = IsClosedPath(path, surface.Metadata);
            if (closed)
                totalLength += Vector3.Distance(path[path.Count - 1].Position, path[0].Position);
            var segmentCount = closed ? path.Count : path.Count - 1;

            var baseOffset = profile.IsAbsolute ? 0f : baseHeight;
            var degToRad = (float)(Math.PI / 180.0);

            for (var i = 0; i < segmentCount; i++)
            {
                var a = path[i];
                var b = path[(i + 1) % path.Count];

                var dir2 = new Vector2(b.Position.X - a.Position.X, b.Position.Z - a.Position.Z);
                if (dir2.LengthSquared() <= 0.000001f)
                    continue;
                dir2 = Vector2.Normalize(dir2);
                var right = new Vector2(dir2.Y, -dir2.X);

                var bDistance = b.Distance;
                if (closed && i == path.Count - 1)
                    bDistance = totalLength;

                var bankDegA = bank.EvaluateDegrees(a.Distance, totalLength);
                var bankDegB = bank.EvaluateDegrees(bDistance, totalLength);
                var slopeA = (float)Math.Tan(bankDegA * degToRad);
                var slopeB = (float)Math.Tan(bankDegB * degToRad);
                var bankSign = bank.Side == TrackBankSide.Left ? -1f : 1f;

                var profileA = profile.EvaluateAlong(a.Distance, totalLength);
                var profileB = profile.EvaluateAlong(bDistance, totalLength);
                var baseA = baseOffset + a.Position.Y + profileA;
                var baseB = baseOffset + b.Position.Y + profileB;

                var leftOffset = -leftWidth;
                var rightOffset = rightWidth;

                var leftA2 = new Vector2(a.Position.X, a.Position.Z) + right * leftOffset;
                var rightA2 = new Vector2(a.Position.X, a.Position.Z) + right * rightOffset;
                var leftB2 = new Vector2(b.Position.X, b.Position.Z) + right * leftOffset;
                var rightB2 = new Vector2(b.Position.X, b.Position.Z) + right * rightOffset;

                var leftAY = baseA + (bankSign * slopeA * leftOffset);
                var rightAY = baseA + (bankSign * slopeA * rightOffset);
                var leftBY = baseB + (bankSign * slopeB * leftOffset);
                var rightBY = baseB + (bankSign * slopeB * rightOffset);

                var leftA3 = new Vector3(leftA2.X, leftAY, leftA2.Y);
                var rightA3 = new Vector3(rightA2.X, rightAY, rightA2.Y);
                var leftB3 = new Vector3(leftB2.X, leftBY, leftB2.Y);
                var rightB3 = new Vector3(rightB2.X, rightBY, rightB2.Y);

                var tangent = new Vector3(b.Position.X - a.Position.X, baseB - baseA, b.Position.Z - a.Position.Z);

                triangles.Add(new TrackSurfaceTriangle(leftA3, rightA3, rightB3, tangent));
                triangles.Add(new TrackSurfaceTriangle(leftA3, rightB3, leftB3, tangent));
            }

            if (triangles.Count == 0)
                return null;

            return new TrackSurfaceMesh(surface.Id, surface.MaterialId, surface.Layer, triangles.ToArray(), resolution);
        }

        private static TrackSurfaceMesh? BuildPolygonSurface(
            TrackSurfaceDefinition surface,
            GeometryDefinition geometry,
            SurfaceProfileEvaluator profile,
            float baseHeight,
            float resolution)
        {
            if (!TryGetPolygonContours(geometry, out var outer, out var holes))
                return null;

            if (outer.Count < 3)
                return null;

            if (!TryGetPlane(geometry.Points, out var planeNormal, out var planeD))
                return null;
            if (Math.Abs(planeNormal.Y) <= 0.000001f)
                return null;

            var triangles2D = new List<Triangle2D>();
            if (!TryTriangulateContours(outer, holes, triangles2D))
                return null;

            var triangles = new List<TrackSurfaceTriangle>(triangles2D.Count);
            var baseOffset = profile.IsAbsolute ? 0f : baseHeight;
            foreach (var tri in triangles2D)
            {
                var a = tri.A;
                var b = tri.B;
                var c = tri.C;

                var ha = baseOffset + profile.EvaluateAt(a) + ResolvePlaneY(planeNormal, planeD, a.X, a.Y);
                var hb = baseOffset + profile.EvaluateAt(b) + ResolvePlaneY(planeNormal, planeD, b.X, b.Y);
                var hc = baseOffset + profile.EvaluateAt(c) + ResolvePlaneY(planeNormal, planeD, c.X, c.Y);

                var a3 = new Vector3(a.X, ha, a.Y);
                var b3 = new Vector3(b.X, hb, b.Y);
                var c3 = new Vector3(c.X, hc, c.Y);

                triangles.Add(new TrackSurfaceTriangle(a3, b3, c3, Vector3.UnitZ));
            }

            if (triangles.Count == 0)
                return null;

            return new TrackSurfaceMesh(surface.Id, surface.MaterialId, surface.Layer, triangles.ToArray(), resolution);
        }

        private static List<PathSample> BuildPathSamples(GeometryDefinition geometry, float resolution, IReadOnlyDictionary<string, string> metadata)
        {
            var samples = new List<Vector3>();
            if (geometry.Type == GeometryType.Spline)
                samples.AddRange(SampleSpline(geometry.Points, resolution));
            else
                samples.AddRange(SamplePolyline(geometry.Points, resolution));

            if (samples.Count < 2)
                return new List<PathSample>();

            var isClosed = IsClosedPath(samples, metadata);
            if (isClosed && Vector3.DistanceSquared(samples[0], samples[samples.Count - 1]) <= 0.0001f)
                samples.RemoveAt(samples.Count - 1);

            var path = new List<PathSample>(samples.Count);
            var distance = 0f;
            for (var i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                    distance += Vector3.Distance(samples[i - 1], samples[i]);
                path.Add(new PathSample(samples[i], distance));
            }

            return path;
        }

        private static List<Vector3> SamplePolyline(IReadOnlyList<Vector3> points, float resolution)
        {
            var samples = new List<Vector3>();
            if (points == null || points.Count == 0)
                return samples;

            var step = Math.Max(0.25f, resolution);
            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var segment = b - a;
                var length = segment.Length();
                var steps = Math.Max(1, (int)Math.Ceiling(length / step));
                for (var s = 0; s <= steps; s++)
                {
                    var t = steps == 0 ? 0f : (float)s / steps;
                    var point = a + (segment * t);
                    if (samples.Count == 0 || Vector3.DistanceSquared(samples[samples.Count - 1], point) > 0.0001f)
                        samples.Add(point);
                }
            }

            if (points.Count == 1)
                samples.Add(points[0]);

            return samples;
        }

        private static List<Vector3> SampleSpline(IReadOnlyList<Vector3> points, float resolution)
        {
            var samples = new List<Vector3>();
            if (points == null || points.Count < 2)
                return samples;

            var step = Math.Max(0.25f, resolution);
            for (var i = 0; i < points.Count - 1; i++)
            {
                var p0 = points[Math.Max(0, i - 1)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[Math.Min(points.Count - 1, i + 2)];

                var segmentLength = Vector3.Distance(p1, p2);
                var segments = Math.Max(1, (int)Math.Ceiling(segmentLength / step));
                for (var s = 0; s <= segments; s++)
                {
                    var t = segments == 0 ? 0f : (float)s / segments;
                    var point = CatmullRom(p0, p1, p2, p3, t);
                    if (samples.Count == 0 || Vector3.DistanceSquared(samples[samples.Count - 1], point) > 0.0001f)
                        samples.Add(point);
                }
            }

            return samples;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;

            var x = 0.5f * ((2f * p1.X) +
                            (-p0.X + p2.X) * t +
                            (2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * t2 +
                            (-p0.X + 3f * p1.X - 3f * p2.X + p3.X) * t3);

            var y = 0.5f * ((2f * p1.Y) +
                            (-p0.Y + p2.Y) * t +
                            (2f * p0.Y - 5f * p1.Y + 4f * p2.Y - p3.Y) * t2 +
                            (-p0.Y + 3f * p1.Y - 3f * p2.Y + p3.Y) * t3);

            var z = 0.5f * ((2f * p1.Z) +
                            (-p0.Z + p2.Z) * t +
                            (2f * p0.Z - 5f * p1.Z + 4f * p2.Z - p3.Z) * t2 +
                            (-p0.Z + 3f * p1.Z - 3f * p2.Z + p3.Z) * t3);

            return new Vector3(x, y, z);
        }

        private static (float left, float right) ResolveSurfaceWidth(IReadOnlyDictionary<string, string> metadata, float defaultWidth)
        {
            var width = SurfaceParameterParser.TryGetFloat(metadata, out var widthValue, "width", "surface_width", "path_width", "track_width", "lane_width", "road_width")
                ? widthValue
                : defaultWidth;

            var hasLeft = SurfaceParameterParser.TryGetFloat(metadata, out var leftValue, "left", "left_width", "left_offset");
            var hasRight = SurfaceParameterParser.TryGetFloat(metadata, out var rightValue, "right", "right_width", "right_offset");

            if (hasLeft || hasRight)
            {
                var left = hasLeft ? Math.Abs(leftValue) : Math.Abs(width) * 0.5f;
                var right = hasRight ? Math.Abs(rightValue) : Math.Abs(width) * 0.5f;
                return (left, right);
            }

            var half = Math.Abs(width) * 0.5f;
            return (half, half);
        }

        private static bool IsClosedPath(IReadOnlyList<PathSample> path, IReadOnlyDictionary<string, string> metadata)
        {
            if (path.Count < 3)
                return false;
            if (SurfaceParameterParser.TryGetBool(metadata, out var closedValue, "closed", "loop", "closed_path", "is_closed"))
                return closedValue;
            return Vector3.DistanceSquared(path[0].Position, path[path.Count - 1].Position) <= 0.0001f;
        }

        private static bool IsClosedPath(IReadOnlyList<Vector3> points, IReadOnlyDictionary<string, string> metadata)
        {
            if (points.Count < 3)
                return false;
            if (SurfaceParameterParser.TryGetBool(metadata, out var closedValue, "closed", "loop", "closed_path", "is_closed"))
                return closedValue;
            return Vector3.DistanceSquared(points[0], points[points.Count - 1]) <= 0.0001f;
        }

        private static bool TryGetPolygonContours(
            GeometryDefinition geometry,
            out List<Vector2> outer,
            out List<IReadOnlyList<Vector2>> holes)
        {
            outer = new List<Vector2>();
            holes = new List<IReadOnlyList<Vector2>>();

            if (geometry == null || geometry.Type != GeometryType.Polygon)
                return false;

            var normalized = NormalizePolygonPoints(ProjectToXZ(geometry.Points));
            outer.AddRange(normalized);
            return outer.Count >= 3;
        }

        private static List<Vector2> NormalizePolygonPoints(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
                return new List<Vector2>();

            var list = new List<Vector2>(points.Count);
            for (var i = 0; i < points.Count; i++)
                list.Add(points[i]);

            if (list.Count > 2 && Vector2.DistanceSquared(list[0], list[list.Count - 1]) <= 0.0001f)
                list.RemoveAt(list.Count - 1);

            return list;
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

        private static bool TryGetPlane(IReadOnlyList<Vector3> points, out Vector3 normal, out float planeD)
        {
            normal = Vector3.UnitY;
            planeD = 0f;
            if (points == null || points.Count < 3)
                return false;

            var origin = points[0];
            for (var i = 1; i < points.Count - 1; i++)
            {
                var ab = points[i] - origin;
                var ac = points[i + 1] - origin;
                var cross = Vector3.Cross(ab, ac);
                if (cross.LengthSquared() <= 0.000001f)
                    continue;
                normal = Vector3.Normalize(cross);
                planeD = -(normal.X * origin.X + normal.Y * origin.Y + normal.Z * origin.Z);
                return true;
            }

            return false;
        }

        private static Vector3 ApplyMeshHeight(Vector3 point, MeshHeightMode mode, float baseHeight)
        {
            switch (mode)
            {
                case MeshHeightMode.Relative:
                    return new Vector3(point.X, point.Y + baseHeight, point.Z);
                case MeshHeightMode.Base:
                    return new Vector3(point.X, baseHeight, point.Z);
                case MeshHeightMode.Absolute:
                default:
                    return point;
            }
        }

        private static MeshHeightMode ResolveMeshHeightMode(IReadOnlyDictionary<string, string> metadata)
        {
            if (SurfaceParameterParser.TryGetValue(metadata, out var raw, "mesh_height_mode", "mesh_y_mode", "height_mode", "mesh_height"))
            {
                var trimmed = raw.Trim().ToLowerInvariant();
                switch (trimmed)
                {
                    case "relative":
                    case "add_base":
                    case "add":
                    case "base_add":
                        return MeshHeightMode.Relative;
                    case "base":
                    case "flat":
                    case "plane":
                        return MeshHeightMode.Base;
                    case "absolute":
                    case "world":
                    case "raw":
                        return MeshHeightMode.Absolute;
                }
            }

            return MeshHeightMode.Absolute;
        }

        private static float ResolvePlaneY(Vector3 normal, float planeD, float x, float z)
        {
            if (Math.Abs(normal.Y) <= 0.000001f)
                return 0f;
            return (-planeD - (normal.X * x) - (normal.Z * z)) / normal.Y;
        }

        private static bool TryTriangulateContours(
            IReadOnlyList<Vector2> outer,
            IReadOnlyList<IReadOnlyList<Vector2>> holes,
            List<Triangle2D> output)
        {
            output.Clear();
            if (outer == null || outer.Count < 3)
                return false;

            try
            {
                var polygon = new Polygon();
                polygon.Add(new Contour(ToVertices(outer)), false);
                if (holes != null)
                {
                    foreach (var hole in holes)
                    {
                        if (hole == null || hole.Count < 3)
                            continue;
                        polygon.Add(new Contour(ToVertices(hole)), true);
                    }
                }

                var options = new ConstraintOptions { ConformingDelaunay = true };
                var quality = new QualityOptions();
                var mesh = polygon.Triangulate(options, quality);
                foreach (var tri in mesh.Triangles)
                {
                    var a = tri.GetVertex(0);
                    var b = tri.GetVertex(1);
                    var c = tri.GetVertex(2);
                    output.Add(new Triangle2D(
                        new Vector2((float)a.X, (float)a.Y),
                        new Vector2((float)b.X, (float)b.Y),
                        new Vector2((float)c.X, (float)c.Y)));
                }

                return output.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static Vertex[] ToVertices(IReadOnlyList<Vector2> points)
        {
            var vertices = new Vertex[points.Count];
            for (var i = 0; i < points.Count; i++)
                vertices[i] = new Vertex(points[i].X, points[i].Y);
            return vertices;
        }

        private readonly struct Triangle2D
        {
            public Triangle2D(Vector2 a, Vector2 b, Vector2 c)
            {
                A = a;
                B = b;
                C = c;
            }

            public Vector2 A { get; }
            public Vector2 B { get; }
            public Vector2 C { get; }
        }

        private readonly struct PathSample
        {
            public PathSample(Vector3 position, float distance)
            {
                Position = position;
                Distance = distance;
            }

            public Vector3 Position { get; }
            public float Distance { get; }
        }

        private enum MeshHeightMode
        {
            Absolute = 0,
            Relative = 1,
            Base = 2
        }
    }
}
