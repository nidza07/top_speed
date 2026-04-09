using TopSpeed.Localization;

namespace TopSpeed.Menu
{
    internal static class ItemText
    {
        public static string Compose(string baseText, string kind, string value, bool separated = true)
        {
            var typeLabel = LocalizationService.Translate(kind);
            return separated
                ? $"{baseText}; {typeLabel} {value}"
                : $"{baseText} {typeLabel} {value}";
        }
    }
}
