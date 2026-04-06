using System;
using System.Collections.Generic;
using TopSpeed.Input.Devices.Keyboard;
using TopSpeed.Runtime;

namespace TopSpeed.Input
{
    internal sealed class BackendRegistry : IBackendRegistry
    {
        private readonly List<IKeyboardBackendFactory> _keyboardFactories;
        private readonly List<IControllerBackendFactory> _controllerFactories;

        internal BackendRegistry(
            IEnumerable<IKeyboardBackendFactory> keyboardFactories,
            IEnumerable<IControllerBackendFactory> controllerFactories)
        {
            if (keyboardFactories == null)
                throw new ArgumentNullException(nameof(keyboardFactories));
            if (controllerFactories == null)
                throw new ArgumentNullException(nameof(controllerFactories));

            _keyboardFactories = new List<IKeyboardBackendFactory>(keyboardFactories);
            _controllerFactories = new List<IControllerBackendFactory>(controllerFactories);
            _keyboardFactories.Sort((left, right) =>
            {
                var byPriority = right.Priority.CompareTo(left.Priority);
                return byPriority != 0 ? byPriority : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
            });
            _controllerFactories.Sort((left, right) =>
            {
                var byPriority = right.Priority.CompareTo(left.Priority);
                return byPriority != 0 ? byPriority : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
            });
        }

        public IKeyboardDevice CreateKeyboard(IntPtr windowHandle, IKeyboardEventSource? eventSource)
        {
            var attempts = new List<string>();
            for (var i = 0; i < _keyboardFactories.Count; i++)
            {
                var factory = _keyboardFactories[i];
                if (!factory.IsSupported())
                {
                    attempts.Add(FormatUnsupported(factory));
                    continue;
                }

                try
                {
                    var backend = factory.Create(windowHandle, eventSource);
                    if (backend != null)
                        return backend;

                    attempts.Add($"{factory.Id}: returned null");
                }
                catch (Exception ex)
                {
                    attempts.Add($"{factory.Id}: {ex.GetType().Name}");
                }
            }

            throw new InvalidOperationException(
                $"Unable to initialize keyboard backend. Attempts: {FormatAttempts(attempts)}");
        }

        public IControllerBackend CreateController(IntPtr windowHandle)
        {
            var attempts = new List<string>();
            for (var i = 0; i < _controllerFactories.Count; i++)
            {
                var factory = _controllerFactories[i];
                if (!factory.IsSupported())
                {
                    attempts.Add(FormatUnsupported(factory));
                    continue;
                }

                try
                {
                    var backend = factory.Create(windowHandle);
                    if (backend != null)
                        return backend;

                    attempts.Add($"{factory.Id}: returned null");
                }
                catch (Exception ex)
                {
                    attempts.Add($"{factory.Id}: {ex.GetType().Name}");
                }
            }

            throw new InvalidOperationException(
                $"Unable to initialize controller backend. Attempts: {FormatAttempts(attempts)}");
        }

        private static string FormatAttempts(List<string> attempts)
        {
            if (attempts == null || attempts.Count == 0)
                return "none";

            return string.Join(", ", attempts);
        }

        private static string FormatUnsupported(object factory)
        {
            if (factory is IBackendSupportDiagnostics diagnostics)
            {
                var reason = diagnostics.GetUnsupportedReason();
                if (!string.IsNullOrWhiteSpace(reason))
                    return $"{GetFactoryId(factory)}: unsupported ({reason})";
            }

            return $"{GetFactoryId(factory)}: unsupported";
        }

        private static string GetFactoryId(object factory)
        {
            return factory switch
            {
                IKeyboardBackendFactory keyboard => keyboard.Id,
                IControllerBackendFactory controller => controller.Id,
                _ => factory.GetType().Name
            };
        }
    }
}
