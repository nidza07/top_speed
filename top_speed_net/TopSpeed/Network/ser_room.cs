using System;
using TopSpeed.Data;
using TopSpeed.Protocol;

namespace TopSpeed.Network
{
    internal static partial class ClientPacketSerializer
    {
        public static bool TryReadPlayerJoined(byte[] data, out PacketPlayerJoined packet)
        {
            packet = new PacketPlayerJoined();
            if (data.Length < 2 + 4 + 1 + ProtocolConstants.MaxPlayerNameLength)
                return false;
            if (data[0] != ProtocolConstants.Version || data[1] != (byte)Command.PlayerJoined)
                return false;
            var reader = new PacketReader(data);
            reader.ReadByte();
            reader.ReadByte();
            packet.PlayerId = reader.ReadUInt32();
            packet.PlayerNumber = reader.ReadByte();
            packet.Name = reader.ReadFixedString(ProtocolConstants.MaxPlayerNameLength);
            return true;
        }

        public static bool TryReadRoomList(byte[] data, out PacketRoomList packet)
        {
            packet = new PacketRoomList();
            if (data.Length < 2 + 1)
                return false;
            if (data[0] != ProtocolConstants.Version || data[1] != (byte)Command.RoomList)
                return false;
            var reader = new PacketReader(data);
            reader.ReadByte();
            reader.ReadByte();
            var count = reader.ReadByte();
            var stride = 4 + ProtocolConstants.MaxRoomNameLength + 1 + 1 + 1 + 1 + 12;
            var available = (data.Length - 3) / stride;
            var actualCount = Math.Min(count, available);
            var rooms = new PacketRoomSummary[actualCount];
            for (var i = 0; i < actualCount; i++)
            {
                rooms[i] = new PacketRoomSummary
                {
                    RoomId = reader.ReadUInt32(),
                    RoomName = reader.ReadFixedString(ProtocolConstants.MaxRoomNameLength),
                    RoomType = (GameRoomType)reader.ReadByte(),
                    PlayerCount = reader.ReadByte(),
                    PlayersToStart = reader.ReadByte(),
                    RaceStarted = reader.ReadBool(),
                    TrackName = reader.ReadFixedString(12)
                };
            }
            packet.Rooms = rooms;
            return true;
        }

        public static bool TryReadRoomState(byte[] data, out PacketRoomState packet)
        {
            packet = new PacketRoomState();
            if (data.Length < 2 + 4 + 4 + ProtocolConstants.MaxRoomNameLength + 1 + 1 + 1 + 1 + 1 + 12 + 1 + 1)
                return false;
            if (data[0] != ProtocolConstants.Version || data[1] != (byte)Command.RoomState)
                return false;
            var reader = new PacketReader(data);
            reader.ReadByte();
            reader.ReadByte();
            packet.RoomId = reader.ReadUInt32();
            packet.HostPlayerId = reader.ReadUInt32();
            packet.RoomName = reader.ReadFixedString(ProtocolConstants.MaxRoomNameLength);
            packet.RoomType = (GameRoomType)reader.ReadByte();
            packet.PlayersToStart = reader.ReadByte();
            packet.InRoom = reader.ReadBool();
            packet.IsHost = reader.ReadBool();
            packet.RaceStarted = reader.ReadBool();
            packet.TrackName = reader.ReadFixedString(12);
            packet.Laps = reader.ReadByte();
            var count = reader.ReadByte();
            var stride = 4 + 1 + 1 + ProtocolConstants.MaxPlayerNameLength;
            var available = Math.Max(0, (data.Length - (2 + 4 + 4 + ProtocolConstants.MaxRoomNameLength + 1 + 1 + 1 + 1 + 1 + 12 + 1 + 1)) / stride);
            var actualCount = Math.Min(count, available);
            var players = new PacketRoomPlayer[actualCount];
            for (var i = 0; i < actualCount; i++)
            {
                players[i] = new PacketRoomPlayer
                {
                    PlayerId = reader.ReadUInt32(),
                    PlayerNumber = reader.ReadByte(),
                    State = (PlayerState)reader.ReadByte(),
                    Name = reader.ReadFixedString(ProtocolConstants.MaxPlayerNameLength)
                };
            }
            packet.Players = players;
            return true;
        }

        public static byte[] WriteRoomListRequest()
        {
            return WriteGeneral(Command.RoomListRequest);
        }

        public static byte[] WriteRoomCreate(string roomName, GameRoomType roomType, byte playersToStart)
        {
            var buffer = WritePacketHeader(Command.RoomCreate, ProtocolConstants.MaxRoomNameLength + 1 + 1);
            var writer = new PacketWriter(buffer);
            writer.WriteByte(ProtocolConstants.Version);
            writer.WriteByte((byte)Command.RoomCreate);
            writer.WriteFixedString(roomName ?? string.Empty, ProtocolConstants.MaxRoomNameLength);
            writer.WriteByte((byte)roomType);
            writer.WriteByte(playersToStart);
            return buffer;
        }

        public static byte[] WriteRoomJoin(uint roomId)
        {
            var buffer = WritePacketHeader(Command.RoomJoin, 4);
            var writer = new PacketWriter(buffer);
            writer.WriteByte(ProtocolConstants.Version);
            writer.WriteByte((byte)Command.RoomJoin);
            writer.WriteUInt32(roomId);
            return buffer;
        }

        public static byte[] WriteRoomLeave()
        {
            return WriteGeneral(Command.RoomLeave);
        }

        public static byte[] WriteRoomSetTrack(string trackName)
        {
            var buffer = WritePacketHeader(Command.RoomSetTrack, 12);
            var writer = new PacketWriter(buffer);
            writer.WriteByte(ProtocolConstants.Version);
            writer.WriteByte((byte)Command.RoomSetTrack);
            writer.WriteFixedString(trackName ?? string.Empty, 12);
            return buffer;
        }

        public static byte[] WriteRoomSetLaps(byte laps)
        {
            var buffer = WritePacketHeader(Command.RoomSetLaps, 1);
            var writer = new PacketWriter(buffer);
            writer.WriteByte(ProtocolConstants.Version);
            writer.WriteByte((byte)Command.RoomSetLaps);
            writer.WriteByte(laps);
            return buffer;
        }

        public static byte[] WriteRoomStartRace()
        {
            return WriteGeneral(Command.RoomStartRace);
        }

        public static byte[] WriteRoomSetPlayersToStart(byte playersToStart)
        {
            var buffer = WritePacketHeader(Command.RoomSetPlayersToStart, 1);
            var writer = new PacketWriter(buffer);
            writer.WriteByte(ProtocolConstants.Version);
            writer.WriteByte((byte)Command.RoomSetPlayersToStart);
            writer.WriteByte(playersToStart);
            return buffer;
        }

        public static byte[] WriteRoomAddBot()
        {
            return WriteGeneral(Command.RoomAddBot);
        }

        public static byte[] WriteRoomRemoveBot()
        {
            return WriteGeneral(Command.RoomRemoveBot);
        }

        public static byte[] WriteRoomPlayerReady(CarType car, bool automaticTransmission)
        {
            var buffer = WritePacketHeader(Command.RoomPlayerReady, 1 + 1);
            var writer = new PacketWriter(buffer);
            writer.WriteByte(ProtocolConstants.Version);
            writer.WriteByte((byte)Command.RoomPlayerReady);
            writer.WriteByte((byte)car);
            writer.WriteBool(automaticTransmission);
            return buffer;
        }
    }
}
