using System;
using System.Collections.Generic;
using System.Linq;
using TopSpeed.Protocol;
using TopSpeed.Server.Protocol;
using TopSpeed.Server.Bots;

namespace TopSpeed.Server.Network
{
    internal sealed partial class RaceServer
    {
        private void CompactRoomNumbers(RaceRoom room)
        {
            if (room == null || room.RaceStarted || room.PreparingRace)
                return;

            var humans = room.PlayerIds
                .Where(id => _players.TryGetValue(id, out _))
                .Select(id => _players[id])
                .OrderBy(player => player.PlayerNumber)
                .ThenBy(player => player.Id)
                .ToList();

            var bots = room.Bots
                .OrderBy(bot => bot.PlayerNumber)
                .ThenBy(bot => bot.AddedOrder)
                .ThenBy(bot => bot.Id)
                .ToList();

            var changedPlayers = new List<PlayerConnection>();
            var changedBots = new List<RoomBot>();
            var next = 0;

            for (var i = 0; i < humans.Count; i++)
            {
                var expected = (byte)next++;
                if (humans[i].PlayerNumber == expected)
                    continue;

                humans[i].PlayerNumber = expected;
                changedPlayers.Add(humans[i]);
            }

            for (var i = 0; i < bots.Count; i++)
            {
                var expected = (byte)next++;
                if (bots[i].PlayerNumber == expected)
                    continue;

                bots[i].PlayerNumber = expected;
                changedBots.Add(bots[i]);
            }

            if (changedPlayers.Count == 0 && changedBots.Count == 0)
                return;

            TouchRoomVersion(room);

            for (var i = 0; i < changedPlayers.Count; i++)
            {
                var player = changedPlayers[i];
                SendStream(player, PacketSerializer.WritePlayerNumber(player.Id, player.PlayerNumber), PacketStream.Control);
                EmitRoomParticipantEvent(
                    room,
                    RoomEventKind.ParticipantStateChanged,
                    player.Id,
                    player.PlayerNumber,
                    player.State,
                    string.IsNullOrWhiteSpace(player.Name) ? $"Player {player.PlayerNumber + 1}" : player.Name);
            }

            for (var i = 0; i < changedBots.Count; i++)
            {
                var bot = changedBots[i];
                EmitRoomParticipantEvent(
                    room,
                    RoomEventKind.ParticipantStateChanged,
                    bot.Id,
                    bot.PlayerNumber,
                    bot.State,
                    FormatBotDisplayName(bot));
            }

            EmitRoomLifecycleEvent(room, RoomEventKind.RoomSummaryUpdated);
            _logger.Debug($"Room numbers compacted: room={room.Id}, humans={changedPlayers.Count}, bots={changedBots.Count}.");
        }
    }
}
