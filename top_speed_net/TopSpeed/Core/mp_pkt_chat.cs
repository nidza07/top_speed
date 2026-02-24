using TopSpeed.Network;
using TopSpeed.Protocol;

namespace TopSpeed.Core
{
    internal sealed partial class Game
    {
        private void RegisterMultiplayerChatPacketHandlers()
        {
            _mpPktReg.Add("chat", Command.ProtocolMessage, HandleMpProtocolMessagePacket);
        }

        private bool HandleMpProtocolMessagePacket(IncomingPacket packet)
        {
            if (ClientPacketSerializer.TryReadProtocolMessage(packet.Payload, out var message))
                _multiplayerCoordinator.HandleProtocolMessage(message);
            return true;
        }
    }
}
