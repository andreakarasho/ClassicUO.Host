using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Host
{
    sealed class ClassicUORpcServer : TcpServerRpc
    {
        enum PluginCuoProtocol : byte
        {
            OnInitialize,
            OnTick,
            OnClosing,
            OnFocusGained,
            OnFocusLost,
            OnConnected,
            OnDisconnected,
            OnHotkey,
            OnMouse,
            OnCmdList,
            OnSdlEvent,
            OnUpdatePlayerPos,
            OnPacketIn,
            OnPacketOut,

            OnPluginRecv,
            OnPluginSend,
        }

        private readonly ConcurrentDictionary<Guid, Plugin> _plugins = new ConcurrentDictionary<Guid, Plugin>();

        protected override void OnClientConnected(Guid id)
        {
            var plugin = new Plugin(this, id);
            _plugins.TryAdd(id, plugin);
        }

        protected override void OnClientDisconnected(Guid id)
        {
            if (_plugins.TryRemove(id, out Plugin plugin))
            {
                plugin.Close();
            }
        }

        protected override void OnMessage(Guid id, RpcMessage msg)
        {
            if (msg.Command == RpcCommand.Response)
                return;

            if (msg.Payload.Count == 0)
                return;

            if (!_plugins.TryGetValue(id, out Plugin plugin))
                return;

            var cuoProtcolID = (PluginCuoProtocol)msg.Payload.Array[msg.Payload.Offset + 0];

            switch (cuoProtcolID)
            {
                case PluginCuoProtocol.OnInitialize:
                    var clientVersion = BinaryPrimitives.ReadUInt32LittleEndian(msg.Payload.AsSpan(1, sizeof(uint)));
                    var pluginPathlen = BinaryPrimitives.ReadInt16LittleEndian(msg.Payload.AsSpan(1 + sizeof(uint), sizeof(ushort)));
                    var pluginPath = Encoding.UTF8.GetString(msg.Payload.Array, 1 + sizeof(uint) + sizeof(ushort), pluginPathlen);
                    var assetsPathLen = BinaryPrimitives.ReadInt16LittleEndian(msg.Payload.AsSpan(1 + sizeof(uint) + sizeof(ushort) + pluginPathlen, sizeof(ushort)));
                    var assetsPath = Encoding.UTF8.GetString(msg.Payload.Array, 1 + sizeof(uint) + sizeof(ushort) * 2 + pluginPathlen, assetsPathLen);
                    plugin.Load(pluginPath, clientVersion, assetsPath);
                    break;
                case PluginCuoProtocol.OnTick:
                    plugin.Tick();
                    break;
                case PluginCuoProtocol.OnClosing:
                    plugin.Close();
                    break;
                case PluginCuoProtocol.OnFocusGained:
                    plugin.FocusGained();
                    break;
                case PluginCuoProtocol.OnFocusLost:
                    plugin.FocusLost();
                    break;
                case PluginCuoProtocol.OnConnected:
                    plugin.Connected();
                    break;
                case PluginCuoProtocol.OnDisconnected:
                    plugin.Disconnected();
                    break;
                case PluginCuoProtocol.OnHotkey:
                    {
                        var key = BinaryPrimitives.ReadInt32LittleEndian(msg.Payload.AsSpan(1, sizeof(int)));
                        var mod = BinaryPrimitives.ReadInt32LittleEndian(msg.Payload.AsSpan(1 + sizeof(int), sizeof(int)));
                        var isPressed = msg.Payload.AsSpan(1 + sizeof(int) * 2, 1)[0] == 0x01;
                        var ok = plugin.ProcessHotkeys(key, mod, isPressed);
                    }
                    break;
                case PluginCuoProtocol.OnMouse:
                    {
                        var button = BinaryPrimitives.ReadInt32LittleEndian(msg.Payload.AsSpan(1, sizeof(int)));
                        var wheel = BinaryPrimitives.ReadInt32LittleEndian(msg.Payload.AsSpan(1 + sizeof(int), sizeof(int)));
                        plugin.ProcessMouse(button, wheel);
                    }
                    break;
                case PluginCuoProtocol.OnCmdList:
                    break;
                case PluginCuoProtocol.OnSdlEvent:
                    break;
                case PluginCuoProtocol.OnUpdatePlayerPos:
                    break;
                case PluginCuoProtocol.OnPacketIn:
                    {
                        var size = msg.Payload.Count - 1;
                        var buf = new byte[size];
                        Array.Copy(msg.Payload.Array, msg.Payload.Offset + 1, buf, 0, size);
                        var ok = plugin.ProcessRecvPacket(buf, ref size);
                        Array.Copy(buf, 0, msg.Payload.Array, 1, size);
                    }
                   
                    break;
                case PluginCuoProtocol.OnPacketOut:
                    {
                        var size = msg.Payload.Count - 1;
                        var buf = new byte[size];
                        Array.Copy(msg.Payload.Array, msg.Payload.Offset + 1, buf, 0, size);
                        var span = buf.AsSpan(0, size);
                        var ok = plugin.ProcessSendPacket(ref span);
                        Array.Copy(buf, 0, msg.Payload.Array, 1, size);
                    }
                   
                    break;
            }

            return;
        }

        public async Task<short> GetPackeLen(Guid id)
        {
            var payload = new ArraySegment<byte>(Array.Empty<byte>());
            var resp = await Request(id, payload);
            return 0;
        }

        public async Task<bool> OnPluginRecv(Guid id, byte[] buffer, int len)
        {
            var buf = new byte[1 + len];
            buf[0] = (byte)PluginCuoProtocol.OnPluginRecv;

            Array.Copy(buffer, 0, buf, 1, len);

            var req = await Request(id, new ArraySegment<byte>(buf));

            return true;
        }

        public async Task<bool> OnPluginRecv(Guid id, IntPtr buffer, int len)
        {
            var buf = new byte[1 + len];
            buf[0] = (byte)PluginCuoProtocol.OnPluginRecv;

            unsafe
            {
                fixed (byte* pt = &buf[1])
                    Buffer.MemoryCopy(buffer.ToPointer(), pt, sizeof(byte) * len, sizeof(byte) * len);
            }
            
            var req = await Request(id, new ArraySegment<byte>(buf));

            return true;
        }

        public async Task<bool> OnPluginSend(Guid id, byte[] buffer, int len)
        {
            var buf = new byte[1 + len];
            buf[0] = (byte)PluginCuoProtocol.OnPluginSend;

            Array.Copy(buffer, 0, buf, 1, len);

            var req = await Request(id, new ArraySegment<byte>(buf));

            return true;
        }

        public async Task<bool> OnPluginSend(Guid id, IntPtr buffer, int len)
        {
            var buf = new byte[1 + len];
            buf[0] = (byte)PluginCuoProtocol.OnPluginSend;
           
            unsafe
            {
                fixed (byte* pt = &buf[1])
                    Buffer.MemoryCopy(buffer.ToPointer(), pt, sizeof(byte) * len, sizeof(byte) * len);
            }

            var req = await Request(id, new ArraySegment<byte>(buf));

            return true;
        }
    }
}
