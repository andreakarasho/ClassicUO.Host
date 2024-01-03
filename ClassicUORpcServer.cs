using StructPacker;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Drawing;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Host
{
    sealed class ClassicUORpcServer : TcpServerRpc
    {
        public enum PluginCuoProtocol : byte
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

        [Pack]
        internal struct PluginInitializeRequest
        {
            public byte Cmd;
            public uint ClientVersion;
            public string PluginPath;
            public string AssetsPath;
        }

        [Pack]
        internal struct PluginHotkeyRequest
        {
            public byte Cmd;
            public int Key;
            public int Mod;
            public bool IsPressed;
        }

        [Pack]
        internal struct PluginHotkeyResponse
        {
            public byte Cmd;
            public bool Allowed;
        }

        [Pack]
        internal struct PluginMouseRequest
        {
            public byte Cmd;
            public int Button;
            public int Wheel;
        }

        [Pack]
        internal struct PluginSimpleRequest
        {
            public byte Cmd;
        }

        [Pack]
        internal struct PluginPacketRequest
        {
            public byte Cmd;
            public byte[] Packet;
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

            Environment.Exit(0);
        }

        static readonly ArraySegment<byte> _empty = new ArraySegment<byte>(Array.Empty<byte>());

        protected override ArraySegment<byte> OnRequest(Guid id, RpcMessage msg)
        {
            if (msg.Payload.Count == 0)
                return _empty;

            if (!_plugins.TryGetValue(id, out Plugin plugin))
                return _empty;

            var cuoProtcolID = (PluginCuoProtocol)msg.Payload.Array[msg.Payload.Offset + 0];

            switch (cuoProtcolID)
            {
                case PluginCuoProtocol.OnInitialize:
                    var initReq = new PluginInitializeRequest();
                    initReq.Unpack(msg.Payload.Array, msg.Payload.Offset);

                    plugin.Load(initReq.PluginPath, initReq.ClientVersion, initReq.AssetsPath);
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
                        var req = new PluginHotkeyRequest();
                        req.Unpack(msg.Payload.Array, msg.Payload.Offset);

                        var ok = plugin.ProcessHotkeys(req.Key, req.Mod, req.IsPressed);

                        var resp = new PluginHotkeyResponse()
                        {
                            Cmd = (byte)msg.Command,
                            Allowed = ok,
                        };
                        using var buf = resp.PackToBuffer();

                        return new ArraySegment<byte>(buf.Data, 0, buf.Size);
                    }
                case PluginCuoProtocol.OnMouse:
                    {
                        var req = new PluginMouseRequest();
                        req.Unpack(msg.Payload.Array, msg.Payload.Offset);

                        plugin.ProcessMouse(req.Button, req.Wheel);
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
                        var req = new PluginPacketRequest();
                        req.Unpack(msg.Payload.Array, msg.Payload.Offset);

                        var packetLen = req.Packet.Length;
                        var ok = plugin.ProcessRecvPacket(ref req.Packet, ref packetLen);

                        var resp = new PluginPacketRequest()
                        {
                            Cmd = req.Cmd,
                            Packet = ok ? req.Packet : Array.Empty<byte>(),
                        };

                        using var buf = resp.PackToBuffer();

                        return new ArraySegment<byte>(buf.Data, 0, buf.Size);
                    }            
                case PluginCuoProtocol.OnPacketOut:
                    {
                        var req = new PluginPacketRequest();
                        req.Unpack(msg.Payload.Array, msg.Payload.Offset);

                        var span = req.Packet.AsSpan();
                        var ok = plugin.ProcessSendPacket(ref span);

                        var resp = new PluginPacketRequest()
                        {
                            Cmd = req.Cmd,
                            Packet = ok ? req.Packet : Array.Empty<byte>(),
                        };

                        using var buf = resp.PackToBuffer();

                        return new ArraySegment<byte>(buf.Data, 0, buf.Size);
                    }
            }

            return _empty;
        }

        public short GetPackeLen(Guid id)
        {
            var payload = new ArraySegment<byte>(Array.Empty<byte>());
            var resp = Request(id, payload);
            return 0;
        }

        public bool OnPluginRecv(Guid id, byte[] buffer, int len)
        {
            var buf = new byte[1 + len];
            buf[0] = (byte)PluginCuoProtocol.OnPluginRecv;

            Array.Copy(buffer, 0, buf, 1, len);

            var req = Request(id, new ArraySegment<byte>(buf));

            return true;
        }

        public bool OnPluginRecv(Guid id, IntPtr buffer, int len)
        {
            var buf = new byte[1 + len];
            buf[0] = (byte)PluginCuoProtocol.OnPluginRecv;

            unsafe
            {
                fixed (byte* pt = &buf[1])
                    Buffer.MemoryCopy(buffer.ToPointer(), pt, sizeof(byte) * len, sizeof(byte) * len);
            }
            
            var req = Request(id, new ArraySegment<byte>(buf));

            return true;
        }

        public bool OnPluginSend(Guid id, byte[] buffer, int len)
        {
            var buf = new byte[1 + len];
            buf[0] = (byte)PluginCuoProtocol.OnPluginSend;

            Array.Copy(buffer, 0, buf, 1, len);

            var req = Request(id, new ArraySegment<byte>(buf));

            return true;
        }

        public bool OnPluginSend(Guid id, IntPtr buffer, int len)
        {
            var buf = new byte[1 + len];
            buf[0] = (byte)PluginCuoProtocol.OnPluginSend;
           
            unsafe
            {
                fixed (byte* pt = &buf[1])
                    Buffer.MemoryCopy(buffer.ToPointer(), pt, sizeof(byte) * len, sizeof(byte) * len);
            }

            var req = Request(id, new ArraySegment<byte>(buf));

            return true;
        }
    }
}
