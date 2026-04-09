namespace TopSpeed.Menu
{
    internal enum CloseKind
    {
        Back,
        Close
    }

    internal readonly struct CloseEvent
    {
        public CloseEvent(string menuId, string viewId, MenuCloseSource source, CloseKind kind)
        {
            MenuId = menuId ?? string.Empty;
            ViewId = viewId ?? string.Empty;
            Source = source;
            Kind = kind;
        }

        public string MenuId { get; }
        public string ViewId { get; }
        public MenuCloseSource Source { get; }
        public CloseKind Kind { get; }
    }
}
