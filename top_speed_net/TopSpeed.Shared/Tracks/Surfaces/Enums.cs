namespace TopSpeed.Tracks.Surfaces
{
    public enum TrackSurfaceType
    {
        Undefined = 0,
        Loft,
        Polygon,
        Mesh
    }

    public enum TrackProfileType
    {
        Undefined = 0,
        Flat,
        Plane,
        LinearAlongPath,
        SplineAlongPath,
        BezierAlongPath,
        Grid
    }

    public enum TrackBankType
    {
        Undefined = 0,
        Flat,
        LinearAlongPath,
        SplineAlongPath,
        BezierAlongPath
    }

    public enum TrackBankSide
    {
        Left = 0,
        Right = 1
    }
}
