using System;
using System.Numerics;

namespace TopSpeed.Tracks.Surfaces
{
    internal readonly struct TrackSurfaceTriangle
    {
        private readonly Vector2 _a2;
        private readonly Vector2 _b2;
        private readonly Vector2 _c2;
        private readonly float _planeA;
        private readonly float _planeB;
        private readonly float _planeC;
        private readonly float _planeD;

        public TrackSurfaceTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 tangent)
        {
            var ab = b - a;
            var ac = c - a;
            var normal = Vector3.Cross(ab, ac);
            if (normal.LengthSquared() <= 0.000001f)
            {
                Normal = Vector3.UnitY;
                A = a;
                B = b;
                C = c;
            }
            else
            {
                normal = Vector3.Normalize(normal);
                if (normal.Y < 0f)
                {
                    var temp = b;
                    b = c;
                    c = temp;
                    ab = b - a;
                    ac = c - a;
                    normal = Vector3.Cross(ab, ac);
                    if (normal.LengthSquared() > 0.000001f)
                        normal = Vector3.Normalize(normal);
                    else
                        normal = Vector3.UnitY;
                }

                Normal = normal;
                A = a;
                B = b;
                C = c;
            }

            Tangent = tangent.LengthSquared() > 0.000001f ? Vector3.Normalize(tangent) : Vector3.UnitZ;

            _a2 = new Vector2(A.X, A.Z);
            _b2 = new Vector2(B.X, B.Z);
            _c2 = new Vector2(C.X, C.Z);

            _planeA = Normal.X;
            _planeB = Normal.Y;
            _planeC = Normal.Z;
            _planeD = -(Normal.X * A.X + Normal.Y * A.Y + Normal.Z * A.Z);

            MinX = Math.Min(A.X, Math.Min(B.X, C.X));
            MaxX = Math.Max(A.X, Math.Max(B.X, C.X));
            MinZ = Math.Min(A.Z, Math.Min(B.Z, C.Z));
            MaxZ = Math.Max(A.Z, Math.Max(B.Z, C.Z));
        }

        public Vector3 A { get; }
        public Vector3 B { get; }
        public Vector3 C { get; }
        public Vector3 Normal { get; }
        public Vector3 Tangent { get; }
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public bool TrySample(float x, float z, out float y)
        {
            y = 0f;
            if (x < MinX || x > MaxX || z < MinZ || z > MaxZ)
                return false;

            var p = new Vector2(x, z);
            var v0 = _c2 - _a2;
            var v1 = _b2 - _a2;
            var v2 = p - _a2;
            var denom = (v0.X * v1.Y) - (v1.X * v0.Y);
            if (Math.Abs(denom) <= 0.0000001f)
                return false;

            var invDenom = 1f / denom;
            var u = ((v2.X * v1.Y) - (v1.X * v2.Y)) * invDenom;
            var v = ((v0.X * v2.Y) - (v2.X * v0.Y)) * invDenom;
            if (u < -0.0001f || v < -0.0001f || (u + v) > 1.0001f)
                return false;

            if (Math.Abs(_planeB) > 0.000001f)
            {
                y = (-_planeD - (_planeA * x) - (_planeC * z)) / _planeB;
                return true;
            }

            var w = 1f - u - v;
            y = (A.Y * w) + (B.Y * v) + (C.Y * u);
            return true;
        }
    }
}
