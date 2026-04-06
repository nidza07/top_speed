using TopSpeed.Input;
using TopSpeed.Localization;
using TopSpeed.Menu;

namespace TopSpeed.Game
{
    internal sealed partial class Game
    {
        private void HandleNoControllerDetected()
        {
            SetDevice(InputDeviceMode.Keyboard);

            var dialog = new Dialog(
                LocalizationService.Mark("No controllers found"),
                LocalizationService.Mark("No controllers were found. The game has reverted to keyboard input."),
                QuestionId.Ok,
                items: null,
                onResult: null,
                new DialogButton(QuestionId.Ok, LocalizationService.Mark("OK")))
            {
                FocusFirstButtonByDefault = false
            };

            _dialogs.Show(dialog);
        }

        private void HandleControllerBackendUnavailable(string reason)
        {
            SetDevice(InputDeviceMode.Keyboard);

            var dialog = new Dialog(
                LocalizationService.Mark("Controller backend unavailable"),
                LocalizationService.Format(
                    LocalizationService.Mark("The SDL controller backend could not be initialized. The game has reverted to keyboard input.\n\nReason: {0}"),
                    reason),
                QuestionId.Ok,
                items: null,
                onResult: null,
                new DialogButton(QuestionId.Ok, LocalizationService.Mark("OK")))
            {
                FocusFirstButtonByDefault = false
            };

            _dialogs.Show(dialog);
        }

        private void ShowRestoreDefaultsDialog()
        {
            var dialog = new Dialog(
                LocalizationService.Mark("Restore default settings"),
                LocalizationService.Mark("Are you sure you would like to restore all settings to their default values?"),
                QuestionId.Cancel,
                items: null,
                onResult: result =>
                {
                    if (result == QuestionId.Yes)
                        RestoreDefaults();
                },
                new DialogButton(QuestionId.Yes, LocalizationService.Mark("Yes")),
                new DialogButton(QuestionId.No, LocalizationService.Mark("No")))
            {
                FocusFirstButtonByDefault = false
            };

            _dialogs.Show(dialog);
        }

        private void ResetMappings(InputMappingMode mode)
        {
            var title = mode == InputMappingMode.Keyboard
                ? LocalizationService.Mark("Reset keyboard mappings")
                : LocalizationService.Mark("Reset controller mappings");
            var caption = mode == InputMappingMode.Keyboard
                ? LocalizationService.Mark("Are you sure you would like to restore all keyboard bindings to their default values?")
                : LocalizationService.Mark("Are you sure you would like to restore all controller bindings to their default values?");

            var dialog = new Dialog(
                title,
                caption,
                QuestionId.Cancel,
                items: null,
                onResult: result =>
                {
                    if (result == QuestionId.Yes)
                        ApplyResetMappings(mode);
                },
                new DialogButton(QuestionId.Yes, LocalizationService.Mark("Yes")),
                new DialogButton(QuestionId.No, LocalizationService.Mark("No")))
            {
                FocusFirstButtonByDefault = false
            };

            _dialogs.Show(dialog);
        }

        private void ApplyResetMappings(InputMappingMode mode)
        {
            var defaults = new RaceSettings();
            var defaultInput = new RaceInput(defaults);

            foreach (var action in _raceInput.KeyMap.Actions)
            {
                if (mode == InputMappingMode.Keyboard)
                {
                    _raceInput.KeyMap.ApplyKeyMapping(action.Action, defaultInput.KeyMap.GetKey(action.Action));
                }
                else
                {
                    _raceInput.KeyMap.ApplyAxisMapping(action.Action, defaultInput.KeyMap.GetAxis(action.Action));
                }
            }

            SaveSettings();
            _speech.Speak(
                mode == InputMappingMode.Keyboard
                    ? LocalizationService.Mark("Keyboard mappings restored to defaults.")
                    : LocalizationService.Mark("Controller mappings restored to defaults."));
        }
    }
}
