using System;
using System.Collections.Generic;
using System.Numerics;

namespace TopSpeed.Tracks.Surfaces
{
    internal sealed class SurfaceProfileEvaluator
    {
        private readonly TrackProfileType _type;
        private readonly float _start;
        private readonly float _end;
        private readonly float _offset;
        private readonly float _scale;
        private readonly float _originX;
        private readonly float _originZ;
        private readonly float _slopeX;
        private readonly float _slopeZ;
        private readonly float _tension;
        private readonly SurfaceCurve? _curve;
        private readonly SurfaceGrid? _grid;

        private SurfaceProfileEvaluator(
            TrackProfileType type,
            float start,
            float end,
            float offset,
            float scale,
            float originX,
            float originZ,
            float slopeX,
            float slopeZ,
            float tension,
            SurfaceCurve? curve,
            SurfaceGrid? grid,
            bool isAbsolute)
        {
            _type = type;
            _start = start;
            _end = end;
            _offset = offset;
            _scale = scale;
            _originX = originX;
            _originZ = originZ;
            _slopeX = slopeX;
            _slopeZ = slopeZ;
            _tension = tension;
            _curve = curve;
            _grid = grid;
            IsAbsolute = isAbsolute;
        }

        public bool IsAbsolute { get; }

        public static SurfaceProfileEvaluator Create(
            TrackProfileDefinition? definition,
            float defaultCellSize)
        {
            if (definition == null)
                return new SurfaceProfileEvaluator(TrackProfileType.Flat, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0.5f, null, null, false);

            var meta = definition.Parameters;

            var offset = SurfaceParameterParser.TryGetFloat(meta, out var offsetValue, "offset", "height_offset", "base_offset")
                ? offsetValue
                : 0f;

            var scale = SurfaceParameterParser.TryGetFloat(meta, out var scaleValue, "scale", "mult", "multiplier")
                ? scaleValue
                : 1f;

            var isAbsolute = SurfaceParameterParser.TryGetBool(meta, out var absValue, "absolute", "absolute_height", "absolute_values")
                ? absValue
                : false;

            switch (definition.Type)
            {
                case TrackProfileType.Plane:
                    return CreatePlaneProfile(meta, offset, scale, isAbsolute);
                case TrackProfileType.Grid:
                    return CreateGridProfile(meta, offset, scale, defaultCellSize, isAbsolute);
                case TrackProfileType.LinearAlongPath:
                case TrackProfileType.SplineAlongPath:
                case TrackProfileType.BezierAlongPath:
                    return CreateCurveProfile(definition.Type, meta, offset, scale, isAbsolute);
                case TrackProfileType.Flat:
                default:
                    var height = SurfaceParameterParser.TryGetFloat(meta, out var heightValue, "height", "value")
                        ? heightValue
                        : 0f;
                    return new SurfaceProfileEvaluator(TrackProfileType.Flat, height, height, offset, scale, 0f, 0f, 0f, 0f, 0.5f, null, null, isAbsolute);
            }
        }

        public float EvaluateAlong(float distance, float totalLength)
        {
            var value = 0f;
            switch (_type)
            {
                case TrackProfileType.LinearAlongPath:
                    value = EvaluateLinear(distance, totalLength);
                    break;
                case TrackProfileType.SplineAlongPath:
                    value = _curve?.EvaluateSpline(distance, _tension) ?? EvaluateLinear(distance, totalLength);
                    break;
                case TrackProfileType.BezierAlongPath:
                    value = _curve?.EvaluateBezier(distance) ?? EvaluateLinear(distance, totalLength);
                    break;
                case TrackProfileType.Flat:
                    value = _start;
                    break;
                default:
                    value = 0f;
                    break;
            }

            return (_offset + value) * _scale;
        }

