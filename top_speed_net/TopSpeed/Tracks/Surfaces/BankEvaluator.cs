using System;
using System.Collections.Generic;

namespace TopSpeed.Tracks.Surfaces
{
    internal sealed class SurfaceBankEvaluator
    {
        private readonly TrackBankType _type;
        private readonly float _start;
        private readonly float _end;
        private readonly float _offset;
        private readonly float _scale;
        private readonly float _tension;
        private readonly SurfaceCurve? _curve;

        private SurfaceBankEvaluator(
            TrackBankType type,
            TrackBankSide side,
            float start,
            float end,
            float offset,
            float scale,
            float tension,
            SurfaceCurve? curve)
        {
            _type = type;
            Side = side;
            _start = start;
            _end = end;
            _offset = offset;
            _scale = scale;
            _tension = tension;
            _curve = curve;
        }

        public TrackBankSide Side { get; }

        public static SurfaceBankEvaluator Create(TrackBankDefinition? definition)
        {
            if (definition == null)
                return new SurfaceBankEvaluator(TrackBankType.Flat, TrackBankSide.Right, 0f, 0f, 0f, 1f, 0.5f, null);

            var meta = definition.Parameters;
            var offset = SurfaceParameterParser.TryGetFloat(meta, out var offsetValue, "offset", "angle_offset", "bank_offset")
                ? offsetValue
                : 0f;
            var scale = SurfaceParameterParser.TryGetFloat(meta, out var scaleValue, "scale", "mult", "multiplier")
                ? scaleValue
                : 1f;

            switch (definition.Type)
            {
                case TrackBankType.LinearAlongPath:
                case TrackBankType.SplineAlongPath:
                case TrackBankType.BezierAlongPath:
                    return CreateCurveBank(definition.Type, definition.Side, meta, offset, scale);
                case TrackBankType.Flat:
                default:
                    var angle = SurfaceParameterParser.TryGetFloat(meta, out var angleValue, "angle", "degrees", "deg", "bank")
                        ? angleValue
                        : 0f;
                    return new SurfaceBankEvaluator(TrackBankType.Flat, definition.Side, angle, angle, offset, scale, 0.5f, null);
            }
        }

        public float EvaluateDegrees(float distance, float totalLength)
        {
            var value = 0f;
            switch (_type)
            {
                case TrackBankType.LinearAlongPath:
                    value = EvaluateLinear(distance, totalLength);
                    break;
                case TrackBankType.SplineAlongPath:
                    value = _curve?.EvaluateSpline(distance, _tension) ?? EvaluateLinear(distance, totalLength);
                    break;
                case TrackBankType.BezierAlongPath:
                    value = _curve?.EvaluateBezier(distance) ?? EvaluateLinear(distance, totalLength);
                    break;
                case TrackBankType.Flat:
                    value = _start;
                    break;
                default:
                    value = 0f;
                    break;
            }

            return (_offset + value) * _scale;
        }

        private float EvaluateLinear(float distance, float totalLength)
        {
            if (_curve != null && _curve.HasPoints)
                return _curve.EvaluateLinear(distance);

            var span = Math.Max(0.0001f, totalLength);
            var t = SurfaceMath.Clamp(distance / span, 0f, 1f);
            return _start + ((_end - _start) * t);
        }

        private static SurfaceBankEvaluator CreateCurveBank(
            TrackBankType type,
            TrackBankSide side,
            IReadOnlyDictionary<string, string> meta,
            float offset,
            float scale)
        {
            var start = SurfaceParameterParser.TryGetFloat(meta, out var startValue, "start_deg", "start_angle", "start", "angle_start")
                ? startValue
                : 0f;
            var end = SurfaceParameterParser.TryGetFloat(meta, out var endValue, "end_deg", "end_angle", "end", "angle_end")
                ? endValue
                : start;

            SurfaceCurve? curve = null;
            if (SurfaceParameterParser.TryGetCurvePoints(meta, out var points, "points", "curve", "keys", "values"))
                curve = new SurfaceCurve(points);

            var tension = SurfaceParameterParser.TryGetFloat(meta, out var tensionValue, "tension", "smooth", "smoothness")
                ? SurfaceMath.Clamp(tensionValue, 0f, 1f)
                : 0.5f;

            return new SurfaceBankEvaluator(type, side, start, end, offset, scale, tension, curve);
        }
    }
}
