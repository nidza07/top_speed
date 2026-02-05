using System;
using System.Collections.Generic;

namespace TopSpeed.Tracks.Surfaces
{
    internal sealed class SurfaceCellIndex
    {
        private readonly float _cellSize;
        private readonly Dictionary<long, List<int>> _cells;

        public SurfaceCellIndex(float cellSize)
        {
            _cellSize = Math.Max(0.1f, cellSize);
            _cells = new Dictionary<long, List<int>>();
        }

        public void AddSurface(int index, SurfaceBounds2D bounds)
        {
            var minCellX = ToCell(bounds.MinX);
            var maxCellX = ToCell(bounds.MaxX);
            var minCellZ = ToCell(bounds.MinZ);
            var maxCellZ = ToCell(bounds.MaxZ);

            for (var z = minCellZ; z <= maxCellZ; z++)
            {
                for (var x = minCellX; x <= maxCellX; x++)
                {
                    var key = PackCellKey(x, z);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        _cells[key] = list;
                    }
                    list.Add(index);
                }
            }
        }

        public bool TryGetSurfaces(float x, float z, out List<int> indices)
        {
            var cellX = ToCell(x);
            var cellZ = ToCell(z);
            return _cells.TryGetValue(PackCellKey(cellX, cellZ), out indices!);
        }

        private int ToCell(float value)
        {
            return (int)Math.Floor(value / _cellSize);
        }

        private static long PackCellKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }
    }
}
