using System.Collections.Generic;
using TopSpeed.Speech;

using TopSpeed.Localization;
namespace TopSpeed.Menu
{
    internal sealed partial class MenuRegistry
    {
        private MenuScreen BuildMultiplayerMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Join a game on the local network"), MenuAction.None, onActivate: _server.StartServerDiscovery),
                new MenuItem(LocalizationService.Mark("Manage saved servers"), MenuAction.None, onActivate: _server.OpenSavedServersManager),
                new MenuItem(LocalizationService.Mark("Enter the IP address or domain manually"), MenuAction.None, onActivate: _server.BeginManualServerEntry)
            };
            return _menu.CreateMenu("multiplayer", items, spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerServersMenu()
        {
            return _menu.CreateMenu("multiplayer_servers", new MenuItem[0], LocalizationService.Mark("Available servers"), spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerLobbyMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Create a new game"), MenuAction.None, onActivate: _ui.SpeakNotImplemented),
                new MenuItem(LocalizationService.Mark("Join an existing game"), MenuAction.None, onActivate: _ui.SpeakNotImplemented),
                new MenuItem(LocalizationService.Mark("Options"), MenuAction.None, nextMenuId: "options_main"),
                new MenuItem(LocalizationService.Mark("Disconnect"), MenuAction.None, flags: MenuItemFlags.Close)
            };
            var menu = _menu.CreateMenu("multiplayer_lobby", items, LocalizationService.Mark("Multiplayer lobby"));
            menu.SetScreens(new[]
            {
                new MenuView("lobby_main", items, LocalizationService.Mark("Multiplayer lobby"), spec: ScreenSpec.Silent),
                _sharedLobbyChatScreen
            }, "lobby_main");
            return menu;
        }

        private MenuScreen BuildMultiplayerSavedServersMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Add a new server"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_saved_servers", items, string.Empty, spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerSavedServerFormMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Server form is loading"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_saved_server_form", items, LocalizationService.Mark("Server details"), spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerRoomsMenu()
        {
            return _menu.CreateMenu("multiplayer_rooms", new MenuItem[0], LocalizationService.Mark("Available game rooms"), spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerCreateRoomMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Create room controls are loading"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_create_room", items);
        }

        private MenuScreen BuildMultiplayerRoomControlsMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Join a game room first"), MenuAction.None)
            };
            var menu = _menu.CreateMenu("multiplayer_room_controls", items, LocalizationService.Mark("Room controls"));
            menu.SetScreens(new[]
            {
                new MenuView("room_controls_main", items, LocalizationService.Mark("Room controls"), spec: ScreenSpec.BackSilent),
                _sharedLobbyChatScreen
            }, "room_controls_main");
            return menu;
        }

        private MenuScreen BuildMultiplayerRoomPlayersMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Join a game room first"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_room_players", items, LocalizationService.Mark("Players in room"), spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerOnlinePlayersMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("No players are currently connected."), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_online_players", items, LocalizationService.Mark("Online players"), spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerRoomOptionsMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Join a game room first"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_room_options", items, string.Empty, spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerRoomGameRulesMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Game rules are loading"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_room_game_rules", items, string.Empty, spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerRoomTrackTypeMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Race track"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_room_track_type", items, LocalizationService.Mark("Choose track type"), spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerRoomTrackRaceMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Race tracks are loading"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_room_tracks_race", items, LocalizationService.Mark("Select a track"), spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerRoomTrackAdventureMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Adventure tracks are loading"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_room_tracks_adventure", items, LocalizationService.Mark("Select a track"), spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerLoadoutVehicleMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Vehicle selection is loading"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_loadout_vehicle", items, LocalizationService.Mark("Choose your vehicle"), spec: ScreenSpec.Back);
        }

        private MenuScreen BuildMultiplayerLoadoutTransmissionMenu()
        {
            var items = new List<MenuItem>
            {
                new MenuItem(LocalizationService.Mark("Transmission selection is loading"), MenuAction.None)
            };
            return _menu.CreateMenu("multiplayer_loadout_transmission", items, LocalizationService.Mark("Choose your transmission mode"), spec: ScreenSpec.Back);
        }
    }
}





