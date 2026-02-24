using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib;
using TopSpeed.Bots;
using TopSpeed.Data;
using TopSpeed.Protocol;
using TopSpeed.Server.Protocol;

namespace TopSpeed.Server.Network
{
    internal sealed partial class RaceServer
    {
        private void UpdateBots(float deltaSeconds)
        {
            foreach (var room in _rooms.Values)
            {
                if (!room.RaceStarted)
                    continue;
                if (room.TrackData == null)
                    continue;

                var definitions = room.TrackData.Definitions;
                if (definitions == null || definitions.Length == 0)
                    continue;

                var laneHalfWidth = GetLaneHalfWidth(room);
                var roadModel = new RoadModel(definitions, laneHalfWidth);
                var lapDistance = roadModel.LapDistance;
                var raceDistance = GetRaceDistance(room);
                if (lapDistance <= 0f || raceDistance <= 0f)
                    continue;

                foreach (var bot in room.Bots)
                {
                    if (bot.BackfirePulseSeconds > 0f)
                    {
                        bot.BackfirePulseSeconds -= deltaSeconds;
                        if (bot.BackfirePulseSeconds < 0f)
                            bot.BackfirePulseSeconds = 0f;
                    }

                    if (bot.HornSecondsRemaining > 0f)
                    {
                        bot.HornSecondsRemaining -= deltaSeconds;
                        if (bot.HornSecondsRemaining <= 0f)
                        {
                            bot.HornSecondsRemaining = 0f;
                            bot.Horning = false;
                        }
                    }

                    if (bot.State == PlayerState.Finished || bot.State == PlayerState.NotReady)
                        continue;

                    if (bot.State == PlayerState.AwaitingStart)
                    {
                        if (bot.StartDelaySeconds > 0f)
                        {
                            bot.StartDelaySeconds -= deltaSeconds;
                            if (bot.StartDelaySeconds > 0f)
                                continue;
                            bot.StartDelaySeconds = 0f;
                        }

                        if (bot.EngineStartSecondsRemaining <= 0f)
                        {
                            bot.EngineStartSecondsRemaining = BotRaceRules.DefaultBotEngineStartSeconds;
                            bot.SpeedKph = 0f;
                            bot.EngineFrequency = bot.AudioProfile.IdleFrequency;
                            continue;
                        }

                        bot.EngineStartSecondsRemaining -= deltaSeconds;
                        if (bot.EngineStartSecondsRemaining > 0f)
                        {
                            bot.EngineFrequency = bot.AudioProfile.IdleFrequency;
                            continue;
                        }

                        bot.EngineStartSecondsRemaining = 0f;
                        bot.State = PlayerState.Racing;
                        bot.RacePhase = BotRacePhase.Normal;
                        bot.CrashRecoverySeconds = 0f;
                        bot.SpeedKph = 0f;
                        bot.EngineFrequency = bot.AudioProfile.IdleFrequency;
                        bot.BackfireArmed = true;
                        _botStartEvents++;
                        _logger.Debug($"Bot started racing: room={room.Id}, bot={bot.Id}, number={bot.PlayerNumber}.");
                    }

                    if (bot.State != PlayerState.Racing)
                        continue;

                    if (bot.RacePhase == BotRacePhase.Crashing)
                    {
                        bot.CrashRecoverySeconds -= deltaSeconds;
                        bot.SpeedKph = 0f;
                        var crashState = bot.PhysicsState;
                        crashState.SpeedKph = 0f;
                        crashState.Gear = 1;
                        crashState.AutoShiftCooldownSeconds = 0f;
                        bot.PhysicsState = crashState;
                        bot.EngineFrequency = bot.AudioProfile.IdleFrequency;
                        bot.Horning = false;
                        bot.HornSecondsRemaining = 0f;
                        bot.BackfirePulseSeconds = 0f;
                        bot.BackfireArmed = true;
                        if (bot.CrashRecoverySeconds > 0f)
                            continue;

                        bot.CrashRecoverySeconds = 0f;
                        bot.RacePhase = BotRacePhase.Restarting;
                        bot.StartDelaySeconds = BotRaceRules.DefaultBotRestartDelaySeconds;
                        bot.EngineStartSecondsRemaining = 0f;
                        _botRestartEvents++;
                        _logger.Debug($"Bot restarting after crash: room={room.Id}, bot={bot.Id}, number={bot.PlayerNumber}, restartDelay={BotRaceRules.DefaultBotRestartDelaySeconds:0.00}s, startDelay={BotRaceRules.DefaultBotEngineStartSeconds:0.00}s.");
                        continue;
                    }

                    if (bot.RacePhase == BotRacePhase.Restarting)
                    {
                        bot.SpeedKph = 0f;
                        var restartState = bot.PhysicsState;
                        restartState.SpeedKph = 0f;
                        restartState.Gear = 1;
                        restartState.AutoShiftCooldownSeconds = 0f;
                        bot.PhysicsState = restartState;
                        bot.EngineFrequency = bot.AudioProfile.IdleFrequency;
                        bot.Horning = false;
                        bot.HornSecondsRemaining = 0f;
                        bot.BackfirePulseSeconds = 0f;
                        bot.BackfireArmed = true;
                        if (bot.StartDelaySeconds > 0f)
                        {
                            bot.StartDelaySeconds -= deltaSeconds;
                            if (bot.StartDelaySeconds > 0f)
                                continue;
                            bot.StartDelaySeconds = 0f;
                        }

                        if (bot.EngineStartSecondsRemaining <= 0f)
                        {
                            bot.EngineStartSecondsRemaining = BotRaceRules.DefaultBotEngineStartSeconds;
                            continue;
                        }

                        bot.EngineStartSecondsRemaining -= deltaSeconds;
                        if (bot.EngineStartSecondsRemaining > 0f)
                            continue;

                        bot.EngineStartSecondsRemaining = 0f;
                        bot.RacePhase = BotRacePhase.Normal;
                        _botResumeEvents++;
                        _logger.Debug($"Bot recovered and resumed: room={room.Id}, bot={bot.Id}, number={bot.PlayerNumber}.");
                        continue;
                    }

                    var currentRoad = roadModel.At(bot.PositionY);
                    var nextRoad = roadModel.At(bot.PositionY + BotAiLookaheadMeters);
                    var currentLaneHalfWidth = Math.Max(0.1f, Math.Abs(currentRoad.Right - currentRoad.Left) * 0.5f);
                    var relPos = BotRaceRules.CalculateRelativeLanePosition(bot.PositionX, currentRoad.Left, currentLaneHalfWidth);
                    relPos = Math.Max(0f, Math.Min(1f, relPos));
                    var controlRandom = (bot.AddedOrder * 37) % 100;
                    BotSharedModel.GetControlInputs((int)bot.Difficulty, controlRandom, currentRoad.Type, nextRoad.Type, relPos, out var throttle, out var steering);

                    var physicsState = bot.PhysicsState;
                    physicsState.PositionX = bot.PositionX;
                    physicsState.PositionY = bot.PositionY;
                    physicsState.SpeedKph = bot.SpeedKph;
                    if (physicsState.Gear <= 0)
                        physicsState.Gear = 1;

                    var physicsInput = new BotPhysicsInput(
                        deltaSeconds,
                        currentRoad.Surface,
                        (int)Math.Round(throttle),
                        brake: 0,
                        steering: (int)Math.Round(steering));
                    BotPhysics.Step(bot.PhysicsConfig, ref physicsState, in physicsInput);

                    bot.PhysicsState = physicsState;
                    bot.PositionX = physicsState.PositionX;
                    bot.PositionY = physicsState.PositionY;
                    bot.SpeedKph = physicsState.SpeedKph;
                    bot.EngineFrequency = CalculateBotEngineFrequency(bot, out var inShiftBand);
                    if (inShiftBand)
                    {
                        if (bot.BackfireArmed && _random.Next(5) == 0)
                        {
                            bot.BackfirePulseSeconds = BotBackfirePulseSeconds;
                            bot.BackfireArmed = false;
                        }
                    }
                    else
                    {
                        bot.BackfireArmed = true;
                    }
                    TryStartBotHorn(room, bot, raceDistance);

                    var evalRoad = roadModel.At(bot.PositionY);
                    var evalLaneHalfWidth = Math.Max(0.1f, Math.Abs(evalRoad.Right - evalRoad.Left) * 0.5f);
                    var evalRelPos = BotRaceRules.CalculateRelativeLanePosition(bot.PositionX, evalRoad.Left, evalLaneHalfWidth);
                    if (BotRaceRules.IsOutsideRoad(evalRelPos))
                    {
                        var center = BotRaceRules.RoadCenter(evalRoad.Left, evalRoad.Right);
                        var fullCrash = BotRaceRules.IsFullCrash(physicsState.Gear, bot.SpeedKph);
                        if (fullCrash)
                        {
                            physicsState.PositionX = center;
                            physicsState.SpeedKph = 0f;
                            physicsState.Gear = 1;
                            physicsState.AutoShiftCooldownSeconds = 0f;
                            bot.PhysicsState = physicsState;
                            bot.PositionX = center;
                            bot.SpeedKph = 0f;
                            bot.EngineStartSecondsRemaining = 0f;
                            bot.StartDelaySeconds = 0f;
                            bot.RacePhase = BotRacePhase.Crashing;
                            bot.CrashRecoverySeconds = BotRaceRules.DefaultBotCrashRecoverySeconds;
                            bot.EngineFrequency = bot.AudioProfile.IdleFrequency;
                            bot.Horning = false;
                            bot.HornSecondsRemaining = 0f;
                            bot.BackfirePulseSeconds = 0f;
                            bot.BackfireArmed = true;
                            _botCrashEvents++;
                            _logger.Debug($"Bot crashed: room={room.Id}, bot={bot.Id}, number={bot.PlayerNumber}, y={bot.PositionY:0.0}.");
                            SendToRoomOnStream(room, PacketSerializer.WritePlayer(Command.PlayerCrashed, bot.Id, bot.PlayerNumber), PacketStream.RaceEvent);
                            continue;
                        }

                        physicsState.PositionX = center;
                        physicsState.SpeedKph /= 4f;
                        bot.PhysicsState = physicsState;
                        bot.PositionX = center;
                        bot.SpeedKph = Math.Max(0f, physicsState.SpeedKph);
                    }

                    if (bot.PositionY < raceDistance)
                        continue;

                    bot.PositionY = raceDistance;
                    bot.SpeedKph = 0f;
                    bot.State = PlayerState.Finished;
                    bot.EngineFrequency = bot.AudioProfile.IdleFrequency;
                    bot.Horning = false;
                    bot.HornSecondsRemaining = 0f;
                    bot.BackfirePulseSeconds = 0f;
                    bot.BackfireArmed = true;
                    if (!room.RaceResults.Contains(bot.PlayerNumber))
                        room.RaceResults.Add(bot.PlayerNumber);

                    SendToRoomOnStream(room, PacketSerializer.WritePlayer(Command.PlayerFinished, bot.Id, bot.PlayerNumber), PacketStream.RaceEvent);
                    _botFinishEvents++;
                    _logger.Debug($"Bot finished: room={room.Id}, bot={bot.Id}, number={bot.PlayerNumber}, place={room.RaceResults.Count}.");
                }

                if (CountActiveRaceParticipants(room) == 0)
                    StopRace(room);
            }
        }

