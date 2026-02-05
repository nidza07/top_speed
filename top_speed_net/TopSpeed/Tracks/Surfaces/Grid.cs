using System;

namespace TopSpeed.Tracks.Surfaces
{
    internal sealed class SurfaceGrid
    {
        private readonly int _rows;
        private readonly int _cols;
        private readonly float _cellSize;
        private readonly float _originX;
        private readonly float _originZ;
        private readonly float[] _values;

        public SurfaceGrid(int rows, int cols, float cellSize, float originX, float originZ, float[] values)
        {
            _rows = Math.Max(1, rows);
            _cols = Math.Max(1, cols);
            _cellSize = Math.Max(0.1f, cellSize);
            _originX = originX;
            _originZ = originZ;
            _values = values ?? Array.Empty<float>();
        }

        public bool TrySample(float x, float z, out float value)
        {
            value = 0f;
            if (_values.Length == 0)
                return false;

            var localX = (x - _originX) / _cellSize;
            var localZ = (z - _originZ) / _cellSize;

            var x0 = (int)Math.Floor(localX);
            var z0 = (int)Math.Floor(localZ);
            var x1 = x0 + 1;
            var z1 = z0 + 1;

            x0 = SurfaceMath.Clamp(x0, 0, _cols - 1);
            z0 = SurfaceMath.Clamp(z0, 0, _rows - 1);
            x1 = SurfaceMath.Clamp(x1, 0, _cols - 1);
            z1 = SurfaceMath.Clamp(z1, 0, _rows - 1);

            var tx = SurfaceMath.Clamp(localX - x0, 0f, 1f);
            var tz = SurfaceMath.Clamp(localZ - z0, 0f, 1f);

            var v00 = GetValue(z0, x0);
            var v10 = GetValue(z0, x1);
            var v01 = GetValue(z1, x0);
            var v11 = GetValue(z1, x1);

            var v0 = v00 + (v10 - v00) * tx;
            var v1 = v01 + (v11 - v01) * tx;
            value = v0 + (v1 - v0) * tz;
            return true;
        }

        private float GetValue(int row, int col)
        {
            var index = (row * _cols) + col;
            if ((uint)index >= (uint)_values.Length)
                return 0f;
            return _values[index];
        }
    }
}
