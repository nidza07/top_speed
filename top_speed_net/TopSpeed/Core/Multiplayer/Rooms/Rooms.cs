using System;
using TopSpeed.Menu;

namespace TopSpeed.Core.Multiplayer
{
    internal sealed partial class MultiplayerCoordinator
    {
        public bool IsInRoom => _roomState.InRoom;

        private const string MultiplayerPingShortcutActionId = "multiplayer_ping";
        private const string MultiplayerChatShortcutActionId = "multiplayer_chat";
        private const string MultiplayerRoomChatShortcutActionId = "multiplayer_room_chat";

        private static readonly string[] MultiplayerPingShortcutMenus =
        {
            MultiplayerLobbyMenuId,
            MultiplayerRoomBrowserMenuId,
            MultiplayerCreateRoomMenuId,
            MultiplayerRoomControlsMenuId,
            MultiplayerRoomPlayersMenuId,
            MultiplayerRoomOptionsMenuId,
            MultiplayerRoomTrackTypeMenuId,
            MultiplayerRoomTrackRaceMenuId,
            MultiplayerRoomTrackAdventureMenuId,
            MultiplayerLoadoutVehicleMenuId,
            MultiplayerLoadoutTransmissionMenuId
        };

        private static readonly string[] MultiplayerRoomShortcutMenus =
        {
            MultiplayerRoomControlsMenuId,
            MultiplayerRoomPlayersMenuId,
            MultiplayerRoomOptionsMenuId,
            MultiplayerRoomTrackTypeMenuId,
            MultiplayerRoomTrackRaceMenuId,
            MultiplayerRoomTrackAdventureMenuId,
            MultiplayerLoadoutVehicleMenuId,
            MultiplayerLoadoutTransmissionMenuId
        };

        public void ConfigureMenuCloseHandlers()
        {
            _menu.RegisterSharedShortcutAction(
                MultiplayerPingShortcutActionId,
                new MenuShortcut(SharpDX.DirectInput.Key.F1, CheckCurrentPing));
            _menu.RegisterSharedShortcutAction(
                MultiplayerChatShortcutActionId,
                new MenuShortcut(SharpDX.DirectInput.Key.Slash, OpenGlobalChatInput));
            _menu.RegisterSharedShortcutAction(
                MultiplayerRoomChatShortcutActionId,
                new MenuShortcut(SharpDX.DirectInput.Key.Backslash, OpenRoomChatInput));

            for (var i = 0; i < MultiplayerPingShortcutMenus.Length; i++)
            {
                _menu.SetSharedShortcutActions(
                    MultiplayerPingShortcutMenus[i],
                    new[] { MultiplayerPingShortcutActionId, MultiplayerChatShortcutActionId });
            }

            for (var i = 0; i < MultiplayerRoomShortcutMenus.Length; i++)
            {
                _menu.SetSharedShortcutActions(
                    MultiplayerRoomShortcutMenus[i],
                    new[] { MultiplayerPingShortcutActionId, MultiplayerChatShortcutActionId, MultiplayerRoomChatShortcutActionId });
            }

            _menu.SetCloseHandler(MultiplayerLobbyMenuId, _ =>
            {
                OpenDisconnectConfirmation();
                return true;
            });

            _menu.SetCloseHandler(MultiplayerRoomControlsMenuId, _ =>
            {
                OpenLeaveRoomConfirmation();
                return true;
            });

            _menu.SetCloseHandler(MultiplayerSavedServerFormMenuId, _ =>
            {
                CloseSavedServerForm();
                return true;
            });

            _menu.SetCloseHandler(MultiplayerRoomOptionsMenuId, _ =>
            {
                CancelRoomOptionsChanges();
                return false;
            });

            _menu.SetCloseHandler(MultiplayerLoadoutTransmissionMenuId, _ =>
            {
                OpenLoadoutExitConfirmation();
                return true;
            });

            _menu.SetCloseHandler(MultiplayerLoadoutVehicleMenuId, _ =>
            {
                OpenLoadoutExitConfirmation();
                return true;
            });
        }

        public void ShowMultiplayerMenuAfterRace()
        {
            if (_roomState.InRoom)
                _menu.ShowRoot(MultiplayerRoomControlsMenuId);
            else
                _menu.ShowRoot(MultiplayerLobbyMenuId);
        }

        public void BeginRaceLoadoutSelection()
        {
            if (!_roomState.InRoom)
                return;

            _pendingLoadoutVehicleIndex = 0;
            RebuildLoadoutVehicleMenu();
            RebuildLoadoutTransmissionMenu();
            _menu.ShowRoot(MultiplayerLoadoutVehicleMenuId);
            _enterMenuState();
        }
    }
}
