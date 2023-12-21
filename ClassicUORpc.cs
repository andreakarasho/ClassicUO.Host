using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Host
{
    enum CuoRpcProtocol
    {
        ClientReceivePacketFromServer,
        ClientReceivePacketToServer,

        ClientPressedHotkey,
        
        AssistantSendPacketToClient,
        AssistantSendPacketToServer,


        AssistantAskPlayerPosition
    }

    internal class CuoCustomServer : TcpServerRpc
    {
        protected override void OnClientConnected(Guid id)
        {
        }

        protected override void OnClientDisconnected(Guid id)
        {
        }

        protected override void OnMessage(RpcMessage msg)
        {
        }

        public short GetPackeLen(Guid id)
        {
            var payload = new ArraySegment<byte>(Array.Empty<byte>());
            var resp = Request(id, payload);
            return 0;
        }
    }
}
