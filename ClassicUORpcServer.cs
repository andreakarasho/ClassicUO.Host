using StructPacker;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Host
{
    sealed class ClassicUORpcServer : TcpServerRpc
    {
        private readonly ConcurrentDictionary<Guid, Plugin> _plugins = new ConcurrentDictionary<Guid, Plugin>();

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
            OnPacketLength,
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

        //[Pack]
        //internal struct PluginPacketRequestResponse
        //{
        //    public byte Cmd;
        //    public byte[] Packet;
        //}

        [Pack]
        internal struct PluginPacketLengthRequest
        {
            public byte Cmd;
            public byte ID;
        }

        [Pack]
        internal struct PluginPacketLengthResponse
        {
            public byte Cmd;
            public short PacketLength;
        }

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
                        var packetLen = (int) BinaryPrimitives.ReadUInt16LittleEndian(msg.Payload.AsSpan(sizeof(byte), sizeof(ushort)));
                        var buf = new byte[packetLen];

                        msg.Payload.Array.AsSpan(sizeof(byte) + sizeof(ushort)).CopyTo(buf);

                        var ok = plugin.ProcessRecvPacket(ref buf, ref packetLen);
                        
                        if (!ok)
                        {
                            packetLen = 0;
                        }

                        BinaryPrimitives.WriteUInt16LittleEndian(msg.Payload.AsSpan(sizeof(byte), sizeof(ushort)), (ushort)packetLen);
                        buf.AsSpan(0, packetLen).CopyTo(msg.Payload.Array.AsSpan(sizeof(byte) + sizeof(ushort)));
                        return new ArraySegment<byte>(msg.Payload.Array, 0, sizeof(byte) + sizeof(ushort) + packetLen);
                    }            
                case PluginCuoProtocol.OnPacketOut:
                    {
                        var packetLen = (int)BinaryPrimitives.ReadUInt16LittleEndian(msg.Payload.AsSpan(sizeof(byte), sizeof(ushort)));
                        var buf = new byte[packetLen];

                        msg.Payload.Array.AsSpan(sizeof(byte) + sizeof(ushort)).CopyTo(buf);
                        
                        var ok = plugin.ProcessSendPacket(ref buf, ref packetLen);

                        if (!ok)
                        {
                            packetLen = 0;
                        }

                        BinaryPrimitives.WriteUInt16LittleEndian(msg.Payload.AsSpan(sizeof(byte), sizeof(ushort)), (ushort)packetLen);
                        buf.AsSpan(0, packetLen).CopyTo(msg.Payload.Array.AsSpan(sizeof(byte) + sizeof(ushort)));
                        return new ArraySegment<byte>(msg.Payload.Array, 0, sizeof(byte) + sizeof(ushort) + packetLen);
                    }
            }

            return _empty;
        }

        public short GetPacketLen(Guid id, byte packetID)
        {
            var req = new PluginPacketLengthRequest()
            {
                Cmd = (byte)PluginCuoProtocol.OnPacketLength,
                ID = packetID
            };

            using var buf = req.PackToBuffer();
            var respMsg = Request(id, new ArraySegment<byte>(buf.Data, 0, buf.Size));

            var resp = new PluginPacketLengthResponse();
            resp.Unpack(respMsg.Payload.Array, respMsg.Payload.Offset);

            return resp.PacketLength;
        }

        public unsafe bool OnPluginRecv(Guid id, byte[] buffer, int len)
        {
            fixed (byte* ptr = buffer)
                return OnPluginRecv(id, (IntPtr)ptr, len);
        }

        public bool OnPluginRecv(Guid id, IntPtr buffer, int len)
        {
            return OnPluginSendRecv(id, buffer, len, PluginCuoProtocol.OnPluginRecv);
        }

        public unsafe bool OnPluginSend(Guid id, byte[] buffer, int len)
        {
            fixed (byte* ptr = buffer)
                return OnPluginSend(id, (IntPtr)ptr, len);
        }

        public bool OnPluginSend(Guid id, IntPtr buffer, int len)
        {
            return OnPluginSendRecv(id, buffer, len, PluginCuoProtocol.OnPluginSend);
        }

        private bool OnPluginSendRecv(Guid id, IntPtr buffer, int len, PluginCuoProtocol protocol)
        {
            var rentBuf = ArrayPool<byte>.Shared.Rent(sizeof(byte) + sizeof(ushort) + len);

            try
            {
                rentBuf[0] = (byte)protocol;
                BinaryPrimitives.WriteUInt16LittleEndian(rentBuf.AsSpan(sizeof(byte), sizeof(ushort)), (ushort)len);

                unsafe
                {
                    fixed (byte* pt = &rentBuf[sizeof(byte) + sizeof(ushort)])
                        Buffer.MemoryCopy(buffer.ToPointer(), pt, sizeof(byte) * len, sizeof(byte) * len);
                }

                var respMsg = Request(id, new ArraySegment<byte>(rentBuf, 0, sizeof(byte) + sizeof(ushort) + len));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentBuf);
            }

            return true;
        }
    }
}
