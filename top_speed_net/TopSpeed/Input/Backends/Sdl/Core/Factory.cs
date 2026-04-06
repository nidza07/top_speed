using System;
using SdlRuntime = TS.Sdl.Runtime;

namespace TopSpeed.Input.Backends.Sdl
{
    internal sealed class Factory : IControllerBackendFactory, IBackendSupportDiagnostics
    {
        public string Id => "sdl";
        public int Priority => 200;

        public bool IsSupported()
        {
            return SdlRuntime.IsAvailable;
        }

        public IControllerBackend Create(IntPtr windowHandle)
        {
            return new Controller();
        }

        public string? GetUnsupportedReason()
        {
            return SdlRuntime.GetError();
        }
    }
}
