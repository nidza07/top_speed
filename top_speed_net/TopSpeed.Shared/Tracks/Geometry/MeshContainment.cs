using System;
using System.Collections.Generic;
using System.Numerics;

namespace TopSpeed.Tracks.Geometry
{
    internal sealed class MeshContainment
    {
        internal const float DefaultSurfaceEpsilon = 0.01f;

        private readonly Vector3[] _vertices;
        private readonly int[] _indices;
        private readonly Vector3 _min;
        private readonly Vector3 _max;

        private MeshContainment(Vector3[] vertices, int[] indices, Vector3 min, Vector3 max, bool isClosed)
        {
            _vertices = vertices;
            _indices = indices;
            _min = min;
            _max = max;
            IsClosed = isClosed;
        }

        public bool IsClosed { get; }
        public int TriangleCount => _indices.Length / 3;

        public static bool TryCreate(GeometryDefinition geometry, out MeshContainment containment)
        {
            containment = null!;
            if (geometry == null || geometry.Type != GeometryType.Mesh)
                return false;

            var points = geometry.Points;
            if (points == null || points.Count < 3)
                return false;

            var indices = geometry.TriangleIndices;
            int[] indexArray;
            if (indices == null || indices.Count == 0)
            {
                if ((points.Count % 3) != 0)
                    return false;
                indexArray = new int[points.Count];
                for (var i = 0; i < points.Count; i++)
                    indexArray[i] = i;
            }
            else
            {
                if ((indices.Count % 3) != 0)
                    return false;
                indexArray = new int[indices.Count];
                for (var i = 0; i < indices.Count; i++)
                    indexArray[i] = indices[i];
            }

            for (var i = 0; i < indexArray.Length; i++)
            {
                var index = indexArray[i];
                if (index < 0 || index >= points.Count)
                    return false;
            }

            var vertices = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
                vertices[i] = points[i];

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (var i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                if (v.X < min.X) min.X = v.X;
                if (v.Y < min.Y) min.Y = v.Y;
                if (v.Z < min.Z) min.Z = v.Z;
                if (v.X > max.X) max.X = v.X;
                if (v.Y > max.Y) max.Y = v.Y;
                if (v.Z > max.Z) max.Z = v.Z;
            }

            var isClosed = IsMeshClosed(indexArray);
            containment = new MeshContainment(vertices, indexArray, min, max, isClosed);
            return true;
        }

        public bool Contains(Vector3 point, float surfaceEpsilon = DefaultSurfaceEpsilon)
        {
            var epsilon = Math.Max(0f, surfaceEpsilon);
            if (!IsWithinBounds(point, epsilon))
                return false;

            var epsilonSq = epsilon * epsilon;
            double total = 0.0;
            for (var i = 0; i < _indices.Length; i += 3)
            {
                var a = _vertices[_indices[i]];
                var b = _vertices[_indices[i + 1]];
                var c = _vertices[_indices[i + 2]];

                if (epsilon > 0f)
                {
                    var distSq = DistanceSquaredPointTriangle(point, a, b, c);
                    if (distSq <= epsilonSq)
                        return true;
                }

                if (TrySolidAngle(point, a, b, c, out var angle))
                    total += angle;
            }

            var winding = total / (4.0 * Math.PI);
            return Math.Abs(winding) > 0.5;
        }

        private bool IsWithinBounds(Vector3 point, float epsilon)
        {
            return point.X >= _min.X - epsilon &&
                   point.X <= _max.X + epsilon &&
                   point.Y >= _min.Y - epsilon &&
                   point.Y <= _max.Y + epsilon &&
                   point.Z >= _min.Z - epsilon &&
                   point.Z <= _max.Z + epsilon;
        }

        private static bool IsMeshClosed(int[] indices)
        {
            if (indices.Length < 3)
                return false;

            var edgeCounts = new Dictionary<ulong, int>();
            var degenerate = false;

            for (var i = 0; i < indices.Length; i += 3)
            {
                var a = indices[i];
                var b = indices[i + 1];
                var c = indices[i + 2];
                if (a == b || b == c || a == c)
                {
                    degenerate = true;
                    continue;
                }

                AddEdge(edgeCounts, a, b);
                AddEdge(edgeCounts, b, c);
                AddEdge(edgeCounts, c, a);
            }

            if (degenerate || edgeCounts.Count == 0)
                return false;

            foreach (var pair in edgeCounts)
            {
                if (pair.Value != 2)
                    return false;
            }

            return true;
        }

        private static void AddEdge(Dictionary<ulong, int> edgeCounts, int a, int b)
        {
            var min = a < b ? a : b;
            var max = a < b ? b : a;
            var key = ((ulong)(uint)min << 32) | (uint)max;
            if (edgeCounts.TryGetValue(key, out var count))
                edgeCounts[key] = count + 1;
            else
                edgeCounts[key] = 1;
        }

        private static bool TrySolidAngle(Vector3 point, Vector3 a, Vector3 b, Vector3 c, out double angle)
        {
            angle = 0.0;
            var va = a - point;
            var vb = b - point;
            var vc = c - point;

            var la = Length(va);
            var lb = Length(vb);
            var lc = Length(vc);
            if (la <= double.Epsilon || lb <= double.Epsilon || lc <= double.Epsilon)
                return false;

            var det = Dot(va, Cross(vb, vc));
            var denom = la * lb * lc +
                        Dot(va, vb) * lc +
                        Dot(vb, vc) * la +
                        Dot(vc, va) * lb;

            if (Math.Abs(det) <= double.Epsilon && Math.Abs(denom) <= double.Epsilon)
                return false;

            angle = 2.0 * Math.Atan2(det, denom);
            return true;
        }

        private static double Length(Vector3 v)
        {
            return Math.Sqrt((v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z));
        }

        private static double Dot(Vector3 a, Vector3 b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }

        private static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        private static float DistanceSquaredPointTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;
            var d1 = Vector3.Dot(ab, ap);
            var d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
                return Vector3.DistanceSquared(p, a);

            var bp = p - b;
            var d3 = Vector3.Dot(ab, bp);
            var d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
                return Vector3.DistanceSquared(p, b);

            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                var v = d1 / (d1 - d3);
                var proj = a + (v * ab);
                return Vector3.DistanceSquared(p, proj);
            }

            var cp = p - c;
            var d5 = Vector3.Dot(ab, cp);
            var d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
                return Vector3.DistanceSquared(p, c);

            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                var w = d2 / (d2 - d6);
                var proj = a + (w * ac);
                return Vector3.DistanceSquared(p, proj);
            }

            var va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                var w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                var proj = b + (w * (c - b));
                return Vector3.DistanceSquared(p, proj);
            }

            var denom = 1f / (va + vb + vc);
            var v2 = vb * denom;
            var w2 = vc * denom;
            var pointOnPlane = a + (ab * v2) + (ac * w2);
            return Vector3.DistanceSquared(p, pointOnPlane);
        }
    }
}
