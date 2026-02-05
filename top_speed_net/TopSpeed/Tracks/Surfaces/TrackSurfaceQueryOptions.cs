namespace TopSpeed.Tracks.Surfaces
{
    internal sealed class TrackSurfaceQueryOptions
    {
        public int? MinLayer { get; set; }
        public int? MaxLayer { get; set; }
        public bool PreferHighestLayer { get; set; } = true;
        public bool PreferHighestHeight { get; set; } = true;
    }
}
