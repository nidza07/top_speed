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
        private void HandleCreateRoom(PlayerConnection player, PacketRoomCreate packet)
        {
            var roomName = (packet.RoomName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(roomName))
                roomName = $"Game {_nextRoomId}";
            if (roomName.Length > ProtocolConstants.MaxRoomNameLength)
                roomName = roomName.Substring(0, ProtocolConstants.MaxRoomNameLength);

            var roomType = packet.RoomType;
            var playersToStart = packet.PlayersToStart;
            if (playersToStart < 2 || playersToStart > ProtocolConstants.MaxRoomPlayersToStart)
                playersToStart = 2;
            if (roomType == GameRoomType.OneOnOne)
                playersToStart = 2;

            var room = new RaceRoom(_nextRoomId++, roomName, roomType, playersToStart);
            _rooms[room.Id] = room;
            SetTrack(room, room.TrackName);
            JoinRoom(player, room);
            EmitRoomLifecycleEvent(room, RoomEventKind.RoomCreated);
            BroadcastLobbyAnnouncement($"{DescribePlayer(player)} created game room {room.Name}.");
            _logger.Info($"Room created: room={room.Id} \"{room.Name}\", host={player.Id}, type={room.RoomType}, playersToStart={room.PlayersToStart}.");
        }

        private void HandleJoinRoom(PlayerConnection player, PacketRoomJoin packet)
        {
            if (!_rooms.TryGetValue(packet.RoomId, out var room))
            {
                SendProtocolMessage(player, ProtocolMessageCode.RoomNotFound, "Game room not found.");
                return;
            }

            if (room.RaceStarted || room.PreparingRace)
            {
                _joinDeniedRaceInProgress++;
                _logger.Debug($"Join denied: player={player.Id}, room={room.Id}, raceStarted={room.RaceStarted}, preparing={room.PreparingRace}.");
                SendProtocolMessage(player, ProtocolMessageCode.Failed, "This game room is currently in progress.");
                return;
            }

            if (GetRoomParticipantCount(room) >= room.PlayersToStart)
            {
                SendProtocolMessage(player, ProtocolMessageCode.RoomFull, "This game room is unavailable because it is full.");
                return;
            }

            JoinRoom(player, room);
            EmitRoomLifecycleEvent(room, RoomEventKind.RoomSummaryUpdated);
            _logger.Info($"Player joined room: room={room.Id} \"{room.Name}\", player={player.Id}, participants={GetRoomParticipantCount(room)}/{room.PlayersToStart}.");
        }

        private void HandleLeaveRoom(PlayerConnection player, bool notify)
        {
            if (!player.RoomId.HasValue)
            {
                SendProtocolMessage(player, ProtocolMessageCode.NotInRoom, "You are not in a game room.");
                return;
            }

            var roomId = player.RoomId.Value;
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                player.RoomId = null;
                player.Live = null;
                SendRoomState(player, null);
                return;
            }

            var oldNumber = player.PlayerNumber;
            var leftName = DescribePlayer(player);
            room.PlayerIds.Remove(player.Id);
            var previousHostId = room.HostId;
            player.RoomId = null;
            player.PlayerNumber = 0;
            player.State = PlayerState.NotReady;
            room.PendingLoadouts.Remove(player.Id);
            room.PrepareSkips.Remove(player.Id);
            room.MediaMap.Remove(player.Id);
            StopLive(player, room, notifyRoom: notify);
            player.IncomingMedia = null;
            player.MediaLoaded = false;
            player.MediaPlaying = false;
            player.MediaId = 0;

            if (notify)
            {
                var stream = room.RaceStarted ? PacketStream.RaceEvent : PacketStream.Room;
                SendToRoomOnStream(room, PacketSerializer.WritePlayer(Command.PlayerDisconnected, player.Id, oldNumber), stream);
                SendProtocolMessageToRoom(room, $"{leftName} has left the game.");
            }

            SendRoomState(player, null);

            if (room.PlayerIds.Count == 0)
            {
                _rooms.Remove(room.Id);
                EmitRoomRemovedEvent(roomId, room.Name);
                _logger.Info($"Room closed: room={room.Id} \"{room.Name}\".");
            }
            else
            {
                if (room.HostId == player.Id)
                    room.HostId = room.PlayerIds.OrderBy(x => x).First();
                if (room.RaceStarted && CountActiveRaceParticipants(room) == 0)
                    StopRace(room);
                if (room.PreparingRace)
                    TryStartRaceAfterLoadout(room);
                CompactRoomNumbers(room);
                TouchRoomVersion(room);
                EmitRoomParticipantEvent(room, RoomEventKind.ParticipantLeft, player.Id, oldNumber, PlayerState.NotReady, leftName);
                if (previousHostId != room.HostId)
                    EmitRoomLifecycleEvent(room, RoomEventKind.HostChanged);
                EmitRoomLifecycleEvent(room, RoomEventKind.RoomSummaryUpdated);
            }
            _logger.Info($"Player left room: room={room.Id} \"{room.Name}\", player={player.Id}, notify={notify}.");
        }

        private void JoinRoom(PlayerConnection player, RaceRoom room)
        {
            if (player.RoomId.HasValue)
                HandleLeaveRoom(player, true);

            room.PlayerIds.Add(player.Id);
            if (room.HostId == 0 || !room.PlayerIds.Contains(room.HostId))
                room.HostId = player.Id;

            player.RoomId = room.Id;
            player.PlayerNumber = byte.MaxValue;
            player.State = PlayerState.NotReady;
            room.PrepareSkips.Remove(player.Id);
            CompactRoomNumbers(room);

            SendStream(player, PacketSerializer.WritePlayerNumber(player.Id, player.PlayerNumber), PacketStream.Control);
            SendTrack(room, player);
            SyncMediaTo(room, player);
            SyncLiveTo(room, player);
            TouchRoomVersion(room);
            SendRoomState(player, room);
            EmitRoomParticipantEvent(
                room,
                RoomEventKind.ParticipantJoined,
                player.Id,
                player.PlayerNumber,
                player.State,
                string.IsNullOrWhiteSpace(player.Name) ? $"Player {player.PlayerNumber + 1}" : player.Name);

            var joinedName = string.IsNullOrWhiteSpace(player.Name)
                ? $"Player {player.PlayerNumber + 1}"
                : player.Name;
            var joined = new PacketPlayerJoined { PlayerId = player.Id, PlayerNumber = player.PlayerNumber, Name = joinedName };
            SendToRoomExceptOnStream(room, player.Id, PacketSerializer.WritePlayerJoined(joined), PacketStream.Room);
            _logger.Debug($"Join room assignment: room={room.Id}, player={player.Id}, playerNumber={player.PlayerNumber}, host={room.HostId}.");
        }

        private int FindFreeRoomNumber(RaceRoom room)
        {
            for (var i = 0; i < room.PlayersToStart; i++)
            {
                var usedByPlayer = room.PlayerIds.Any(id => _players.TryGetValue(id, out var p) && p.PlayerNumber == i);
                var usedByBot = room.Bots.Any(bot => bot.PlayerNumber == i);
                var used = usedByPlayer || usedByBot;
                if (!used)
                    return i;
            }

            return 0;
        }

    }
}