        private static float GetLapDistance(RaceRoom room)
        {
            if (room.TrackData == null || room.TrackData.Definitions == null || room.TrackData.Definitions.Length == 0)
                return 0f;

            var lapDistance = 0f;
            foreach (var definition in room.TrackData.Definitions)
                lapDistance += Math.Max(1f, definition.Length);
            return lapDistance;
        }

        private float GetLaneHalfWidth(RaceRoom room)
        {
            if (room.TrackData == null || room.TrackData.Definitions == null || room.TrackData.Definitions.Length == 0)
                return RoadModel.DefaultLaneHalfWidth;

            var model = new RoadModel(room.TrackData.Definitions, RoadModel.DefaultLaneHalfWidth);
            var startRoad = model.At(BotRaceRules.StartLineY);
            var laneHalfWidth = Math.Abs(startRoad.Right - startRoad.Left) * 0.5f;
            return laneHalfWidth > 0f ? laneHalfWidth : RoadModel.DefaultLaneHalfWidth;
        }

        private float GetStartRowSpacing(RaceRoom room)
        {
            var maxLength = 4.5f;

            foreach (var playerId in room.PlayerIds)
            {
                if (_players.TryGetValue(playerId, out var player))
                    maxLength = Math.Max(maxLength, player.LengthM);
            }

            for (var i = 0; i < room.Bots.Count; i++)
                maxLength = Math.Max(maxLength, room.Bots[i].LengthM);

            return BotRaceRules.CalculateStartRowSpacing(maxLength);
        }

