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
        private void HandleSetTrack(PlayerConnection player, PacketRoomSetTrack packet)
        {
            if (!TryGetHostedRoom(player, out var room))
                return;
            if (room.RaceStarted || room.PreparingRace)
            {
                _roomMutationDenied++;
                _logger.Debug($"Room track change denied: room={room.Id}, player={player.Id}, raceStarted={room.RaceStarted}, preparing={room.PreparingRace}.");
                SendProtocolMessage(player, ProtocolMessageCode.Failed, "Cannot change track while race setup or race is active.");
                return;
            }

            var trackName = (packet.TrackName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trackName))
            {
                SendProtocolMessage(player, ProtocolMessageCode.InvalidTrack, "Track cannot be empty.");
                return;
            }

            SetTrack(room, trackName);
            SendTrackToNotReady(room);
            TouchRoomVersion(room);
            EmitRoomLifecycleEvent(room, RoomEventKind.TrackChanged);
        }

        private void HandleSetLaps(PlayerConnection player, PacketRoomSetLaps packet)
        {
            if (!TryGetHostedRoom(player, out var room))
                return;
            if (room.RaceStarted || room.PreparingRace)
            {
                _roomMutationDenied++;
                _logger.Debug($"Room laps change denied: room={room.Id}, player={player.Id}, raceStarted={room.RaceStarted}, preparing={room.PreparingRace}.");
                SendProtocolMessage(player, ProtocolMessageCode.Failed, "Cannot change laps while race setup or race is active.");
                return;
            }

            if (packet.Laps < 1 || packet.Laps > 16)
            {
                SendProtocolMessage(player, ProtocolMessageCode.InvalidLaps, "Laps must be between 1 and 16.");
                return;
            }

            room.Laps = packet.Laps;
            if (room.TrackSelected)
                SetTrack(room, room.TrackName);
            SendTrackToNotReady(room);
            TouchRoomVersion(room);
            EmitRoomLifecycleEvent(room, RoomEventKind.LapsChanged);
        }

        private void HandleStartRace(PlayerConnection player)
        {
            if (!TryGetHostedRoom(player, out var room))
                return;

            var minimumParticipants = GetMinimumParticipantsToStart(room);
            if (GetRoomParticipantCount(room) < minimumParticipants)
            {
                SendProtocolMessage(player, ProtocolMessageCode.Failed, $"Not enough players. {minimumParticipants} required to start.");
                return;
            }

            if (room.RaceStarted)
            {
                SendProtocolMessage(player, ProtocolMessageCode.Failed, "A race is already in progress.");
                return;
            }

            if (room.PreparingRace)
            {
                SendProtocolMessage(player, ProtocolMessageCode.Failed, "Race setup is already in progress.");
                return;
            }

            room.PreparingRace = true;
            room.PendingLoadouts.Clear();
            room.PrepareSkips.Clear();
            AssignRandomBotLoadouts(room);
            AnnounceBotsReady(room);
            TouchRoomVersion(room);
            _logger.Info($"Race prepare started: room={room.Id} \"{room.Name}\", requestedBy={player.Id}, humans={room.PlayerIds.Count}, bots={room.Bots.Count}, capacity={room.PlayersToStart}, minStart={minimumParticipants}.");

            SendProtocolMessageToRoom(room, $"{DescribePlayer(player)} is about to start the game. Choose your vehicle and transmission mode.");
            EmitRoomLifecycleEvent(room, RoomEventKind.PrepareStarted);
            TryStartRaceAfterLoadout(room);
        }

        private void HandleSetPlayersToStart(PlayerConnection player, PacketRoomSetPlayersToStart packet)
        {
            if (!TryGetHostedRoom(player, out var room))
                return;
            if (room.RaceStarted || room.PreparingRace)
            {
                _roomMutationDenied++;
                _logger.Debug($"Room player-limit change denied: room={room.Id}, player={player.Id}, raceStarted={room.RaceStarted}, preparing={room.PreparingRace}.");
                SendProtocolMessage(player, ProtocolMessageCode.Failed, "Cannot change player limit while race setup or race is active.");
                return;
            }

            var value = packet.PlayersToStart;
            if (value < 2 || value > ProtocolConstants.MaxRoomPlayersToStart)
            {
                SendProtocolMessage(player, ProtocolMessageCode.InvalidPlayersToStart, "Player limit must be between 2 and 10.");
                return;
            }

            if (room.RoomType == GameRoomType.OneOnOne && value != 2)
            {
                SendProtocolMessage(player, ProtocolMessageCode.InvalidPlayersToStart, "One-on-one rooms always allow a maximum of 2 players.");
                return;
            }

            if (GetRoomParticipantCount(room) > value)
            {
                SendProtocolMessage(player, ProtocolMessageCode.InvalidPlayersToStart, "Cannot set lower than current players in room.");
                return;
            }

            room.PlayersToStart = value;
            TouchRoomVersion(room);
            EmitRoomLifecycleEvent(room, RoomEventKind.PlayersToStartChanged);
            EmitRoomLifecycleEvent(room, RoomEventKind.RoomSummaryUpdated);
        }

    }
}
