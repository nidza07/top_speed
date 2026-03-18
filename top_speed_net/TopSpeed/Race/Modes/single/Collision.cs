using System;
using System.Collections.Generic;
using TopSpeed.Collision;
using TopSpeed.Vehicles;

namespace TopSpeed.Race
{
    internal sealed partial class SingleRaceMode
    {
        private readonly struct CollisionActor
        {
            public CollisionActor(uint id, bool isPlayer, ComputerPlayer? bot)
            {
                Id = id;
                IsPlayer = isPlayer;
                Bot = bot;
            }

            public uint Id { get; }
            public bool IsPlayer { get; }
            public ComputerPlayer? Bot { get; }
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
            var actors = new List<CollisionActor>(_nComputerPlayers + 1);
            var activePairs = new HashSet<ulong>();

            if (_car.State == CarState.Running)
                actors.Add(new CollisionActor((uint)_playerNumber, isPlayer: true, bot: null));

            for (var i = 0; i < _nComputerPlayers; i++)
            {
                var bot = _computerPlayers[i];
                if (bot == null)
                    continue;
                if (bot.State == ComputerPlayer.ComputerState.Running && !bot.Finished)
                    actors.Add(new CollisionActor((uint)bot.PlayerNumber, isPlayer: false, bot: bot));
            }

            for (var i = 0; i < actors.Count; i++)
            {
                for (var j = i + 1; j < actors.Count; j++)
                {
                    var first = actors[i];
                    var second = actors[j];
                    if (!VehicleCollisionResolver.TryResolve(BuildCollisionBody(first), BuildCollisionBody(second), out var response))
                        continue;

                    var pairKey = MakePairKey(first.Id, second.Id);
                    activePairs.Add(pairKey);
                    if (_activeBumpPairs.Contains(pairKey))
                        continue;

                    ApplyCollisionImpulse(first, response.First);
                    ApplyCollisionImpulse(second, response.Second);
                }
            }

            _activeBumpPairs.RemoveWhere(key => !activePairs.Contains(key));
            foreach (var pairKey in activePairs)
                _activeBumpPairs.Add(pairKey);
        }

        private VehicleCollisionBody BuildCollisionBody(in CollisionActor actor)
        {
            if (actor.IsPlayer)
            {
                return new VehicleCollisionBody(
                    _car.PositionX,
                    _car.PositionY,
                    _car.Speed,
                    _car.WidthM,
                    _car.LengthM,
                    _car.MassKg);
            }

            var bot = actor.Bot!;
            return new VehicleCollisionBody(
                bot.PositionX,
                bot.PositionY,
                bot.Speed,
                bot.WidthM,
                bot.LengthM,
                bot.MassKg);
        }

        private void ApplyCollisionImpulse(in CollisionActor actor, in VehicleCollisionImpulse impulse)
        {
            if (actor.IsPlayer)
            {
                _car.Bump(impulse.BumpX, impulse.BumpY, impulse.SpeedDeltaKph);
                return;
            }

            actor.Bot!.Bump(impulse.BumpX, impulse.BumpY, impulse.SpeedDeltaKph);
        }

        private bool CheckFinish()
        {
            for (var i = 0; i < _nComputerPlayers; i++)
            {
                if (_computerPlayers[i]?.Finished == false)
                    return false;
            }
            if (_lap <= _nrOfLaps)
                return false;
            return true;
        }
    }
}

