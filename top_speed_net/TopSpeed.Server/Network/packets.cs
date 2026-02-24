using System.Net;
using TopSpeed.Protocol;
using TopSpeed.Server.Protocol;

namespace TopSpeed.Server.Network
{
    internal sealed partial class RaceServer
    {
        private void OnPacket(IPEndPoint endPoint, byte[] payload)
        {
            if (!PacketSerializer.TryReadHeader(payload, out var header))
            {
                _logger.Warning($"Dropped packet with invalid header from {endPoint}.");
                return;
            }
            if (header.Version != ProtocolConstants.Version)
            {
                _logger.Debug($"Dropped packet with protocol version mismatch from {endPoint}: received={header.Version}, expected={ProtocolConstants.Version}.");
                return;
            }

            lock (_lock)
            {
                var player = GetOrAddPlayer(endPoint);
                if (player == null)
                    return;

                player.LastSeenUtc = DateTime.UtcNow;
                if (!_pktReg.TryDispatch(header.Command, player, payload, endPoint))
                    _logger.Warning($"Ignoring unknown packet command {(byte)header.Command} from {endPoint}.");
            }
        }

        private void RegisterPackets()
        {
            RegisterCorePackets();
            RegisterRacePackets();
            RegisterMediaPackets();
            RegisterRoomPackets();
        }

    }
}
