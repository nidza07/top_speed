using System;
using System.Collections.Generic;

namespace TopSpeed.Tracks.Surfaces
{
    internal readonly struct SurfaceCurvePoint
    {
        public SurfaceCurvePoint(float distance, float value)
        {
            Distance = distance;
            Value = value;
        }

        public float Distance { get; }
        public float Value { get; }
    }

    internal sealed class SurfaceCurve
    {
        private readonly SurfaceCurvePoint[] _points;
        private readonly SurfaceBezierSegment[] _bezierSegments;

        public SurfaceCurve(IReadOnlyList<SurfaceCurvePoint> points)
        {
            if (points == null || points.Count == 0)
            {
                _points = Array.Empty<SurfaceCurvePoint>();
                _bezierSegments = Array.Empty<SurfaceBezierSegment>();
                return;
            }

            _points = new SurfaceCurvePoint[points.Count];
            for (var i = 0; i < points.Count; i++)
                _points[i] = points[i];
            Array.Sort(_points, (a, b) => a.Distance.CompareTo(b.Distance));
            _bezierSegments = BuildBezierSegments(_points);
        }

        public bool HasPoints => _points.Length > 0;

        public float MinDistance => _points.Length == 0 ? 0f : _points[0].Distance;
        public float MaxDistance => _points.Length == 0 ? 0f : _points[_points.Length - 1].Distance;

        public float EvaluateLinear(float distance)
        {
            if (_points.Length == 0)
                return 0f;

            if (distance <= _points[0].Distance)
                return _points[0].Value;
            if (distance >= _points[_points.Length - 1].Distance)
                return _points[_points.Length - 1].Value;

            var index = FindSegment(distance);
            var a = _points[index];
            var b = _points[index + 1];
            var span = b.Distance - a.Distance;
            if (span <= 0.000001f)
                return b.Value;
            var t = (distance - a.Distance) / span;
            return a.Value + (b.Value - a.Value) * t;
        }

        public float EvaluateSpline(float distance, float tension)
        {
            if (_points.Length == 0)
                return 0f;
            if (_points.Length == 1)
                return _points[0].Value;
            if (distance <= _points[0].Distance)
                return _points[0].Value;
            if (distance >= _points[_points.Length - 1].Distance)
                return _points[_points.Length - 1].Value;

            var index = FindSegment(distance);
            var p0 = _points[Math.Max(0, index - 1)];
            var p1 = _points[index];
            var p2 = _points[index + 1];
            var p3 = _points[Math.Min(_points.Length - 1, index + 2)];

            var span = p2.Distance - p1.Distance;
            if (span <= 0.000001f)
                return p2.Value;

            var t = (distance - p1.Distance) / span;
            return CatmullRom(p0.Value, p1.Value, p2.Value, p3.Value, t, tension);
        }

        public float EvaluateBezier(float distance)
        {
            if (_bezierSegments.Length == 0)
                return EvaluateLinear(distance);

            if (distance <= _bezierSegments[0].StartDistance)
                return _bezierSegments[0].P0.Value;
            if (distance >= _bezierSegments[_bezierSegments.Length - 1].EndDistance)
                return _bezierSegments[_bezierSegments.Length - 1].P3.Value;

            for (var i = 0; i < _bezierSegments.Length; i++)
            {
                var seg = _bezierSegments[i];
                if (distance < seg.StartDistance || distance > seg.EndDistance)
                    continue;
                var span = seg.EndDistance - seg.StartDistance;
                if (span <= 0.000001f)
                    return seg.P3.Value;
                var t = (distance - seg.StartDistance) / span;
                return CubicBezier(seg.P0.Value, seg.P1.Value, seg.P2.Value, seg.P3.Value, t);
            }

            return EvaluateLinear(distance);
        }

        private int FindSegment(float distance)
        {
            for (var i = 0; i < _points.Length - 1; i++)
            {
                if (distance <= _points[i + 1].Distance)
                    return i;
            }
            return _points.Length - 2;
        }

        private static float CatmullRom(float p0, float p1, float p2, float p3, float t, float tension)
        {
            var t2 = t * t;
            var t3 = t2 * t;

            var s = SurfaceMath.Clamp(1f - tension, 0f, 1f);
            var m1 = s * (p2 - p0) * 0.5f;
            var m2 = s * (p3 - p1) * 0.5f;

            var h00 = (2f * t3) - (3f * t2) + 1f;
            var h10 = t3 - (2f * t2) + t;
            var h01 = (-2f * t3) + (3f * t2);
            var h11 = t3 - t2;

            return (h00 * p1) + (h10 * m1) + (h01 * p2) + (h11 * m2);
        }

        private static float CubicBezier(float p0, float p1, float p2, float p3, float t)
        {
            var u = 1f - t;
            var tt = t * t;
            var uu = u * u;
            var uuu = uu * u;
            var ttt = tt * t;
            return (uuu * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (ttt * p3);
        }

        private static SurfaceBezierSegment[] BuildBezierSegments(SurfaceCurvePoint[] points)
        {
            if (points.Length < 4)
                return Array.Empty<SurfaceBezierSegment>();

            var segments = new List<SurfaceBezierSegment>();
            for (var i = 0; i + 3 < points.Length; i += 3)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var p2 = points[i + 2];
                var p3 = points[i + 3];
                var start = Math.Min(p0.Distance, p3.Distance);
                var end = Math.Max(p0.Distance, p3.Distance);
                segments.Add(new SurfaceBezierSegment(p0, p1, p2, p3, start, end));
            }

            return segments.ToArray();
        }

        private readonly struct SurfaceBezierSegment
        {
            public SurfaceBezierSegment(
                SurfaceCurvePoint p0,
                SurfaceCurvePoint p1,
                SurfaceCurvePoint p2,
                SurfaceCurvePoint p3,
                float startDistance,
                float endDistance)
            {
                P0 = p0;
                P1 = p1;
                P2 = p2;
                P3 = p3;
                StartDistance = startDistance;
                EndDistance = endDistance;
            }

            public SurfaceCurvePoint P0 { get; }
            public SurfaceCurvePoint P1 { get; }
            public SurfaceCurvePoint P2 { get; }
            public SurfaceCurvePoint P3 { get; }
            public float StartDistance { get; }
            public float EndDistance { get; }
        }
    }
}
