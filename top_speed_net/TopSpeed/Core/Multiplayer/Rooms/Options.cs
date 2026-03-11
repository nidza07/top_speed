using System;
using System.Collections.Generic;
using TopSpeed.Common;
using TopSpeed.Data;
using TopSpeed.Menu;
using TopSpeed.Protocol;

namespace TopSpeed.Core.Multiplayer
{
    internal sealed partial class MultiplayerCoordinator
    {
        private void OpenRoomOptionsMenu()
        {
            if (!_roomState.InRoom)
            {
                _speech.Speak("You are not currently inside a game room.");
                return;
            }

            if (!_roomState.IsHost)
            {
                _speech.Speak("Only the host can change game options.");
                return;
            }

            BeginRoomOptionsDraft();
            RebuildRoomOptionsMenu();
            _menu.Push(MultiplayerRoomOptionsMenuId);
        }

        private void BeginRoomOptionsDraft()
        {
            _roomOptionsDraftActive = true;
            _roomOptionsTrackRandom = false;
            _roomOptionsTrackName = string.IsNullOrWhiteSpace(_roomState.TrackName)
                ? TrackList.RaceTracks[0].Key
                : _roomState.TrackName;
            _roomOptionsLaps = _roomState.Laps > 0 ? _roomState.Laps : (byte)1;
            _roomOptionsPlayersToStart = _roomState.PlayersToStart >= 2 ? _roomState.PlayersToStart : (byte)2;
            if (_roomState.RoomType == GameRoomType.OneOnOne)
                _roomOptionsPlayersToStart = 2;
        }

        private void CancelRoomOptionsChanges()
        {
            _roomOptionsDraftActive = false;
            _roomOptionsTrackRandom = false;
        }

        private void ConfirmRoomOptionsChanges()
        {
            var session = SessionOrNull();
            if (session == null)
            {
                _speech.Speak("Not connected to a server.");
                return;
            }

            if (!_roomState.InRoom || !_roomState.IsHost || !_roomOptionsDraftActive)
            {
                _speech.Speak("Only the host can change game options.");
                return;
            }

            var appliedAny = false;
            var currentTrack = string.IsNullOrWhiteSpace(_roomState.TrackName) ? TrackList.RaceTracks[0].Key : _roomState.TrackName;
            if (!string.Equals(currentTrack, _roomOptionsTrackName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TrySend(session.SendRoomSetTrack(_roomOptionsTrackName), "track change request"))
                    return;
                appliedAny = true;
            }

            if (_roomState.Laps != _roomOptionsLaps)
            {
                if (!TrySend(session.SendRoomSetLaps(_roomOptionsLaps), "lap count change request"))
                    return;
                appliedAny = true;
            }

            if (_roomState.RoomType != GameRoomType.OneOnOne)
            {
                var playersToStart = _roomOptionsPlayersToStart < 2 ? (byte)2 : _roomOptionsPlayersToStart;
                if (_roomState.PlayersToStart != playersToStart)
                {
                    if (!TrySend(session.SendRoomSetPlayersToStart(playersToStart), "player count change request"))
                        return;
                    appliedAny = true;
                }
            }

            CancelRoomOptionsChanges();
            _menu.ShowRoot(MultiplayerRoomControlsMenuId);
            _speech.Speak(appliedAny ? "Room options updated." : "No option changes to apply.");
        }

        private string GetRoomOptionsTrackText()
        {
            if (!_roomOptionsDraftActive)
                BeginRoomOptionsDraft();

            if (_roomOptionsTrackRandom)
                return "Track, currently random chosen.";

            var trackName = TryGetTrackDisplay(_roomOptionsTrackName, out var display)
                ? display
                : _roomOptionsTrackName;
            return $"Track, currently {trackName}.";
        }

        private int GetRoomOptionsLapsIndex()
        {
            if (!_roomOptionsDraftActive)
                BeginRoomOptionsDraft();

            var laps = _roomOptionsLaps < 1 ? (byte)1 : _roomOptionsLaps;
            return Math.Max(0, Math.Min(LapCountOptions.Length - 1, laps - 1));
        }

        private void SetRoomOptionsLaps(byte laps)
        {
            if (!_roomOptionsDraftActive)
                BeginRoomOptionsDraft();

            if (laps < 1 || laps > 16)
                return;
            _roomOptionsLaps = laps;
        }

