using System;
using System.Collections.Generic;
using TopSpeed.Input.Devices.Controller;
using TopSpeed.Input.Devices.Vibration;

namespace TopSpeed.Input.Backends.Disabled
{
    internal sealed class Controller : IControllerBackend
    {
        private bool _enabled;

        public Controller(string reason)
        {
        }

        public event Action? NoControllerDetected
        {
            add { }
            remove { }
        }

        public bool ActiveControllerIsRacingWheel => false;
        public bool IgnoreAxesForMenuNavigation => false;
        public IVibrationDevice? VibrationDevice => null;

        public bool TryGetDisplayProfile(out ControllerDisplayProfile profile)
        {
            profile = default;
            return false;
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public void Update()
        {
        }

        public bool TryGetState(out State state)
        {
            state = default;
            return false;
        }

        public bool TryPollState(out State state)
        {
            state = default;
            return false;
        }

        public bool IsAnyButtonHeld() => false;

        public bool TryGetPendingChoices(out IReadOnlyList<Choice> choices)
        {
            choices = Array.Empty<Choice>();
            return false;
        }

        public bool TrySelect(Guid instanceGuid) => false;

        public void Suspend()
        {
        }

        public void Resume()
        {
        }

        public void Dispose()
        {
        }
    }
}
