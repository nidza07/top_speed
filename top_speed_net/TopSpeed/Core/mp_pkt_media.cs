using TopSpeed.Network;
using TopSpeed.Protocol;

namespace TopSpeed.Core
{
    internal sealed partial class Game
    {
        private void RegisterMultiplayerMediaPacketHandlers()
        {
            _mpPktReg.Add("media", Command.PlayerMediaBegin, HandleMpPlayerMediaBeginPacket);
            _mpPktReg.Add("media", Command.PlayerMediaChunk, HandleMpPlayerMediaChunkPacket);
            _mpPktReg.Add("media", Command.PlayerMediaEnd, HandleMpPlayerMediaEndPacket);
        }

        private bool HandleMpPlayerMediaBeginPacket(IncomingPacket packet)
        {
            if (_multiplayerRace == null)
                return true;

            if (ClientPacketSerializer.TryReadPlayerMediaBegin(packet.Payload, out var mediaBegin))
                _multiplayerRace.ApplyRemoteMediaBegin(mediaBegin);
            return true;
        }

        private bool HandleMpPlayerMediaChunkPacket(IncomingPacket packet)
        {
            if (_multiplayerRace == null)
                return true;

            if (ClientPacketSerializer.TryReadPlayerMediaChunk(packet.Payload, out var mediaChunk))
                _multiplayerRace.ApplyRemoteMediaChunk(mediaChunk);
            return true;
        }

        private bool HandleMpPlayerMediaEndPacket(IncomingPacket packet)
        {
            if (_multiplayerRace == null)
                return true;

            if (ClientPacketSerializer.TryReadPlayerMediaEnd(packet.Payload, out var mediaEnd))
                _multiplayerRace.ApplyRemoteMediaEnd(mediaEnd);
            return true;
        }
    }
}