        private static float CalculateStartX(int gridIndex, float vehicleWidth, float laneHalfWidth)
        {
            return BotRaceRules.CalculateStartX(gridIndex, vehicleWidth, laneHalfWidth);
        }

        private static float CalculateStartY(int gridIndex, float rowSpacing)
        {
            return BotRaceRules.CalculateStartY(gridIndex, rowSpacing);
        }

        private static PacketPlayerData ToBotPacket(RoomBot bot)
        {
            return new PacketPlayerData
            {
                PlayerId = bot.Id,
                PlayerNumber = bot.PlayerNumber,
                Car = bot.Car,
                RaceData = new PlayerRaceData
                {
                    PositionX = bot.PositionX,
                    PositionY = bot.PositionY,
                    Speed = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (int)Math.Round(bot.SpeedKph))),
                    Frequency = bot.EngineFrequency > 0 ? bot.EngineFrequency : bot.AudioProfile.IdleFrequency
                },
                State = bot.State,
                EngineRunning = (bot.State == PlayerState.Racing && bot.RacePhase == BotRacePhase.Normal)
                    || bot.EngineStartSecondsRemaining > 0f,
                Braking = false,
                Horning = bot.Horning,
                Backfiring = bot.BackfirePulseSeconds > 0f,
                MediaLoaded = false,
                MediaPlaying = false,
                MediaId = 0
            };
        }

        private static float GetRaceDistance(RaceRoom room)
        {
            var lapDistance = GetLapDistance(room);
            if (lapDistance <= 0f)
                return 0f;

            var laps = room.Laps > 0 ? room.Laps : (byte)1;
            return lapDistance * laps;
        }

        private void TryStartBotHorn(RaceRoom room, RoomBot bot, float raceDistance)
        {
            if (bot.Horning || bot.HornSecondsRemaining > 0f)
                return;
            if (raceDistance <= 0f)
                return;

            foreach (var id in room.PlayerIds)
            {
                if (!_players.TryGetValue(id, out var player))
                    continue;
                if (player.State != PlayerState.Racing && player.State != PlayerState.Finished)
                    continue;

                var delta = bot.PositionY - player.PositionY;
                if (delta < -BotHornMinDistanceMeters)
                {
                    if (_random.Next(2500) == 0)
                        TriggerBotHorn(bot, "overtake", 0.2f);
                    return;
                }
            }
        }

        private void TriggerBotHorn(RoomBot bot, string reason, float minDurationSeconds = 0.2f)
        {
            var duration = minDurationSeconds + (_random.Next(80) / 80.0f);
            if (duration <= bot.HornSecondsRemaining)
                return;

            bot.Horning = true;
            bot.HornSecondsRemaining = duration;
            if (string.Equals(reason, "overtake", StringComparison.Ordinal))
                _botHornOvertakeEvents++;
            else if (string.Equals(reason, "bump", StringComparison.Ordinal))
                _botHornBumpEvents++;

            _logger.Debug($"Bot horn triggered: bot={bot.Id}, number={bot.PlayerNumber}, reason={reason}, duration={duration:0.00}s.");
        }

        private static int CalculateBotEngineFrequency(RoomBot bot, out bool inShiftBand)
        {
            inShiftBand = false;
            var speedKph = Math.Max(0f, bot.SpeedKph);
            var config = bot.PhysicsConfig;
            var profile = bot.AudioProfile;

            var gearForSound = GetGearForSpeed(config, speedKph);
            if (!TryGetGearBand(config, gearForSound, out var gearMinKph, out var gearRangeKph))
                return profile.IdleFrequency;

            int frequency;
            if (gearForSound <= 1)
            {
                var gearSpeed = gearRangeKph <= 0f ? 0f : Math.Min(1.0f, speedKph / gearRangeKph);
                frequency = (int)(gearSpeed * (profile.TopFrequency - profile.IdleFrequency)) + profile.IdleFrequency;
            }
            else
            {
                var gearSpeed = (speedKph - gearMinKph) / gearRangeKph;
                if (gearSpeed < 0.07f)
                {
                    inShiftBand = true;
                    frequency = (int)(((0.07f - gearSpeed) / 0.07f) * (profile.TopFrequency - profile.ShiftFrequency) + profile.ShiftFrequency);
                }
                else
                {
                    if (gearSpeed > 1.0f)
                        gearSpeed = 1.0f;
                    frequency = (int)(gearSpeed * (profile.TopFrequency - profile.ShiftFrequency) + profile.ShiftFrequency);
                }
            }

            var minFrequency = Math.Max(1000, profile.IdleFrequency / 2);
            var maxFrequency = Math.Max(profile.TopFrequency, profile.TopFrequency * 2);
            if (frequency < minFrequency)
                frequency = minFrequency;
            if (frequency > maxFrequency)
                frequency = maxFrequency;
            return frequency;
        }

        private static int GetGearForSpeed(BotPhysicsConfig config, float speedKph)
        {
            var speedMps = Math.Max(0f, speedKph / 3.6f);
            var topSpeedMps = config.TopSpeedKph / 3.6f;
            var autoShiftRpm = config.IdleRpm + ((config.RevLimiter - config.IdleRpm) * 0.92f);
            for (var gear = 1; gear <= config.Gears; gear++)
            {
                var rpm = gear == config.Gears ? config.RevLimiter : autoShiftRpm;
                var gearMax = Math.Min(SpeedMpsFromRpm(config, rpm, gear), topSpeedMps);
                if (speedMps <= gearMax + 0.01f)
                    return gear;
            }

            return config.Gears;
        }

        private static bool TryGetGearBand(BotPhysicsConfig config, int gear, out float minSpeedKph, out float rangeKph)
        {
            minSpeedKph = 0f;
            rangeKph = 0f;

            if (config.Gears <= 0)
                return false;

            var clampedGear = gear;
            if (clampedGear < 1)
                clampedGear = 1;
            if (clampedGear > config.Gears)
                clampedGear = config.Gears;

            var maxSpeedMps = SpeedMpsFromRpm(config, config.RevLimiter, clampedGear);
            var shiftRpm = config.IdleRpm + ((config.RevLimiter - config.IdleRpm) * 0.35f);
            var minSpeedMps = clampedGear == 1 ? 0f : SpeedMpsFromRpm(config, shiftRpm, clampedGear);
            minSpeedKph = minSpeedMps * 3.6f;
            rangeKph = Math.Max(0.1f, (maxSpeedMps - minSpeedMps) * 3.6f);
            return true;
        }

        private static float SpeedMpsFromRpm(BotPhysicsConfig config, float rpm, int gear)
        {
            var ratio = config.GetGearRatio(gear) * config.FinalDriveRatio;
            if (ratio <= 0f)
                return 0f;

            var tireCircumference = config.WheelRadiusM * 2f * (float)Math.PI;
            return (rpm / ratio) * (tireCircumference / 60f);
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
                var activePairs = new HashSet<ulong>();

                for (var i = 0; i < racers.Count; i++)
                {
                    for (var j = i + 1; j < racers.Count; j++)
                    {
                        var player = racers[i];
                        var other = racers[j];
                        var xThreshold = (player.WidthM + other.WidthM) * 0.5f;
                        var yThreshold = (player.LengthM + other.LengthM) * 0.5f;
                        var dx = player.PositionX - other.PositionX;
                        var dy = player.PositionY - other.PositionY;
                        if (Math.Abs(dx) >= xThreshold || Math.Abs(dy) >= yThreshold)
                            continue;

                        var pairKey = MakePairKey(player.Id, other.Id);
                        activePairs.Add(pairKey);
                        if (room.ActiveBumpPairs.Contains(pairKey))
                            continue;

                        SendStream(player, PacketSerializer.WritePlayerBumped(new PacketPlayerBumped
                        {
                            PlayerId = player.Id,
                            PlayerNumber = player.PlayerNumber,
                            BumpX = dx,
                            BumpY = dy,
                            BumpSpeed = (ushort)Math.Max(0, player.Speed - other.Speed)
                        }), PacketStream.RaceEvent, PacketDeliveryKind.Sequenced);

                        SendStream(other, PacketSerializer.WritePlayerBumped(new PacketPlayerBumped
                        {
                            PlayerId = other.Id,
                            PlayerNumber = other.PlayerNumber,
                            BumpX = -dx,
                            BumpY = -dy,
                            BumpSpeed = (ushort)Math.Max(0, other.Speed - player.Speed)
                        }), PacketStream.RaceEvent, PacketDeliveryKind.Sequenced);
                        _bumpEventsHumanHuman++;
                    }
                }

                foreach (var player in racers)
                {
                    foreach (var bot in botRacers)
                    {
                        var xThreshold = (player.WidthM + bot.WidthM) * 0.5f;
                        var yThreshold = (player.LengthM + bot.LengthM) * 0.5f;
                        var dx = player.PositionX - bot.PositionX;
                        var dy = player.PositionY - bot.PositionY;
                        if (Math.Abs(dx) >= xThreshold || Math.Abs(dy) >= yThreshold)
                            continue;

                        var pairKey = MakePairKey(player.Id, bot.Id);
                        activePairs.Add(pairKey);
                        if (room.ActiveBumpPairs.Contains(pairKey))
                            continue;

                        var botSpeed = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (int)Math.Round(bot.SpeedKph)));
                        SendStream(player, PacketSerializer.WritePlayerBumped(new PacketPlayerBumped
                        {
                            PlayerId = player.Id,
                            PlayerNumber = player.PlayerNumber,
                            BumpX = dx,
                            BumpY = dy,
                            BumpSpeed = (ushort)Math.Max(0, player.Speed - botSpeed)
                        }), PacketStream.RaceEvent, PacketDeliveryKind.Sequenced);

                        bot.PositionX -= 2f * dx;
                        bot.PositionY -= dy;
                        if (bot.PositionY < 0f)
                            bot.PositionY = 0f;
                        bot.SpeedKph = Math.Max(0f, bot.SpeedKph * 0.8f);
                        var state = bot.PhysicsState;
                        state.PositionX = bot.PositionX;
                        state.PositionY = bot.PositionY;
                        state.SpeedKph = bot.SpeedKph;
                        bot.PhysicsState = state;
                        TriggerBotHorn(bot, "bump", 0.2f);
                        _bumpEventsHumanBot++;
                    }
                }

                room.ActiveBumpPairs.RemoveWhere(key => !activePairs.Contains(key));
                foreach (var pairKey in activePairs)
                    room.ActiveBumpPairs.Add(pairKey);
            }
        }
    }
}