        private int GetRoomOptionsPlayersToStartIndex()
        {
            if (!_roomOptionsDraftActive)
                BeginRoomOptionsDraft();

            var playersToStart = _roomOptionsPlayersToStart < 2 ? (byte)2 : _roomOptionsPlayersToStart;
            return Math.Max(0, Math.Min(RoomCapacityOptions.Length - 1, playersToStart - 2));
        }

        private void SetRoomOptionsPlayersToStart(byte playersToStart)
        {
            if (!_roomOptionsDraftActive)
                BeginRoomOptionsDraft();

            if (_roomState.RoomType == GameRoomType.OneOnOne)
            {
                _roomOptionsPlayersToStart = 2;
                return;
            }

            if (playersToStart < 2 || playersToStart > ProtocolConstants.MaxRoomPlayersToStart)
                return;

            _roomOptionsPlayersToStart = playersToStart;
        }

        private void OpenRoomTrackTypeMenu()
        {
            if (!_roomOptionsDraftActive)
                BeginRoomOptionsDraft();

            RebuildRoomTrackTypeMenu();
            RebuildRoomTrackMenu(MultiplayerRoomTrackRaceMenuId, TrackCategory.RaceTrack);
            RebuildRoomTrackMenu(MultiplayerRoomTrackAdventureMenuId, TrackCategory.StreetAdventure);
            _menu.Push(MultiplayerRoomTrackTypeMenuId);
        }

        private void RebuildRoomTrackTypeMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Race track", MenuAction.None, nextMenuId: MultiplayerRoomTrackRaceMenuId),
                new MenuItem("Street adventure", MenuAction.None, nextMenuId: MultiplayerRoomTrackAdventureMenuId),
                new MenuItem("Random", MenuAction.None, onActivate: SelectRandomRoomTrackAny),
                new MenuItem("Go back", MenuAction.Back)
            };

            _menu.UpdateItems(MultiplayerRoomTrackTypeMenuId, items);
        }

        private void RebuildRoomTrackMenu(string menuId, TrackCategory category)
        {
            var items = new List<MenuItem>();
            var tracks = TrackList.GetTracks(category);
            for (var i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                items.Add(new MenuItem(track.Display, MenuAction.None, onActivate: () => SelectRoomTrack(track.Key, false)));
            }

            items.Add(new MenuItem("Random", MenuAction.None, onActivate: () => SelectRandomRoomTrackCategory(category)));
            items.Add(new MenuItem("Go back", MenuAction.Back));
            _menu.UpdateItems(menuId, items);
        }

        private void SelectRandomRoomTrackAny()
        {
            if (RoomTrackOptions.Length == 0)
            {
                SelectRoomTrack(TrackList.RaceTracks[0].Key, true);
                return;
            }

            var index = Algorithm.RandomInt(RoomTrackOptions.Length);
            SelectRoomTrack(RoomTrackOptions[index].Key, true);
        }

        private void SelectRandomRoomTrackCategory(TrackCategory category)
        {
            var tracks = TrackList.GetTracks(category);
            if (tracks.Count == 0)
            {
                SelectRandomRoomTrackAny();
                return;
            }

            var index = Algorithm.RandomInt(tracks.Count);
            SelectRoomTrack(tracks[index].Key, true);
        }

        private void SelectRoomTrack(string trackKey, bool randomChosen)
        {
            _roomOptionsTrackName = string.IsNullOrWhiteSpace(trackKey) ? TrackList.RaceTracks[0].Key : trackKey;
            _roomOptionsTrackRandom = randomChosen;
            ReturnToRoomOptionsMenu();
            _speech.Speak(GetRoomOptionsTrackText());
        }

        private void ReturnToRoomOptionsMenu()
        {
            if (string.Equals(_menu.CurrentId, MultiplayerRoomOptionsMenuId, StringComparison.Ordinal))
                return;

            while (_menu.CanPop && !string.Equals(_menu.CurrentId, MultiplayerRoomOptionsMenuId, StringComparison.Ordinal))
                _menu.PopToPrevious(announceTitle: false);

            if (!string.Equals(_menu.CurrentId, MultiplayerRoomOptionsMenuId, StringComparison.Ordinal))
                _menu.Push(MultiplayerRoomOptionsMenuId);
        }

        private static bool TryGetTrackDisplay(string trackKey, out string display)
        {
            display = string.Empty;
            if (string.IsNullOrWhiteSpace(trackKey))
                return false;

            for (var i = 0; i < RoomTrackOptions.Length; i++)
            {
                if (!string.Equals(RoomTrackOptions[i].Key, trackKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                display = RoomTrackOptions[i].Display;
                return true;
            }

            return false;
        }
    }
}
