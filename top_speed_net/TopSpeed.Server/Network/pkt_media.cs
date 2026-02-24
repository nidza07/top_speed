using System.Net;
using TopSpeed.Protocol;
using TopSpeed.Server.Protocol;

namespace TopSpeed.Server.Network
{
    internal sealed partial class RaceServer
    {
        private void RegisterMediaPackets()
        {
            _pktReg.Add("media", Command.PlayerMediaBegin, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayerMediaBegin(payload, out var begin))
                    OnMediaBegin(player, begin);
                else
                    PacketFail(endPoint, Command.PlayerMediaBegin);
            });
            _pktReg.Add("media", Command.PlayerMediaChunk, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayerMediaChunk(payload, out var chunk))
                    OnMediaChunk(player, chunk);
                else
                    PacketFail(endPoint, Command.PlayerMediaChunk);
            });
            _pktReg.Add("media", Command.PlayerMediaEnd, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayerMediaEnd(payload, out var end))
                    OnMediaEnd(player, end);
                else
                    PacketFail(endPoint, Command.PlayerMediaEnd);
            });
        }
    }
}
