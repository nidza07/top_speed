using System.Net;
using TopSpeed.Protocol;
using TopSpeed.Server.Protocol;

namespace TopSpeed.Server.Network
{
    internal sealed partial class RaceServer
    {
        private void RegisterRacePackets()
        {
            _pktReg.Add("race", Command.PlayerState, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayerState(payload, out var state))
                    HandlePlayerState(player, state);
                else
                    PacketFail(endPoint, Command.PlayerState);
            });
            _pktReg.Add("race", Command.PlayerDataToServer, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayerData(payload, out var data))
                    HandlePlayerData(player, data);
                else
                    PacketFail(endPoint, Command.PlayerDataToServer);
            });
            _pktReg.Add("race", Command.PlayerStarted, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayer(payload, out _))
                    HandlePlayerStarted(player);
                else
                    PacketFail(endPoint, Command.PlayerStarted);
            });
            _pktReg.Add("race", Command.PlayerFinished, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayer(payload, out var finished))
                    HandlePlayerFinished(player, finished);
                else
                    PacketFail(endPoint, Command.PlayerFinished);
            });
            _pktReg.Add("race", Command.PlayerCrashed, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayer(payload, out var crashed))
                    HandlePlayerCrashed(player, crashed);
                else
                    PacketFail(endPoint, Command.PlayerCrashed);
            });
        }
    }
}
