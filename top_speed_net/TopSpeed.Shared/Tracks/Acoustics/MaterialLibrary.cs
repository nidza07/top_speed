using System;
using System.Collections.Generic;
using TopSpeed.Tracks.Walls;

namespace TopSpeed.Tracks.Materials
{
    public static class TrackMaterialLibrary
    {
        private struct MaterialValues
        {
            public float AbsLow;
            public float AbsMid;
            public float AbsHigh;
            public float Scatter;
            public float TransLow;
            public float TransMid;
            public float TransHigh;
            public TrackWallMaterial CollisionMaterial;
        }

        private static readonly Dictionary<string, MaterialValues> Presets =
            new Dictionary<string, MaterialValues>(StringComparer.OrdinalIgnoreCase)
            {
                ["concrete"] = new MaterialValues { AbsLow = 0.01f, AbsMid = 0.02f, AbsHigh = 0.04f, Scatter = 0.10f, TransLow = 0.00f, TransMid = 0.00f, TransHigh = 0.00f, CollisionMaterial = TrackWallMaterial.Concrete },
                ["asphalt"] = new MaterialValues { AbsLow = 0.02f, AbsMid = 0.04f, AbsHigh = 0.06f, Scatter = 0.20f, TransLow = 0.00f, TransMid = 0.00f, TransHigh = 0.00f, CollisionMaterial = TrackWallMaterial.Hard },
                ["brick"] = new MaterialValues { AbsLow = 0.03f, AbsMid = 0.05f, AbsHigh = 0.07f, Scatter = 0.10f, TransLow = 0.00f, TransMid = 0.00f, TransHigh = 0.00f, CollisionMaterial = TrackWallMaterial.Hard },
                ["metal"] = new MaterialValues { AbsLow = 0.01f, AbsMid = 0.01f, AbsHigh = 0.02f, Scatter = 0.05f, TransLow = 0.00f, TransMid = 0.00f, TransHigh = 0.00f, CollisionMaterial = TrackWallMaterial.Metal },
                ["wood"] = new MaterialValues { AbsLow = 0.10f, AbsMid = 0.15f, AbsHigh = 0.20f, Scatter = 0.20f, TransLow = 0.02f, TransMid = 0.02f, TransHigh = 0.02f, CollisionMaterial = TrackWallMaterial.Wood },
                ["glass"] = new MaterialValues { AbsLow = 0.02f, AbsMid = 0.03f, AbsHigh = 0.02f, Scatter = 0.05f, TransLow = 0.05f, TransMid = 0.05f, TransHigh = 0.05f, CollisionMaterial = TrackWallMaterial.Hard },
                ["plaster"] = new MaterialValues { AbsLow = 0.05f, AbsMid = 0.07f, AbsHigh = 0.10f, Scatter = 0.10f, TransLow = 0.00f, TransMid = 0.00f, TransHigh = 0.00f, CollisionMaterial = TrackWallMaterial.Hard },
                ["fabric"] = new MaterialValues { AbsLow = 0.20f, AbsMid = 0.35f, AbsHigh = 0.60f, Scatter = 0.30f, TransLow = 0.05f, TransMid = 0.05f, TransHigh = 0.05f, CollisionMaterial = TrackWallMaterial.Soft },
                ["grass"] = new MaterialValues { AbsLow = 0.20f, AbsMid = 0.30f, AbsHigh = 0.40f, Scatter = 0.25f, TransLow = 0.10f, TransMid = 0.10f, TransHigh = 0.10f, CollisionMaterial = TrackWallMaterial.Grass },
                ["dirt"] = new MaterialValues { AbsLow = 0.15f, AbsMid = 0.25f, AbsHigh = 0.35f, Scatter = 0.20f, TransLow = 0.10f, TransMid = 0.10f, TransHigh = 0.10f, CollisionMaterial = TrackWallMaterial.Dirt },
                ["sand"] = new MaterialValues { AbsLow = 0.20f, AbsMid = 0.30f, AbsHigh = 0.40f, Scatter = 0.30f, TransLow = 0.10f, TransMid = 0.10f, TransHigh = 0.10f, CollisionMaterial = TrackWallMaterial.Sand },
                ["gravel"] = new MaterialValues { AbsLow = 0.10f, AbsMid = 0.20f, AbsHigh = 0.35f, Scatter = 0.40f, TransLow = 0.10f, TransMid = 0.10f, TransHigh = 0.10f, CollisionMaterial = TrackWallMaterial.Hard },
                ["snow"] = new MaterialValues { AbsLow = 0.30f, AbsMid = 0.50f, AbsHigh = 0.70f, Scatter = 0.40f, TransLow = 0.20f, TransMid = 0.20f, TransHigh = 0.20f, CollisionMaterial = TrackWallMaterial.Soft },
                ["water"] = new MaterialValues { AbsLow = 0.05f, AbsMid = 0.05f, AbsHigh = 0.05f, Scatter = 0.10f, TransLow = 0.20f, TransMid = 0.20f, TransHigh = 0.20f, CollisionMaterial = TrackWallMaterial.Soft },
                ["rubber"] = new MaterialValues { AbsLow = 0.10f, AbsMid = 0.20f, AbsHigh = 0.40f, Scatter = 0.20f, TransLow = 0.05f, TransMid = 0.05f, TransHigh = 0.05f, CollisionMaterial = TrackWallMaterial.Rubber }
            };

        public static bool IsPreset(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return Presets.ContainsKey(name.Trim());
        }

        public static bool TryGetPreset(string name, out TrackMaterialDefinition material)
        {
            material = null!;
            if (string.IsNullOrWhiteSpace(name))
                return false;
            if (!Presets.TryGetValue(name.Trim(), out var values))
                return false;

            var id = name.Trim();
            material = new TrackMaterialDefinition(
                id,
                id,
                values.AbsLow,
                values.AbsMid,
                values.AbsHigh,
                values.Scatter,
                values.TransLow,
                values.TransMid,
                values.TransHigh,
                values.CollisionMaterial);
            return true;
        }
    }
}