        public float EvaluateAt(Vector2 position)
        {
            var value = 0f;
            switch (_type)
            {
                case TrackProfileType.Plane:
                    value = _start + (_slopeX * (position.X - _originX)) + (_slopeZ * (position.Y - _originZ));
                    break;
                case TrackProfileType.Grid:
                    if (_grid != null && _grid.TrySample(position.X, position.Y, out var gridValue))
                        value = gridValue;
                    else
                        value = _start;
                    break;
                case TrackProfileType.Flat:
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

        private static SurfaceProfileEvaluator CreatePlaneProfile(
            IReadOnlyDictionary<string, string> meta,
            float offset,
            float scale,
            bool isAbsolute)
        {
            var originX = SurfaceParameterParser.TryGetFloat(meta, out var originXValue, "origin_x", "x", "start_x")
                ? originXValue
                : 0f;
            var originZ = SurfaceParameterParser.TryGetFloat(meta, out var originZValue, "origin_z", "z", "start_z")
                ? originZValue
                : 0f;
            var baseHeight = SurfaceParameterParser.TryGetFloat(meta, out var heightValue, "height", "value", "base_height")
                ? heightValue
                : 0f;

            var slopeX = SurfaceParameterParser.TryGetFloat(meta, out var slopeXValue, "slope_x", "dx", "grade_x")
                ? slopeXValue
                : 0f;
            var slopeZ = SurfaceParameterParser.TryGetFloat(meta, out var slopeZValue, "slope_z", "dz", "grade_z")
                ? slopeZValue
                : 0f;

            if (SurfaceParameterParser.TryGetFloat(meta, out var rollDeg, "roll", "roll_deg"))
                slopeX = (float)Math.Tan(rollDeg * Math.PI / 180f);
            if (SurfaceParameterParser.TryGetFloat(meta, out var pitchDeg, "pitch", "pitch_deg"))
                slopeZ = (float)Math.Tan(pitchDeg * Math.PI / 180f);

            return new SurfaceProfileEvaluator(TrackProfileType.Plane, baseHeight, baseHeight, offset, scale, originX, originZ, slopeX, slopeZ, 0.5f, null, null, isAbsolute);
        }

        private static SurfaceProfileEvaluator CreateGridProfile(
            IReadOnlyDictionary<string, string> meta,
            float offset,
            float scale,
            float defaultCellSize,
            bool isAbsolute)
        {
            var rows = SurfaceParameterParser.TryGetFloat(meta, out var rowsValue, "rows", "grid_rows") ? (int)Math.Max(1, rowsValue) : 0;
            var cols = SurfaceParameterParser.TryGetFloat(meta, out var colsValue, "cols", "columns", "grid_cols") ? (int)Math.Max(1, colsValue) : 0;
            var cell = SurfaceParameterParser.TryGetFloat(meta, out var cellValue, "cell", "cell_size", "grid_cell", "resolution")
                ? Math.Max(0.1f, cellValue)
                : Math.Max(0.1f, defaultCellSize);
            var originX = SurfaceParameterParser.TryGetFloat(meta, out var originXValue, "origin_x", "x", "start_x")
                ? originXValue
                : 0f;
            var originZ = SurfaceParameterParser.TryGetFloat(meta, out var originZValue, "origin_z", "z", "start_z")
                ? originZValue
                : 0f;

            var values = new List<float>();
            if (!SurfaceParameterParser.TryGetValue(meta, out var raw, "values", "grid", "grid_values", "heights", "heightmap") ||
                !SurfaceParameterParser.TryParseFloatList(raw, values) ||
                rows <= 0 || cols <= 0)
            {
                return new SurfaceProfileEvaluator(TrackProfileType.Grid, 0f, 0f, offset, scale, originX, originZ, 0f, 0f, 0.5f, null, null, isAbsolute);
            }

            var expected = rows * cols;
            if (values.Count < expected)
            {
                while (values.Count < expected)
                    values.Add(0f);
            }
            else if (values.Count > expected)
            {
                values.RemoveRange(expected, values.Count - expected);
            }

            var grid = new SurfaceGrid(rows, cols, cell, originX, originZ, values.ToArray());
            return new SurfaceProfileEvaluator(TrackProfileType.Grid, 0f, 0f, offset, scale, originX, originZ, 0f, 0f, 0.5f, null, grid, isAbsolute);
        }

        private static SurfaceProfileEvaluator CreateCurveProfile(
            TrackProfileType type,
            IReadOnlyDictionary<string, string> meta,
            float offset,
            float scale,
            bool isAbsolute)
        {
            var start = SurfaceParameterParser.TryGetFloat(meta, out var startValue, "start_height", "start", "height_start", "value_start")
                ? startValue
                : 0f;
            var end = SurfaceParameterParser.TryGetFloat(meta, out var endValue, "end_height", "end", "height_end", "value_end")
                ? endValue
                : start;

            SurfaceCurve? curve = null;
            if (SurfaceParameterParser.TryGetCurvePoints(meta, out var points, "points", "curve", "keys", "values"))
                curve = new SurfaceCurve(points);

            var tension = SurfaceParameterParser.TryGetFloat(meta, out var tensionValue, "tension", "smooth", "smoothness")
                ? SurfaceMath.Clamp(tensionValue, 0f, 1f)
                : 0.5f;

            return new SurfaceProfileEvaluator(type, start, end, offset, scale, 0f, 0f, 0f, 0f, tension, curve, null, isAbsolute);
        }
    }
}
