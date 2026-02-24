using System;
using SharpDX.DirectInput;

namespace TopSpeed.Menu
{
    internal sealed class MenuShortcut
    {
        public MenuShortcut(Key key, Action onTrigger)
        {
            Key = key;
            OnTrigger = onTrigger ?? throw new ArgumentNullException(nameof(onTrigger));
        }

        public Key Key { get; }
        public Action OnTrigger { get; }
    }
}
