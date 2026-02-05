using System;
using System.Collections.Generic;
using System.Numerics;

namespace TopSpeed.Tracks.Geometry
{
    public readonly struct MeshBvhHit
    {
        public MeshBvhHit(float t, Vector3 position, Vector3 normal)
        {
            T = t;
            Position = position;
            Normal = normal;
        }

        public float T { get; }
        public Vector3 Position { get; }
        public Vector3 Normal { get; }
    }

    public sealed class MeshBvh
    {
        private const int LeafTriangleCount = 8;
        private const float RayEpsilon = 0.000001f;

        private readonly Vector3[] _vertices;
        private readonly int[] _indices;
        private readonly int[] _triangles;
        private readonly Vector3[] _triangleCenters;
        private readonly Aabb[] _triangleBounds;
        private readonly BvhNode[] _nodes;

        private MeshBvh(
            Vector3[] vertices,
            int[] indices,
            int[] triangles,
            Vector3[] triangleCenters,
            Aabb[] triangleBounds,
            BvhNode[] nodes)
        {
            _vertices = vertices;
            _indices = indices;
            _triangles = triangles;
            _triangleCenters = triangleCenters;
            _triangleBounds = triangleBounds;
            _nodes = nodes;
        }

        public static bool TryCreate(GeometryDefinition geometry, out MeshBvh bvh)
        {
            bvh = null!;
            if (geometry == null || geometry.Type != GeometryType.Mesh)
                return false;

            var points = geometry.Points;
            var indices = geometry.TriangleIndices;
            if (points == null || points.Count < 3)
                return false;
            if (indices == null || indices.Count < 3 || (indices.Count % 3) != 0)
                return false;

            var vertexArray = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
                vertexArray[i] = points[i];

            var indexArray = new int[indices.Count];
            for (var i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                if (idx < 0 || idx >= vertexArray.Length)
                    return false;
                indexArray[i] = idx;
            }

            var triangleCount = indexArray.Length / 3;
            var triangles = new int[triangleCount];
            var centers = new Vector3[triangleCount];
            var bounds = new Aabb[triangleCount];

            for (var t = 0; t < triangleCount; t++)
            {
                triangles[t] = t;
                var baseIndex = t * 3;
                var v0 = vertexArray[indexArray[baseIndex]];
                var v1 = vertexArray[indexArray[baseIndex + 1]];
                var v2 = vertexArray[indexArray[baseIndex + 2]];
                bounds[t] = Aabb.FromTriangle(v0, v1, v2);
                centers[t] = (v0 + v1 + v2) / 3f;
            }

            var builder = new BvhBuilder(triangles, centers, bounds);
            var nodes = builder.Build();
            if (nodes.Length == 0)
                return false;

            bvh = new MeshBvh(vertexArray, indexArray, triangles, centers, bounds, nodes);
            return true;
        }

        public bool TryIntersectSegment(Vector3 from, Vector3 to, out MeshBvhHit hit)
        {
            hit = default;
            if (_nodes.Length == 0)
                return false;

            var dir = to - from;
            if (dir.LengthSquared() < 0.0000001f)
                return false;

            var bestT = float.MaxValue;
            var bestNormal = Vector3.Zero;
            var stack = new int[_nodes.Length];
            var stackCount = 0;
            stack[stackCount++] = 0;

            while (stackCount > 0)
            {
                var node = _nodes[stack[--stackCount]];
                if (!IntersectSegmentAabb(node.Bounds, from, dir, bestT))
                    continue;

                if (node.IsLeaf)
                {
                    var start = node.Start;
                    var end = start + node.Count;
                    for (var i = start; i < end; i++)
                    {
                        var triangleIndex = _triangles[i];
                        var baseIndex = triangleIndex * 3;
                        var v0 = _vertices[_indices[baseIndex]];
                        var v1 = _vertices[_indices[baseIndex + 1]];
                        var v2 = _vertices[_indices[baseIndex + 2]];
                        if (IntersectTriangle(from, dir, v0, v1, v2, out var t, out var normal))
                        {
                            if (t < bestT)
                            {
                                bestT = t;
                                bestNormal = normal;
                            }
                        }
                    }
                }
                else
                {
                    stack[stackCount++] = node.Left;
                    stack[stackCount++] = node.Right;
                }
            }

            if (bestT >= float.MaxValue)
                return false;

            var position = from + (dir * bestT);
            hit = new MeshBvhHit(bestT, position, bestNormal);
            return true;
        }

        private static bool IntersectTriangle(
            Vector3 origin,
            Vector3 dir,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            out float t,
            out Vector3 normal)
        {
            t = 0f;
            normal = Vector3.Zero;

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var pvec = Vector3.Cross(dir, edge2);
            var det = Vector3.Dot(edge1, pvec);
            if (det > -RayEpsilon && det < RayEpsilon)
                return false;

            var invDet = 1.0f / det;
            var tvec = origin - v0;
            var u = Vector3.Dot(tvec, pvec) * invDet;
            if (u < 0f || u > 1f)
                return false;

            var qvec = Vector3.Cross(tvec, edge1);
            var v = Vector3.Dot(dir, qvec) * invDet;
            if (v < 0f || (u + v) > 1f)
                return false;

            var hitT = Vector3.Dot(edge2, qvec) * invDet;
            if (hitT <= RayEpsilon || hitT > 1.0f)
                return false;

            var n = Vector3.Cross(edge1, edge2);
            if (n.LengthSquared() > 0.000001f)
                n = Vector3.Normalize(n);

            t = hitT;
            normal = n;
            return true;
        }

