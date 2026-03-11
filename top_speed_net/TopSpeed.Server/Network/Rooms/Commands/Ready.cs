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
        private void HandlePlayerReady(PlayerConnection player, PacketRoomPlayerReady ready)
        {
            if (!player.RoomId.HasValue || !_rooms.TryGetValue(player.RoomId.Value, out var room))
            {
                SendProtocolMessage(player, ProtocolMessageCode.NotInRoom, "You are not in a game room.");
                return;
            }

            if (!room.PreparingRace)
            {
                SendProtocolMessage(player, ProtocolMessageCode.Failed, "Race setup has not started yet.");
                return;
            }

            if (!room.PlayerIds.Contains(player.Id))
            {
                SendProtocolMessage(player, ProtocolMessageCode.NotInRoom, "You are not in this game room.");
                return;
            }

            var selectedCar = NormalizeNetworkCar(ready.Car);
            player.Car = selectedCar;
            ApplyVehicleDimensions(player, selectedCar);
            room.PrepareSkips.Remove(player.Id);
            room.PendingLoadouts[player.Id] = new PlayerLoadout(selectedCar, ready.AutomaticTransmission);
            _logger.Debug($"Player ready: room={room.Id}, player={player.Id}, car={selectedCar}, automatic={ready.AutomaticTransmission}, ready={room.PendingLoadouts.Count}/{room.PlayerIds.Count}.");
            SendProtocolMessageToRoom(room, $"{DescribePlayer(player)} is ready.");
            TryStartRaceAfterLoadout(room);
        }

        private void HandlePlayerWithdraw(PlayerConnection player)
        {
            if (!player.RoomId.HasValue || !_rooms.TryGetValue(player.RoomId.Value, out var room))
            {
                SendProtocolMessage(player, ProtocolMessageCode.NotInRoom, "You are not in a game room.");
                return;
            }

            if (!room.PreparingRace)
            {
                SendProtocolMessage(player, ProtocolMessageCode.Failed, "Race setup has not started yet.");
                return;
            }

            if (!room.PlayerIds.Contains(player.Id))
            {
                SendProtocolMessage(player, ProtocolMessageCode.NotInRoom, "You are not in this game room.");
                return;
            }

            room.PendingLoadouts.Remove(player.Id);
            room.PrepareSkips.Add(player.Id);
            player.State = PlayerState.NotReady;
            TouchRoomVersion(room);
            EmitRoomParticipantEvent(
                room,
                RoomEventKind.ParticipantStateChanged,
                player.Id,
                player.PlayerNumber,
                player.State,
                string.IsNullOrWhiteSpace(player.Name) ? $"Player {player.PlayerNumber + 1}" : player.Name);
            SendProtocolMessageToRoom(room, $"{DescribePlayer(player)} left race preparation.");
            TryStartRaceAfterLoadout(room);
        }

    }
}
