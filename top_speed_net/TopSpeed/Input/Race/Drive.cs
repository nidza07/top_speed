using SharpDX.DirectInput;
using System;

namespace TopSpeed.Input
{
    internal sealed partial class RaceInput
    {
        public int GetSteering()
        {
            if (!_allowDrivingInput || _overlayInputBlocked)
                return 0;

            var joystickSteer = 0;
            if (UseJoystick)
            {
                var left = GetAxis(_left);
                var right = GetAxis(_right);
                joystickSteer = left != 0 ? -left : right;
            }

            if (!UseKeyboard)
                return joystickSteer;

            var keyboardSteer = _settings.KeyboardProgressiveRate == KeyboardProgressiveRate.Off
                ? (_lastState.IsDown(_kbLeft) ? -100 : (_lastState.IsDown(_kbRight) ? 100 : 0))
                : (int)(_simSteer * 100f);

            return Math.Abs(keyboardSteer) > Math.Abs(joystickSteer) ? keyboardSteer : joystickSteer;
        }

        public int GetThrottle()
        {
            if (!_allowDrivingInput || _overlayInputBlocked)
                return 0;

            var joystickThrottle = UseJoystick ? GetAxis(_throttle) : 0;
            if (!UseKeyboard)
                return joystickThrottle;

            var keyboardThrottle = _settings.KeyboardProgressiveRate == KeyboardProgressiveRate.Off
                ? (_lastState.IsDown(_kbThrottle) ? 100 : 0)
                : (int)(_simThrottle * 100f);

            return Math.Max(joystickThrottle, keyboardThrottle);
        }

        public int GetBrake()
        {
            if (!_allowDrivingInput || _overlayInputBlocked)
                return 0;

            var joystickBrake = UseJoystick ? -GetAxis(_brake) : 0;
            if (!UseKeyboard)
                return joystickBrake;

            var keyboardBrake = _settings.KeyboardProgressiveRate == KeyboardProgressiveRate.Off
                ? (_lastState.IsDown(_kbBrake) ? -100 : 0)
                : (int)(_simBrake * -100f);

            return Math.Min(joystickBrake, keyboardBrake);
        }

        public bool GetReverseRequested() => _allowDrivingInput && UseKeyboard && WasPressed(Key.Z);

        public bool GetForwardRequested() => _allowDrivingInput && UseKeyboard && WasPressed(Key.A);
    }
}
