using System;
using System.Linq;
using LiteNetLib;
using TopSpeed.Bots;
using TopSpeed.Data;
using TopSpeed.Protocol;
using TopSpeed.Server.Protocol;
using TopSpeed.Server.Tracks;

namespace TopSpeed.Server.Network
{
    internal sealed partial class RaceServer
    {
        private void AssignRandomBotLoadouts(RaceRoom room)
        {
            foreach (var bot in room.Bots)
            {
                bot.Car = (CarType)_random.Next((int)CarType.Vehicle1, (int)CarType.CustomVehicle);
                bot.AutomaticTransmission = _random.Next(0, 2) == 0;
                ApplyVehicleDimensions(bot, bot.Car);
            }
        }

        private void AnnounceBotsReady(RaceRoom room)
        {
            foreach (var bot in room.Bots.OrderBy(b => b.PlayerNumber))
            {
                SendProtocolMessageToRoom(room, $"{FormatBotJoinName(bot)} is ready.");
            }
        }

        private void TryStartRaceAfterLoadout(RaceRoom room)
        {
            if (!room.PreparingRace)
                return;
            var minimumParticipants = GetMinimumParticipantsToStart(room);
            if (GetRoomParticipantCount(room) < minimumParticipants)
            {
                room.PreparingRace = false;
                room.PendingLoadouts.Clear();
                room.PrepareSkips.Clear();
                TouchRoomVersion(room);
                EmitRoomLifecycleEvent(room, RoomEventKind.PrepareCancelled);
                SendProtocolMessageToRoom(room, "Race start cancelled because there are not enough players.");
                _logger.Info($"Race prepare cancelled: room={room.Id} \"{room.Name}\", participants={GetRoomParticipantCount(room)}, minStart={minimumParticipants}, capacity={room.PlayersToStart}.");
                return;
            }

            var readyHumans = CountReadyHumans(room);
            var skippedHumans = CountSkippedHumans(room);
            var unresolvedHumans = Math.Max(0, room.PlayerIds.Count - (readyHumans + skippedHumans));
            if (unresolvedHumans > 0)
            {
                _logger.Debug($"Waiting for loadouts: room={room.Id}, ready={readyHumans}, skipped={skippedHumans}, totalHumans={room.PlayerIds.Count}.");
                return;
            }

            var activeParticipants = readyHumans + room.Bots.Count;
            if (activeParticipants < minimumParticipants)
            {
                room.PreparingRace = false;
                room.PendingLoadouts.Clear();
                room.PrepareSkips.Clear();
                TouchRoomVersion(room);
                EmitRoomLifecycleEvent(room, RoomEventKind.PrepareCancelled);
                SendProtocolMessageToRoom(room, "Race start cancelled because there are not enough ready players.");
                _logger.Info($"Race prepare cancelled after loadout: room={room.Id} \"{room.Name}\", active={activeParticipants}, minStart={minimumParticipants}.");
                return;
            }

            room.PreparingRace = false;
            SendProtocolMessageToRoom(room, "All players are ready. Starting game.");
            _logger.Info($"All loadouts ready: room={room.Id} \"{room.Name}\", starting race.");
            StartRace(room);
        }

        private int CountReadyHumans(RaceRoom room)
        {
            return room.PendingLoadouts.Keys.Count(id => room.PlayerIds.Contains(id));
        }

        private int CountSkippedHumans(RaceRoom room)
        {
            return room.PrepareSkips.Count(id => room.PlayerIds.Contains(id));
        }

        private static int GetMinimumParticipantsToStart(RaceRoom room)
        {
            if (room == null)
                return 1;

            // Room player count now acts as capacity. One-on-one still requires two racers.
            return 2;
        }

    }
}
