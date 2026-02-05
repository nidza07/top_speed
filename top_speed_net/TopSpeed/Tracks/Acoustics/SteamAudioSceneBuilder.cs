using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using SteamAudio;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TopSpeed.Tracks.Areas;
using TopSpeed.Tracks.Map;
using TopSpeed.Tracks.Materials;
using TopSpeed.Tracks.Geometry;
using TopSpeed.Tracks.Walls;
using TS.Audio;

namespace TopSpeed.Tracks.Acoustics
{
    internal static class SteamAudioSceneBuilder
    {
        public static TrackSteamAudioScene? Build(TrackMap map, SteamAudioContext context)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.Context.Handle == IntPtr.Zero)
                return null;

            var geometriesById = new Dictionary<string, GeometryDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var geometry in map.Geometries)
            {
                if (geometry == null)
                    continue;
                geometriesById[geometry.Id] = geometry;
            }

            var materialLookup = new MaterialLookup(map);
            var vertices = new List<IPL.Vector3>();
            var triangles = new List<IPL.Triangle>();
            var materialIndices = new List<int>();

            foreach (var area in map.Areas)
            {
                if (area == null || IsOverlayArea(area))
                    continue;
                if (!geometriesById.TryGetValue(area.GeometryId, out var geometry))
                    continue;
                var materialIndex = materialLookup.GetIndex(area.MaterialId);
                AddAreaGeometry(geometry, area, materialIndex, vertices, triangles, materialIndices);
            }

            foreach (var wall in map.Walls)
            {
                if (wall == null)
                    continue;
                if (!geometriesById.TryGetValue(wall.GeometryId, out var geometry))
                    continue;
                var materialIndex = materialLookup.GetIndex(wall.MaterialId);
                AddWallGeometry(geometry, wall, materialIndex, vertices, triangles, materialIndices);
            }

            if (vertices.Count == 0 || triangles.Count == 0)
                return null;

            var materials = materialLookup.ToIplMaterials();
            var sceneSettings = new IPL.SceneSettings
            {
                Type = IPL.SceneType.Default
            };

            var sceneError = IPL.SceneCreate(context.Context, in sceneSettings, out var scene);
            if (sceneError != IPL.Error.Success)
                throw new InvalidOperationException("Failed to create Steam Audio scene: " + sceneError);

            GCHandle verticesHandle = default;
            GCHandle trianglesHandle = default;
            GCHandle materialIndexHandle = default;
            GCHandle materialsHandle = default;

            try
            {
                var vertexArray = vertices.ToArray();
                var triangleArray = triangles.ToArray();
                var materialIndexArray = materialIndices.ToArray();

                verticesHandle = GCHandle.Alloc(vertexArray, GCHandleType.Pinned);
                trianglesHandle = GCHandle.Alloc(triangleArray, GCHandleType.Pinned);
                materialIndexHandle = GCHandle.Alloc(materialIndexArray, GCHandleType.Pinned);
                materialsHandle = GCHandle.Alloc(materials, GCHandleType.Pinned);

                var meshSettings = new IPL.StaticMeshSettings
                {
                    NumVertices = vertexArray.Length,
                    NumTriangles = triangleArray.Length,
                    NumMaterials = materials.Length,
                    Vertices = verticesHandle.AddrOfPinnedObject(),
                    Triangles = trianglesHandle.AddrOfPinnedObject(),
                    MaterialIndices = materialIndexHandle.AddrOfPinnedObject(),
                    Materials = materialsHandle.AddrOfPinnedObject()
                };

                var meshError = IPL.StaticMeshCreate(scene, in meshSettings, out var mesh);
                if (meshError != IPL.Error.Success)
                {
                    IPL.SceneRelease(ref scene);
                    throw new InvalidOperationException("Failed to create Steam Audio static mesh: " + meshError);
                }

                IPL.StaticMeshAdd(mesh, scene);
                IPL.SceneCommit(scene);
                var hasBaked = TryBakeReflections(scene, context, map, vertexArray, out var probeBatch, out var bakedIdentifier, out var portalBakedIdentifiers);
                return new TrackSteamAudioScene(scene, mesh, probeBatch, bakedIdentifier, hasBaked, portalBakedIdentifiers);
            }
            finally
            {
                if (materialsHandle.IsAllocated)
                    materialsHandle.Free();
                if (materialIndexHandle.IsAllocated)
                    materialIndexHandle.Free();
                if (trianglesHandle.IsAllocated)
                    trianglesHandle.Free();
                if (verticesHandle.IsAllocated)
                    verticesHandle.Free();
            }
        }

        private static bool TryBakeReflections(
            IPL.Scene scene,
            SteamAudioContext context,
            TrackMap map,
            IPL.Vector3[] vertices,
            out IPL.ProbeBatch probeBatch,
            out IPL.BakedDataIdentifier bakedIdentifier,
            out Dictionary<string, IPL.BakedDataIdentifier> portalBakedIdentifiers)
        {
            probeBatch = default;
            bakedIdentifier = default;
            portalBakedIdentifiers = new Dictionary<string, IPL.BakedDataIdentifier>(StringComparer.OrdinalIgnoreCase);

            if (vertices == null || vertices.Length == 0)
                return false;

            var bounds = ComputeBounds(vertices);
            if (bounds.Max.X <= bounds.Min.X || bounds.Max.Z <= bounds.Min.Z)
                return false;

            var probeArray = default(IPL.ProbeArray);
            var probeArrayError = IPL.ProbeArrayCreate(context.Context, out probeArray);
            if (probeArrayError != IPL.Error.Success)
                return false;

            try
            {
                using (context.AcquireSimulationLock())
                {
                    var spacing = 20f;
                    var height = 1.5f;
                    var transform = CreateBoundsTransform(bounds.Min, bounds.Max);

                    var genParams = new IPL.ProbeGenerationParams
                    {
                        Type = IPL.ProbeGenerationType.UniformFloor,
                        Spacing = spacing,
                        Height = height,
                        Transform = transform
                    };

                    IPL.ProbeArrayGenerateProbes(probeArray, scene, ref genParams);
                    if (IPL.ProbeArrayGetNumProbes(probeArray) <= 0)
                        return false;

                    var batchError = IPL.ProbeBatchCreate(context.Context, out probeBatch);
                    if (batchError != IPL.Error.Success)
                        return false;

                    IPL.ProbeBatchAddProbeArray(probeBatch, probeArray);
                    IPL.ProbeBatchCommit(probeBatch);

                    var center = new IPL.Vector3
                    {
                        X = (bounds.Min.X + bounds.Max.X) * 0.5f,
                        Y = (bounds.Min.Y + bounds.Max.Y) * 0.5f,
                        Z = (bounds.Min.Z + bounds.Max.Z) * 0.5f
                    };
                    var radius = Math.Max(bounds.Max.X - bounds.Min.X, bounds.Max.Z - bounds.Min.Z);
                    radius = Math.Max(radius, bounds.Max.Y - bounds.Min.Y);
                    radius = Math.Max(radius, 10f);

                    bakedIdentifier = new IPL.BakedDataIdentifier
                    {
                        Type = IPL.BakedDataType.Reflections,
                        Variation = IPL.BakedDataVariation.Reverb,
                        EndpointInfluence = new IPL.Sphere
                        {
                            Center = center,
                            Radius = radius
                        }
                    };

                    var bakeParams = new IPL.ReflectionsBakeParams
                    {
                        Scene = scene,
                        ProbeBatch = probeBatch,
                        SceneType = IPL.SceneType.Default,
                        Identifier = bakedIdentifier,
                        BakeFlags = IPL.ReflectionsBakeFlags.BakeConvolution | IPL.ReflectionsBakeFlags.BakeParametric,
                        NumRays = 2048,
                        NumDiffuseSamples = 256,
                        NumBounces = 4,
                        SimulatedDuration = context.ReflectionDurationSeconds,
                        SavedDuration = context.ReflectionDurationSeconds,
                        Order = context.ReflectionOrder,
                        NumThreads = Math.Max(1, Environment.ProcessorCount - 1),
                        RayBatchSize = 64,
                        IrradianceMinDistance = 1.0f,
                        BakeBatchSize = 16,
                        OpenCLDevice = default,
                        RadeonRaysDevice = default
                    };

                    IPL.ReflectionsBakerBake(context.Context, ref bakeParams, null, IntPtr.Zero);
                    BakeStaticSourceReflections(context, map, bounds, bakeParams, portalBakedIdentifiers);
                    return true;
                }
            }
            catch
            {
                if (probeBatch.Handle != IntPtr.Zero)
                    IPL.ProbeBatchRelease(ref probeBatch);
                return false;
            }
            finally
            {
                if (probeArray.Handle != IntPtr.Zero)
                    IPL.ProbeArrayRelease(ref probeArray);
            }
        }

        private static (IPL.Vector3 Min, IPL.Vector3 Max) ComputeBounds(IReadOnlyList<IPL.Vector3> vertices)
        {
            var min = new IPL.Vector3 { X = float.MaxValue, Y = float.MaxValue, Z = float.MaxValue };
            var max = new IPL.Vector3 { X = float.MinValue, Y = float.MinValue, Z = float.MinValue };

            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                if (v.X < min.X) min.X = v.X;
                if (v.Y < min.Y) min.Y = v.Y;
                if (v.Z < min.Z) min.Z = v.Z;
                if (v.X > max.X) max.X = v.X;
                if (v.Y > max.Y) max.Y = v.Y;
                if (v.Z > max.Z) max.Z = v.Z;
            }

            return (min, max);
        }

        private static void BakeStaticSourceReflections(
            SteamAudioContext context,
            TrackMap map,
            (IPL.Vector3 Min, IPL.Vector3 Max) bounds,
            IPL.ReflectionsBakeParams bakeParams,
            Dictionary<string, IPL.BakedDataIdentifier> portalBakedIdentifiers)
        {
            if (map == null || map.Portals.Count == 0)
                return;

            var span = Math.Max(bounds.Max.X - bounds.Min.X, bounds.Max.Z - bounds.Min.Z);
            var influenceRadius = Math.Max(50f, Math.Min(120f, span * 0.5f));
            foreach (var portal in map.Portals)
            {
                if (portal == null || string.IsNullOrWhiteSpace(portal.Id))
                    continue;

                var portalId = portal.Id.Trim();
                if (portalBakedIdentifiers.ContainsKey(portalId))
                    continue;

                var identifier = new IPL.BakedDataIdentifier
                {
                    Type = IPL.BakedDataType.Reflections,
                    Variation = IPL.BakedDataVariation.StaticSource,
                    EndpointInfluence = new IPL.Sphere
                    {
                        Center = new IPL.Vector3
                        {
                            X = portal.X,
                            Y = portal.Y,
                            Z = portal.Z
                        },
                        Radius = influenceRadius
                    }
                };

                var portalBakeParams = bakeParams;
                portalBakeParams.Identifier = identifier;
                IPL.ReflectionsBakerBake(context.Context, ref portalBakeParams, null, IntPtr.Zero);
                portalBakedIdentifiers[portalId] = identifier;
            }
        }

        private static unsafe IPL.Matrix4x4 CreateBoundsTransform(in IPL.Vector3 min, in IPL.Vector3 max)
        {
            var scaleX = max.X - min.X;
            var scaleY = max.Y - min.Y;
            var scaleZ = max.Z - min.Z;

            IPL.Matrix4x4 matrix = default;
            matrix.Elements[0] = scaleX;
            matrix.Elements[1] = 0f;
            matrix.Elements[2] = 0f;
            matrix.Elements[3] = min.X;
            matrix.Elements[4] = 0f;
            matrix.Elements[5] = scaleY <= 0f ? 1f : scaleY;
            matrix.Elements[6] = 0f;
            matrix.Elements[7] = min.Y;
            matrix.Elements[8] = 0f;
            matrix.Elements[9] = 0f;
            matrix.Elements[10] = scaleZ;
            matrix.Elements[11] = min.Z;
            matrix.Elements[12] = 0f;
            matrix.Elements[13] = 0f;
            matrix.Elements[14] = 0f;
            matrix.Elements[15] = 1f;
            return matrix;
        }

        private static bool IsOverlayArea(TrackAreaDefinition area)
        {
            switch (area.Type)
            {
                case TrackAreaType.Start:
                case TrackAreaType.Finish:
                case TrackAreaType.Checkpoint:
                case TrackAreaType.Intersection:
                    return true;
                default:
                    return false;
            }
        }

        private static void AddAreaGeometry(
            GeometryDefinition geometry,
            TrackAreaDefinition area,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices)
        {
            if (geometry == null)
                return;

            if (geometry.Type == GeometryType.Mesh)
            {
                AddMeshGeometry(geometry, materialIndex, vertices, triangles, materialIndices);
                return;
            }

            if (geometry.Type != GeometryType.Polygon)
                return;

            var points = NormalizePolygonPoints(ProjectToXZ(geometry.Points));
            if (points.Length < 3)
                return;

            var elevation = area.ElevationMeters;
            var ceiling = area.CeilingHeightMeters;

            AddPolygonSurface(points, elevation, materialIndex, vertices, triangles, materialIndices, flipWinding: false);
            if (ceiling.HasValue)
                AddPolygonSurface(points, ceiling.Value, materialIndex, vertices, triangles, materialIndices, flipWinding: true);
        }

        private static void AddWallGeometry(
            GeometryDefinition geometry,
            TrackWallDefinition wall,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices)
        {
            if (geometry == null)
                return;

            if (geometry.Type == GeometryType.Mesh)
            {
                AddMeshGeometry(geometry, materialIndex, vertices, triangles, materialIndices);
                return;
            }

            var baseHeight = wall.ElevationMeters;
            var top = baseHeight + Math.Max(0f, wall.HeightMeters);
            if (top <= baseHeight + 0.01f)
                return;

            var points2D = ProjectToXZ(geometry.Points);
            switch (geometry.Type)
            {
                case GeometryType.Polygon:
                    AddPolygonWalls(points2D, baseHeight, top, materialIndex, vertices, triangles, materialIndices, closed: true);
                    break;
                case GeometryType.Polyline:
                case GeometryType.Spline:
                    AddPolygonWalls(points2D, baseHeight, top, materialIndex, vertices, triangles, materialIndices, closed: false);
                    break;
                case GeometryType.Undefined:
                default:
                    break;
            }
        }

        private static void AddMeshGeometry(
            GeometryDefinition geometry,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices)
        {
            if (geometry == null || geometry.Type != GeometryType.Mesh)
                return;

            var points = geometry.Points;
            if (points == null || points.Count < 3)
                return;

            var indices = geometry.TriangleIndices;
            if (indices == null || indices.Count == 0)
            {
                if ((points.Count % 3) != 0)
                    return;
                for (var i = 0; i < points.Count; i += 3)
                {
                    var a = points[i];
                    var b = points[i + 1];
                    var c = points[i + 2];
                    AddTriangle(
                        new IPL.Vector3 { X = a.X, Y = a.Y, Z = a.Z },
                        new IPL.Vector3 { X = b.X, Y = b.Y, Z = b.Z },
                        new IPL.Vector3 { X = c.X, Y = c.Y, Z = c.Z },
                        materialIndex,
                        vertices,
                        triangles,
                        materialIndices);
                }
                return;
            }

            if ((indices.Count % 3) != 0)
                return;

            for (var i = 0; i < indices.Count; i += 3)
            {
                var ia = indices[i];
                var ib = indices[i + 1];
                var ic = indices[i + 2];
                if ((uint)ia >= (uint)points.Count || (uint)ib >= (uint)points.Count || (uint)ic >= (uint)points.Count)
                    continue;
                var a = points[ia];
                var b = points[ib];
                var c = points[ic];
                AddTriangle(
                    new IPL.Vector3 { X = a.X, Y = a.Y, Z = a.Z },
                    new IPL.Vector3 { X = b.X, Y = b.Y, Z = b.Z },
                    new IPL.Vector3 { X = c.X, Y = c.Y, Z = c.Z },
                    materialIndex,
                    vertices,
                    triangles,
                    materialIndices);
            }
        }

        private static void AddPolygonSurface(
            IReadOnlyList<Vector2> points,
            float y,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices,
            bool flipWinding)
        {
            if (points == null || points.Count < 3)
                return;

            var normalized = NormalizePolygonPoints(points);
            if (normalized.Length < 3)
                return;

            if (!AddTriangulatedSurface(normalized, null, y, materialIndex, vertices, triangles, materialIndices, flipWinding))
                AddPolygonFan(normalized, y, materialIndex, vertices, triangles, materialIndices, flipWinding);
        }

        private static void AddPolygonFan(
            IReadOnlyList<Vector2> points,
            float y,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices,
            bool flipWinding)
        {
            if (points == null || points.Count < 3)
                return;

            var centroid = Vector2.Zero;
            for (var i = 0; i < points.Count; i++)
                centroid += points[i];
            centroid /= points.Count;

            var center = new IPL.Vector3 { X = centroid.X, Y = y, Z = centroid.Y };

            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];

                var v1 = new IPL.Vector3 { X = a.X, Y = y, Z = a.Y };
                var v2 = new IPL.Vector3 { X = b.X, Y = y, Z = b.Y };

                if (!flipWinding)
                    AddTriangle(center, v1, v2, materialIndex, vertices, triangles, materialIndices);
                else
                    AddTriangle(center, v2, v1, materialIndex, vertices, triangles, materialIndices);
            }
        }

        private static Vector2[] NormalizePolygonPoints(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
                return Array.Empty<Vector2>();

            var list = new List<Vector2>(points.Count);
            for (var i = 0; i < points.Count; i++)
                list.Add(points[i]);

            if (list.Count > 2 && Vector2.DistanceSquared(list[0], list[list.Count - 1]) <= 0.0001f)
                list.RemoveAt(list.Count - 1);

            return list.ToArray();
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

        private static bool AddTriangulatedSurface(
            IReadOnlyList<Vector2> outer,
            IReadOnlyList<IReadOnlyList<Vector2>>? holes,
            float y,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices,
            bool flipWinding)
        {
            var triangulated = new List<Triangle2D>();
            if (!TryTriangulateContours(outer, holes, triangulated))
                return false;

            var desiredCcw = !flipWinding;
            foreach (var tri in triangulated)
            {
                var a = tri.A;
                var b = tri.B;
                var c = tri.C;
                var triCcw = SignedArea(a, b, c) >= 0f;
                if (triCcw != desiredCcw)
                {
                    var temp = b;
                    b = c;
                    c = temp;
                }

                var v1 = new IPL.Vector3 { X = a.X, Y = y, Z = a.Y };
                var v2 = new IPL.Vector3 { X = b.X, Y = y, Z = b.Y };
                var v3 = new IPL.Vector3 { X = c.X, Y = y, Z = c.Y };
                AddTriangle(v1, v2, v3, materialIndex, vertices, triangles, materialIndices);
            }

            return true;
        }

        private static float SignedArea(IReadOnlyList<Vector2> points)
        {
            var sum = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                sum += (a.X * b.Y) - (b.X * a.Y);
            }
            return sum * 0.5f;
        }

        private static float SignedArea(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) * 0.5f;
        }

        private static bool TryTriangulateContours(
            IReadOnlyList<Vector2> outer,
            IReadOnlyList<IReadOnlyList<Vector2>>? holes,
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
                    var v0 = tri.GetVertex(0);
                    var v1 = tri.GetVertex(1);
                    var v2 = tri.GetVertex(2);
                    output.Add(new Triangle2D(
                        new Vector2((float)v0.X, (float)v0.Y),
                        new Vector2((float)v1.X, (float)v1.Y),
                        new Vector2((float)v2.X, (float)v2.Y)));
                }

                return output.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static List<Vertex> ToVertices(IReadOnlyList<Vector2> points)
        {
            var vertices = new List<Vertex>(points.Count);
            for (var i = 0; i < points.Count; i++)
                vertices.Add(new Vertex(points[i].X, points[i].Y));
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

        private static void AddPolygonWalls(
            IReadOnlyList<Vector2> points,
            float baseHeight,
            float topHeight,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices,
            bool closed)
        {
            if (points == null || points.Count < 2)
                return;

            var count = points.Count;
            var segments = closed ? count : count - 1;
            for (var i = 0; i < segments; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % count];
                AddWallSegment(a, b, baseHeight, topHeight, materialIndex, vertices, triangles, materialIndices);
            }
        }

        private static void AddWallSegment(
            Vector2 a,
            Vector2 b,
            float baseHeight,
            float topHeight,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices)
        {
            if (Vector2.DistanceSquared(a, b) <= 0.0001f)
                return;

            var v0 = new IPL.Vector3 { X = a.X, Y = baseHeight, Z = a.Y };
            var v1 = new IPL.Vector3 { X = b.X, Y = baseHeight, Z = b.Y };
            var v2 = new IPL.Vector3 { X = b.X, Y = topHeight, Z = b.Y };
            var v3 = new IPL.Vector3 { X = a.X, Y = topHeight, Z = a.Y };

            AddQuad(v0, v1, v2, v3, materialIndex, vertices, triangles, materialIndices, doubleSided: true);
        }

        private static void AddQuad(
            IPL.Vector3 a,
            IPL.Vector3 b,
            IPL.Vector3 c,
            IPL.Vector3 d,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices,
            bool doubleSided,
            bool flipWinding = false)
        {
            if (flipWinding)
            {
                var temp = b;
                b = d;
                d = temp;
            }

            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            AddTriangle(start, start + 1, start + 2, materialIndex, vertices, triangles, materialIndices);
            AddTriangle(start, start + 2, start + 3, materialIndex, vertices, triangles, materialIndices);

            if (!doubleSided)
                return;

            AddTriangle(start + 2, start + 1, start, materialIndex, vertices, triangles, materialIndices);
            AddTriangle(start + 3, start + 2, start, materialIndex, vertices, triangles, materialIndices);
        }

        private static void AddTriangle(
            IPL.Vector3 a,
            IPL.Vector3 b,
            IPL.Vector3 c,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices)
        {
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            AddTriangle(start, start + 1, start + 2, materialIndex, vertices, triangles, materialIndices);
        }

        private static unsafe void AddTriangle(
            int indexA,
            int indexB,
            int indexC,
            int materialIndex,
            List<IPL.Vector3> vertices,
            List<IPL.Triangle> triangles,
            List<int> materialIndices)
        {
            var tri = new IPL.Triangle();
            tri.Indices[0] = indexA;
            tri.Indices[1] = indexB;
            tri.Indices[2] = indexC;
            triangles.Add(tri);
            materialIndices.Add(materialIndex);
        }

        private sealed class MaterialLookup
        {
            private readonly TrackMap _map;
            private readonly Dictionary<string, TrackMaterialDefinition> _materialsById;
            private readonly Dictionary<string, int> _indices;
            private readonly List<TrackMaterialDefinition> _materials;

            public MaterialLookup(TrackMap map)
            {
                _map = map ?? throw new ArgumentNullException(nameof(map));
                _materialsById = new Dictionary<string, TrackMaterialDefinition>(StringComparer.OrdinalIgnoreCase);
                _indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _materials = new List<TrackMaterialDefinition>();

                foreach (var material in map.Materials)
                {
                    if (material == null)
                        continue;
                    _materialsById[material.Id] = material;
                }
            }

            public int GetIndex(string? materialId)
            {
                var resolved = string.IsNullOrWhiteSpace(materialId) ? _map.DefaultMaterialId : materialId!.Trim();
                if (string.IsNullOrWhiteSpace(resolved))
                    resolved = "generic";

                if (_indices.TryGetValue(resolved, out var index))
                    return index;

                if (!_materialsById.TryGetValue(resolved, out var material))
                {
                    if (!TrackMaterialLibrary.TryGetPreset(resolved, out material))
                    {
                        material = new TrackMaterialDefinition(
                            resolved,
                            resolved,
                            0.10f,
                            0.20f,
                            0.30f,
                            0.05f,
                            0.10f,
                            0.05f,
                            0.03f,
                            TrackWallMaterial.Hard);
                    }

                    _materialsById[resolved] = material;
                }

                index = _materials.Count;
                _materials.Add(material);
                _indices[resolved] = index;
                return index;
            }

            public IPL.Material[] ToIplMaterials()
            {
                var result = new IPL.Material[_materials.Count];
                for (var i = 0; i < _materials.Count; i++)
                {
                    result[i] = ToIplMaterial(_materials[i]);
                }
                return result;
            }

            private static unsafe IPL.Material ToIplMaterial(TrackMaterialDefinition material)
            {
                var ipl = new IPL.Material();
                ipl.Absorption[0] = material.AbsorptionLow;
                ipl.Absorption[1] = material.AbsorptionMid;
                ipl.Absorption[2] = material.AbsorptionHigh;
                ipl.Scattering = material.Scattering;
                ipl.Transmission[0] = material.TransmissionLow;
                ipl.Transmission[1] = material.TransmissionMid;
                ipl.Transmission[2] = material.TransmissionHigh;
                return ipl;
            }
        }
    }
}
