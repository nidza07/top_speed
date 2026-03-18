using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib;
using TopSpeed.Bots;
using TopSpeed.Collision;
using TopSpeed.Data;
using TopSpeed.Protocol;
using TopSpeed.Server.Protocol;
using TopSpeed.Server.Bots;

namespace TopSpeed.Server.Network
{
    internal sealed partial class RaceServer
    {
        private sealed class CollisionActor
        {
            public uint Id;
            public bool IsBot;
            public byte PlayerNumber;
            public PlayerConnection Player = null!;
            public RoomBot Bot = null!;
        }

        private static ulong MakePairKey(uint first, uint second)
        {
            if (first > second)
            {
                var swap = first;
                first = second;
                second = swap;
            }

            return ((ulong)first << 32) | second;
        }

        private void CheckForBumps()
        {
            foreach (var room in _rooms.Values)
            {
                var racers = room.PlayerIds.Where(id => _players.TryGetValue(id, out var p) && p.State == PlayerState.Racing)
                    .Select(id => _players[id]).ToList();
                var botRacers = room.Bots.Where(bot => bot.State == PlayerState.Racing).ToList();
                var actors = new List<CollisionActor>(racers.Count + botRacers.Count);
                var activePairs = new HashSet<ulong>();

                for (var i = 0; i < racers.Count; i++)
                {
                    var player = racers[i];
                    actors.Add(new CollisionActor
                    {
                        Id = player.Id,
                        IsBot = false,
                        PlayerNumber = player.PlayerNumber,
                        Player = player
                    });
                }

                for (var i = 0; i < botRacers.Count; i++)
                {
                    var bot = botRacers[i];
                    actors.Add(new CollisionActor
                    {
                        Id = bot.Id,
                        IsBot = true,
                        PlayerNumber = bot.PlayerNumber,
                        Bot = bot
                    });
                }

                for (var i = 0; i < actors.Count; i++)
                {
                    for (var j = i + 1; j < actors.Count; j++)
                    {
                        var first = actors[i];
                        var second = actors[j];
                        if (!VehicleCollisionResolver.TryResolve(
                                BuildCollisionBody(first),
                                BuildCollisionBody(second),
                                out var response))
                            continue;

                        var pairKey = MakePairKey(first.Id, second.Id);
                        activePairs.Add(pairKey);
                        if (room.ActiveBumpPairs.Contains(pairKey))
                            continue;

                        ApplyCollisionImpulse(first, response.First);
                        ApplyCollisionImpulse(second, response.Second);

                        if (!first.IsBot && !second.IsBot)
                            _bumpEventsHumanHuman++;
                        else
                            _bumpEventsHumanBot++;
                    }
                }

                room.ActiveBumpPairs.RemoveWhere(key => !activePairs.Contains(key));
                foreach (var pairKey in activePairs)
                    room.ActiveBumpPairs.Add(pairKey);
            }
        }

        private static VehicleCollisionBody BuildCollisionBody(CollisionActor actor)
        {
            if (actor.IsBot)
            {
                return new VehicleCollisionBody(
                    actor.Bot.PositionX,
                    actor.Bot.PositionY,
                    actor.Bot.SpeedKph,
                    actor.Bot.WidthM,
                    actor.Bot.LengthM,
                    actor.Bot.PhysicsConfig.MassKg);
            }

            return new VehicleCollisionBody(
                actor.Player.PositionX,
                actor.Player.PositionY,
                actor.Player.Speed,
                actor.Player.WidthM,
                actor.Player.LengthM,
                actor.Player.MassKg);
        }

        private void ApplyCollisionImpulse(CollisionActor actor, in VehicleCollisionImpulse impulse)
        {
            if (actor.IsBot)
            {
                ApplyBotCollision(actor.Bot, impulse);
                TriggerBotHorn(actor.Bot, "bump", 0.2f);
                return;
            }

            SendStream(actor.Player, PacketSerializer.WritePlayerBumped(new PacketPlayerBumped
            {
                PlayerId = actor.Player.Id,
                PlayerNumber = actor.Player.PlayerNumber,
                BumpX = impulse.BumpX,
                BumpY = impulse.BumpY,
                SpeedDeltaKph = impulse.SpeedDeltaKph
            }), PacketStream.RaceEvent, PacketDeliveryKind.Sequenced);
        }

        private static void ApplyBotCollision(RoomBot bot, in VehicleCollisionImpulse impulse)
        {
            bot.PositionX += 2f * impulse.BumpX;
            bot.PositionY += impulse.BumpY;
            if (bot.PositionY < 0f)
                bot.PositionY = 0f;

            bot.SpeedKph += impulse.SpeedDeltaKph;
            if (bot.SpeedKph < 0f)
                bot.SpeedKph = 0f;

            var state = bot.PhysicsState;
            state.PositionX = bot.PositionX;
            state.PositionY = bot.PositionY;
            state.SpeedKph = bot.SpeedKph;
            bot.PhysicsState = state;
        }

    }
}
