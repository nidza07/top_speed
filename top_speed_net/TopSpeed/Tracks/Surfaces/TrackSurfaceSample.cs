using System.Numerics;

namespace TopSpeed.Tracks.Surfaces
{
    internal readonly struct TrackSurfaceSample
    {
        public TrackSurfaceSample(
            string surfaceId,
            string? materialId,
            int layer,
            Vector3 position,
            Vector3 normal,
            Vector3 tangent)
        {
            SurfaceId = surfaceId;
            MaterialId = materialId;
            Layer = layer;
            Position = position;
            Normal = normal;
            Tangent = tangent;
        }

        public string SurfaceId { get; }
        public string? MaterialId { get; }
        public int Layer { get; }
        public Vector3 Position { get; }
        public Vector3 Normal { get; }
        public Vector3 Tangent { get; }
    }
}
