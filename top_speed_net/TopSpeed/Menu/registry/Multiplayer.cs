using System.Collections.Generic;
using TopSpeed.Speech;

namespace TopSpeed.Menu
{
    internal sealed partial class MenuRegistry
    {
        private MenuScreen BuildMultiplayerMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Join a game on the local network", MenuAction.None, onActivate: _server.StartServerDiscovery),
                new MenuItem("Manage saved servers", MenuAction.None, onActivate: _server.OpenSavedServersManager),
                new MenuItem("Enter the IP address or domain manually", MenuAction.None, onActivate: _server.BeginManualServerEntry),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer", items);
        }

        private MenuScreen BuildMultiplayerServersMenu()
        {
            var items = new List<MenuItem>
            {
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_servers", items, "Available servers");
        }

        private MenuScreen BuildMultiplayerLobbyMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Create a new game", MenuAction.None, onActivate: _ui.SpeakNotImplemented),
                new MenuItem("Join an existing game", MenuAction.None, onActivate: _ui.SpeakNotImplemented),
                new MenuItem("Options", MenuAction.None, nextMenuId: "options_main"),
                new MenuItem("Disconnect", MenuAction.None, flags: MenuItemFlags.Close)
            };
            var menu = _menu.CreateMenu("multiplayer_lobby", items, "Multiplayer lobby");
            menu.SetScreens(new[]
            {
                new MenuView("lobby_main", items, "Multiplayer lobby", titleSpeakFlag: SpeechService.SpeakFlag.None),
                _sharedLobbyChatScreen
            }, "lobby_main");
            return menu;
        }

        private MenuScreen BuildMultiplayerSavedServersMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Add a new server", MenuAction.None),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_saved_servers", items, string.Empty);
        }

        private MenuScreen BuildMultiplayerSavedServerFormMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Server form is loading", MenuAction.None),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_saved_server_form", items, "Server details");
        }

        private MenuScreen BuildMultiplayerRoomsMenu()
        {
            var items = new List<MenuItem>
            {
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_rooms", items, "Available game rooms");
        }

        private MenuScreen BuildMultiplayerCreateRoomMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Create room controls are loading", MenuAction.None),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_create_room", items);
        }

        private MenuScreen BuildMultiplayerRoomControlsMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Join a game room first", MenuAction.None),
                BackItem()
            };
            var menu = _menu.CreateMenu("multiplayer_room_controls", items, "Room controls");
            menu.SetScreens(new[]
            {
                new MenuView("room_controls_main", items, "Room controls", titleSpeakFlag: SpeechService.SpeakFlag.None),
                _sharedLobbyChatScreen
            }, "room_controls_main");
            return menu;
        }

        private MenuScreen BuildMultiplayerRoomPlayersMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Join a game room first", MenuAction.None),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_room_players", items, "Players in room");
        }

        private MenuScreen BuildMultiplayerRoomOptionsMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Join a game room first", MenuAction.None),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_room_options", items, string.Empty);
        }

        private MenuScreen BuildMultiplayerRoomTrackTypeMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Race track", MenuAction.None),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_room_track_type", items, "Choose track type");
        }

        private MenuScreen BuildMultiplayerRoomTrackRaceMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Race tracks are loading", MenuAction.None),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_room_tracks_race", items, "Select a track");
        }

        private MenuScreen BuildMultiplayerRoomTrackAdventureMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Adventure tracks are loading", MenuAction.None),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_room_tracks_adventure", items, "Select a track");
        }

        private MenuScreen BuildMultiplayerLoadoutVehicleMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Vehicle selection is loading", MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_loadout_vehicle", items, "Choose your vehicle");
        }

        private MenuScreen BuildMultiplayerLoadoutTransmissionMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem("Transmission selection is loading", MenuAction.None),
                BackItem()
            };
            return _menu.CreateMenu("multiplayer_loadout_transmission", items, "Choose your transmission mode");
        }
    }
}