        private static bool IntersectSegmentAabb(Aabb bounds, Vector3 origin, Vector3 dir, float maxT)
        {
            var tmin = 0f;
            var tmax = maxT > 1.0f ? 1.0f : maxT;

            if (!IntersectAxis(origin.X, dir.X, bounds.Min.X, bounds.Max.X, ref tmin, ref tmax))
                return false;
            if (!IntersectAxis(origin.Y, dir.Y, bounds.Min.Y, bounds.Max.Y, ref tmin, ref tmax))
                return false;
            if (!IntersectAxis(origin.Z, dir.Z, bounds.Min.Z, bounds.Max.Z, ref tmin, ref tmax))
                return false;

            return tmax >= tmin;
        }

        private static bool IntersectAxis(float origin, float dir, float min, float max, ref float tmin, ref float tmax)
        {
            if (Math.Abs(dir) < RayEpsilon)
                return origin >= min && origin <= max;

            var inv = 1.0f / dir;
            var t1 = (min - origin) * inv;
            var t2 = (max - origin) * inv;
            if (t1 > t2)
            {
                var tmp = t1;
                t1 = t2;
                t2 = tmp;
            }

            if (t1 > tmin)
                tmin = t1;
            if (t2 < tmax)
                tmax = t2;

            return tmax >= tmin;
        }

        private sealed class BvhBuilder
        {
            private readonly int[] _triangles;
            private readonly Vector3[] _centers;
            private readonly Aabb[] _bounds;
            private readonly List<BvhNode> _nodes;
            private readonly TriangleCenterComparer _comparer;

            public BvhBuilder(int[] triangles, Vector3[] centers, Aabb[] bounds)
            {
                _triangles = triangles;
                _centers = centers;
                _bounds = bounds;
                _nodes = new List<BvhNode>(triangles.Length * 2);
                _comparer = new TriangleCenterComparer(centers);
            }

            public BvhNode[] Build()
            {
                if (_triangles.Length == 0)
                    return Array.Empty<BvhNode>();

                BuildNode(0, _triangles.Length);
                return _nodes.ToArray();
            }

            private int BuildNode(int start, int count)
            {
                var bounds = ComputeBounds(start, count);
                if (count <= LeafTriangleCount)
                {
                    var leafIndex = _nodes.Count;
                    _nodes.Add(new BvhNode(bounds, -1, -1, start, count));
                    return leafIndex;
                }

                var axis = ChooseSplitAxis(start, count);
                _comparer.SetAxis(axis);
                Array.Sort(_triangles, start, count, _comparer);
                var mid = start + (count / 2);

                var nodeIndex = _nodes.Count;
                _nodes.Add(default);

                var left = BuildNode(start, mid - start);
                var right = BuildNode(mid, count - (mid - start));
                _nodes[nodeIndex] = new BvhNode(bounds, left, right, 0, 0);
                return nodeIndex;
            }

            private Aabb ComputeBounds(int start, int count)
            {
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                var end = start + count;

                for (var i = start; i < end; i++)
                {
                    var triIndex = _triangles[i];
                    var triBounds = _bounds[triIndex];
                    min = Vector3.Min(min, triBounds.Min);
                    max = Vector3.Max(max, triBounds.Max);
                }

                return new Aabb(min, max);
            }

            private int ChooseSplitAxis(int start, int count)
            {
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                var end = start + count;

                for (var i = start; i < end; i++)
                {
                    var center = _centers[_triangles[i]];
                    min = Vector3.Min(min, center);
                    max = Vector3.Max(max, center);
                }

                var extent = max - min;
                if (extent.X >= extent.Y && extent.X >= extent.Z)
                    return 0;
                if (extent.Y >= extent.Z)
                    return 1;
                return 2;
            }
        }

        private sealed class TriangleCenterComparer : IComparer<int>
        {
            private readonly Vector3[] _centers;
            private int _axis;

            public TriangleCenterComparer(Vector3[] centers)
            {
                _centers = centers;
                _axis = 0;
            }

            public void SetAxis(int axis)
            {
                _axis = axis;
            }

            public int Compare(int x, int y)
            {
                var a = _centers[x];
                var b = _centers[y];
                var va = _axis == 0 ? a.X : (_axis == 1 ? a.Y : a.Z);
                var vb = _axis == 0 ? b.X : (_axis == 1 ? b.Y : b.Z);
                if (va < vb)
                    return -1;
                if (va > vb)
                    return 1;
                return 0;
            }
        }

        private readonly struct BvhNode
        {
            public BvhNode(Aabb bounds, int left, int right, int start, int count)
            {
                Bounds = bounds;
                Left = left;
                Right = right;
                Start = start;
                Count = count;
            }

            public Aabb Bounds { get; }
            public int Left { get; }
            public int Right { get; }
            public int Start { get; }
            public int Count { get; }
            public bool IsLeaf => Count > 0;
        }

        private readonly struct Aabb
        {
            public Aabb(Vector3 min, Vector3 max)
            {
                Min = min;
                Max = max;
            }

            public Vector3 Min { get; }
            public Vector3 Max { get; }

            public static Aabb FromTriangle(Vector3 v0, Vector3 v1, Vector3 v2)
            {
                var min = Vector3.Min(v0, Vector3.Min(v1, v2));
                var max = Vector3.Max(v0, Vector3.Max(v1, v2));
                return new Aabb(min, max);
            }
        }
    }
}
