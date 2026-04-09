using System;

namespace TopSpeed.Menu
{
    [Flags]
    internal enum ScreenFlags
    {
        None = 0,
        Back = 1,
        Close = 2,
        KeepSelection = 4
    }
}
