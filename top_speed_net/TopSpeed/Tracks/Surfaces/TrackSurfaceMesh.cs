using System;
using System.Collections.Generic;
using System.Numerics;

namespace TopSpeed.Tracks.Surfaces
{
    internal sealed class TrackSurfaceMesh
    {
        private readonly TrackSurfaceTriangle[] _triangles;
        private readonly SurfaceSpatialGrid? _grid;
        private readonly SurfaceBounds2D _bounds;

        public TrackSurfaceMesh(
            string id,
            string? materialId,
            int layer,
            TrackSurfaceTriangle[] triangles,
            float? cellSize)
        {
            Id = id;
            MaterialId = materialId;
            Layer = layer;
            _triangles = triangles ?? Array.Empty<TrackSurfaceTriangle>();

            if (_triangles.Length == 0)
            {
                _bounds = new SurfaceBounds2D(0f, 0f, 0f, 0f);
                _grid = null;
                return;
            }

            var minX = float.MaxValue;
            var minZ = float.MaxValue;
            var maxX = float.MinValue;
            var maxZ = float.MinValue;
            for (var i = 0; i < _triangles.Length; i++)
            {
                var tri = _triangles[i];
                if (tri.MinX < minX) minX = tri.MinX;
                if (tri.MinZ < minZ) minZ = tri.MinZ;
                if (tri.MaxX > maxX) maxX = tri.MaxX;
                if (tri.MaxZ > maxZ) maxZ = tri.MaxZ;
            }
            _bounds = new SurfaceBounds2D(minX, minZ, maxX, maxZ);

            if (cellSize.HasValue && cellSize.Value > 0.05f)
            {
                _grid = new SurfaceSpatialGrid(cellSize.Value);
                for (var i = 0; i < _triangles.Length; i++)
                    _grid.AddTriangle(i, _triangles[i]);
            }
        }

        public string Id { get; }
        public string? MaterialId { get; }
        public int Layer { get; }
        public SurfaceBounds2D Bounds => _bounds;
        public IReadOnlyList<TrackSurfaceTriangle> Triangles => _triangles;

        public bool TrySample(float x, float z, out TrackSurfaceSample sample)
        {
            sample = default;
            if (_triangles.Length == 0)
                return false;
            if (!_bounds.Contains(x, z))
                return false;

            List<int>? indices = null;
            if (_grid != null && !_grid.TryGetTriangles(x, z, out indices))
                return false;

            var bestY = float.MinValue;
            var found = false;
            TrackSurfaceTriangle bestTri = default;

            if (indices != null)
            {
                foreach (var index in indices)
                {
                    if ((uint)index >= (uint)_triangles.Length)
                        continue;
                    var tri = _triangles[index];
                    if (!tri.TrySample(x, z, out var y))
                        continue;
                    if (!found || y > bestY)
                    {
                        found = true;
                        bestY = y;
                        bestTri = tri;
                    }
                }
            }
            else
            {
                for (var i = 0; i < _triangles.Length; i++)
                {
                    var tri = _triangles[i];
                    if (!tri.TrySample(x, z, out var y))
                        continue;
                    if (!found || y > bestY)
                    {
                        found = true;
                        bestY = y;
                        bestTri = tri;
                    }
                }
            }

            if (!found)
                return false;

            var position = new Vector3(x, bestY, z);
            sample = new TrackSurfaceSample(Id, MaterialId, Layer, position, bestTri.Normal, bestTri.Tangent);
            return true;
        }
    }
}
