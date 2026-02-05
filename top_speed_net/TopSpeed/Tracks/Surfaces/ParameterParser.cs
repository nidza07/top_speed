using System;
using System.Collections.Generic;
using System.Globalization;

namespace TopSpeed.Tracks.Surfaces
{
    internal static class SurfaceParameterParser
    {
        private static readonly char[] ValueSeparators = { ',', ';', ':', '|', '\n', '\r', '\t', ' ' };

        public static bool TryGetValue(IReadOnlyDictionary<string, string> metadata, out string value, params string[] keys)
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

        public static bool TryGetFloat(IReadOnlyDictionary<string, string> metadata, out float value, params string[] keys)
        {
            value = 0f;
            if (!TryGetValue(metadata, out var raw, keys))
                return false;
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryGetBool(IReadOnlyDictionary<string, string> metadata, out bool value, params string[] keys)
        {
            value = false;
            if (!TryGetValue(metadata, out var raw, keys))
                return false;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "y":
                case "1":
                case "on":
                    value = true;
                    return true;
                case "false":
                case "no":
                case "n":
                case "0":
                case "off":
                    value = false;
                    return true;
                default:
                    return bool.TryParse(raw, out value);
            }
        }

        public static bool TryGetCurvePoints(IReadOnlyDictionary<string, string> metadata, out List<SurfaceCurvePoint> points, params string[] keys)
        {
            points = new List<SurfaceCurvePoint>();
            if (!TryGetValue(metadata, out var raw, keys))
                return false;
            return TryParseCurvePoints(raw, points);
        }

        public static bool TryParseCurvePoints(string raw, List<SurfaceCurvePoint> points)
        {
            points.Clear();
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var values = new List<float>();
            var tokens = raw.Split(ValueSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (float.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    values.Add(value);
            }

            if (values.Count < 2)
                return false;

            for (var i = 0; i + 1 < values.Count; i += 2)
                points.Add(new SurfaceCurvePoint(values[i], values[i + 1]));

            return points.Count > 0;
        }

        public static bool TryParseFloatList(string raw, List<float> values)
        {
            values.Clear();
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var tokens = raw.Split(ValueSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (float.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    values.Add(value);
            }

            return values.Count > 0;
        }
    }
}
