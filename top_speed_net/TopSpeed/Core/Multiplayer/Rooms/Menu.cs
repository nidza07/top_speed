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
        private void RebuildLobbyMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Create a new game room", MenuAction.None, onActivate: OpenCreateRoomMenu),
                new MenuItem("Join an existing game", MenuAction.None, onActivate: OpenRoomBrowser),
                new MenuItem("Options", MenuAction.None, nextMenuId: "options_main"),
                new MenuItem("Disconnect from server", MenuAction.None, flags: MenuItemFlags.Close)
            };

            _menu.UpdateItems(MultiplayerLobbyMenuId, items);
        }

        private void RebuildRoomControlsMenu()
        {
            var items = new List<MenuItem>();
            if (!_roomState.InRoom)
            {
                items.Add(new MenuItem("You are not currently inside a game room.", MenuAction.None));
                items.Add(new MenuItem("Return to multiplayer lobby", MenuAction.None, onActivate: () => _menu.ShowRoot(MultiplayerLobbyMenuId)));
                _menu.UpdateItems(MultiplayerRoomControlsMenuId, items);
                return;
            }

            if (_roomState.IsHost)
                items.Add(new MenuItem("Start this game now", MenuAction.None, onActivate: StartGame));
            if (_roomState.IsHost)
                items.Add(new MenuItem("Change game options", MenuAction.None, onActivate: OpenRoomOptionsMenu));
            if (_roomState.IsHost && _roomState.RoomType == GameRoomType.BotsRace)
                items.Add(new MenuItem("Add a bot to this game room", MenuAction.None, onActivate: AddBotToRoom));
            if (_roomState.IsHost && _roomState.RoomType == GameRoomType.BotsRace)
                items.Add(new MenuItem("Remove the last bot that was added", MenuAction.None, onActivate: RemoveLastBotFromRoom));
            items.Add(new MenuItem("Who is currently present in this game room", MenuAction.None, onActivate: OpenRoomPlayersMenu));
            items.Add(new MenuItem("Leave this game room", MenuAction.None, flags: MenuItemFlags.Close));
            _menu.UpdateItems(MultiplayerRoomControlsMenuId, items);
        }

        private void RebuildRoomOptionsMenu()
        {
            var items = new List<MenuItem>();
            if (!_roomState.InRoom)
            {
                items.Add(new MenuItem("You are not currently inside a game room.", MenuAction.None));
                items.Add(new MenuItem("Return to room controls", MenuAction.Back));
                _menu.UpdateItems(MultiplayerRoomOptionsMenuId, items);
                return;
            }

            if (!_roomState.IsHost)
            {
                items.Add(new MenuItem("Only the host can change game options.", MenuAction.None));
                items.Add(new MenuItem("Return to room controls", MenuAction.Back));
                _menu.UpdateItems(MultiplayerRoomOptionsMenuId, items);
                return;
            }

            items.Add(new MenuItem(
                () => GetRoomOptionsTrackText(),
                MenuAction.None,
                onActivate: OpenRoomTrackTypeMenu,
                hint: "Press ENTER to choose race tracks, street adventure tracks, or a random track."));

            items.Add(new RadioButton(
                "Number of laps",
                LapCountOptions,
                GetRoomOptionsLapsIndex,
                value => SetRoomOptionsLaps((byte)(value + 1)),
                hint: "Choose the number of laps for this room. Use LEFT or RIGHT to change."));

            var maxPlayersItem = new RadioButton(
                "Maximum players allowed in this room",
                RoomCapacityOptions,
                GetRoomOptionsPlayersToStartIndex,
                value => SetRoomOptionsPlayersToStart((byte)(value + 2)),
                hint: "Select the player capacity for this room. The host can start with fewer players. Use LEFT or RIGHT to change.")
            {
                Hidden = _roomState.RoomType == GameRoomType.OneOnOne
            };
            items.Add(maxPlayersItem);

            items.Add(new MenuItem("Confirm game options", MenuAction.None, onActivate: ConfirmRoomOptionsChanges));
            items.Add(new MenuItem("Cancel and discard changes", MenuAction.Back, onActivate: CancelRoomOptionsChanges));
            var preserveSelection = string.Equals(_menu.CurrentId, MultiplayerRoomOptionsMenuId, StringComparison.Ordinal);
            _menu.UpdateItems(MultiplayerRoomOptionsMenuId, items, preserveSelection);
        }

        private void RebuildLoadoutVehicleMenu()
        {
            var items = new List<MenuItem>();
            for (var i = 0; i < VehicleCatalog.VehicleCount; i++)
            {
                var vehicleIndex = i;
                var vehicleName = VehicleCatalog.Vehicles[i].Name;
                items.Add(new MenuItem(vehicleName, MenuAction.None, nextMenuId: MultiplayerLoadoutTransmissionMenuId, onActivate: () => _pendingLoadoutVehicleIndex = vehicleIndex));
            }

            items.Add(new MenuItem("Random vehicle", MenuAction.None, nextMenuId: MultiplayerLoadoutTransmissionMenuId, onActivate: () => _pendingLoadoutVehicleIndex = Algorithm.RandomInt(VehicleCatalog.VehicleCount)));
            _menu.UpdateItems(MultiplayerLoadoutVehicleMenuId, items);
        }

        private void RebuildLoadoutTransmissionMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Automatic transmission", MenuAction.None, onActivate: () => SubmitLoadoutReady(true)),
                new MenuItem("Manual transmission", MenuAction.None, onActivate: () => SubmitLoadoutReady(false)),
                new MenuItem("Random transmission mode", MenuAction.None, onActivate: () => SubmitLoadoutReady(Algorithm.RandomInt(2) == 0)),
                new MenuItem("Go back to vehicle selection", MenuAction.Back)
            };
            _menu.UpdateItems(MultiplayerLoadoutTransmissionMenuId, items);
        }

        private void RebuildRoomPlayersMenu()
        {
            var items = new List<MenuItem>();
            if (!_roomState.InRoom)
            {
                items.Add(new MenuItem("You are not currently inside a game room.", MenuAction.None));
                items.Add(new MenuItem("Go back", MenuAction.Back));
                _menu.UpdateItems(MultiplayerRoomPlayersMenuId, items);
                return;
            }

            var players = _roomState.Players ?? Array.Empty<PacketRoomPlayer>();
            if (players.Length == 0)
            {
                items.Add(new MenuItem("No players are currently in this game room.", MenuAction.None));
            }
            else
            {
                foreach (var player in players)
                {
                    var name = string.IsNullOrWhiteSpace(player.Name) ? $"Player {player.PlayerNumber + 1}" : player.Name;
                    var label = player.PlayerId == _roomState.HostPlayerId ? $"{name}, host" : name;
                    items.Add(new MenuItem(label, MenuAction.None));
                }
            }

            items.Add(new MenuItem("Go back", MenuAction.Back));
            var preserveSelection = string.Equals(_menu.CurrentId, MultiplayerRoomPlayersMenuId, StringComparison.Ordinal);
            _menu.UpdateItems(MultiplayerRoomPlayersMenuId, items, preserveSelection);
        }

        private void OpenRoomPlayersMenu()
        {
            RebuildRoomPlayersMenu();
            _menu.Push(MultiplayerRoomPlayersMenuId);
        }
    }
}
