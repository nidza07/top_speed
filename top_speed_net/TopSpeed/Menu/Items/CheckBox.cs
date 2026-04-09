using System;
using TopSpeed.Localization;

namespace TopSpeed.Menu
{
    internal sealed class CheckBox : ToggleItem
    {
        public CheckBox(
            string text,
            Func<bool> getValue,
            Action<bool> setValue,
            Action<bool>? onChanged = null,
            MenuAction action = MenuAction.None,
            string? nextMenuId = null,
            Action? onActivate = null,
            bool suppressPostActivateAnnouncement = false,
            string? hint = null)
            : base(text, getValue, setValue, onChanged, action, nextMenuId, onActivate, suppressPostActivateAnnouncement, hint)
        {
        }

        public override string GetDisplayText()
        {
            return Describe(LocalizationService.Mark("check box"), FormatValue(GetValue()), separated: false);
        }

        public override string? ActivateAndGetAnnouncement()
        {
            return Toggle(FormatValue);
        }

        private static string FormatValue(bool value)
        {
            return value
                ? LocalizationService.Translate(LocalizationService.Mark("checked"))
                : LocalizationService.Translate(LocalizationService.Mark("not checked"));
        }
    }
}

