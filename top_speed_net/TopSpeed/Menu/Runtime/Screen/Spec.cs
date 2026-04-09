using System;
using TopSpeed.Speech;

namespace TopSpeed.Menu
{
    internal sealed class ScreenSpec
    {
        public static readonly ScreenSpec None = new ScreenSpec();
        public static readonly ScreenSpec Silent = new ScreenSpec(titleFlag: SpeechService.SpeakFlag.None);
        public static readonly ScreenSpec Back = new ScreenSpec(ScreenFlags.Back);
        public static readonly ScreenSpec BackSilent = new ScreenSpec(ScreenFlags.Back, SpeechService.SpeakFlag.None);
        public static readonly ScreenSpec Close = new ScreenSpec(ScreenFlags.Close);
        public static readonly ScreenSpec KeepSelection = new ScreenSpec(ScreenFlags.KeepSelection);
        public static readonly ScreenSpec KeepSelectionSilent = new ScreenSpec(ScreenFlags.KeepSelection, SpeechService.SpeakFlag.None);
        public static readonly ScreenSpec BackKeepSelection = new ScreenSpec(ScreenFlags.Back | ScreenFlags.KeepSelection);

        public ScreenSpec(
            ScreenFlags flags = ScreenFlags.None,
            SpeechService.SpeakFlag titleFlag = SpeechService.SpeakFlag.NoInterrupt,
            string? closeText = null,
            Func<CloseEvent, bool>? onClose = null)
        {
            Flags = flags;
            TitleFlag = titleFlag;
            CloseText = closeText;
            OnClose = onClose;
        }

        public ScreenFlags Flags { get; }
        public SpeechService.SpeakFlag TitleFlag { get; }
        public string? CloseText { get; }
        public Func<CloseEvent, bool>? OnClose { get; }
    }
}
