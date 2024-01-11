using CUO_API;
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
            OnCastSpell,
            OnSetWindowTitle,
            OnGetCliloc,
            OnRequestMove,
            OnGetPlayerPosition,
            OnUpdatePlayerPosition,
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

        [Pack]
        internal struct PluginCastSpell
        {
            public byte Cmd;
            public int SpellIndex;
        }

        [Pack]
        internal struct PluginSetWindowTitle
        {
            public byte Cmd;
            public string Title;
        }

        [Pack]
        internal struct PluginGetCliloc
        {
            public byte Cmd;
            public int Cliloc;
            public string Args;
            public bool Capitalize;
        }

        [Pack]
        internal struct PluginGetClilocResponse
        {
            public byte Cmd;
            public string Text;
        }

        [Pack]
        internal struct PluginRequestMove
        {
            public byte Cmd;
            public int Direction;
            public bool Run;
        }

        [Pack]
        internal struct PluginRequestMoveResponse
        {
            public byte Cmd;
            public bool CanMove;
        }

        [Pack]
        internal struct PluginGetPlayerPosition
        {
            public byte Cmd;
        }

        [Pack]
        internal struct PluginGetPlayerPositionResponse
        {
            public byte Cmd;
            public int X, Y, Z;
        }

        [Pack]
        internal struct PluginUpdatePlayerPositionRequest
        {
            public byte Cmd;
            public int X, Y, Z;
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

        protected override ArraySegment<byte> OnRequest(Guid id, ArraySegment<byte> msg)
        {
            if (msg.Count == 0)
                return _empty;

            if (!_plugins.TryGetValue(id, out Plugin plugin))
                return _empty;

            var cuoProtcolID = (PluginCuoProtocol)msg.Array[msg.Offset + 0];

            switch (cuoProtcolID)
            {
                case PluginCuoProtocol.OnInitialize:
                    var initReq = new PluginInitializeRequest();
                    initReq.Unpack(msg.Array, msg.Offset);

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
                        req.Unpack(msg.Array, msg.Offset);

                        var ok = plugin.ProcessHotkeys(req.Key, req.Mod, req.IsPressed);

                        var resp = new PluginHotkeyResponse()
                        {
                            Cmd = req.Cmd,
                            Allowed = ok,
                        };

                        using var buf = resp.PackToBuffer();

                        return new ArraySegment<byte>(buf.Data, 0, buf.Size);
                    }
                case PluginCuoProtocol.OnMouse:
                    {
                        var req = new PluginMouseRequest();
                        req.Unpack(msg.Array, msg.Offset);

                        plugin.ProcessMouse(req.Button, req.Wheel);
                    }
                    break;
                case PluginCuoProtocol.OnCmdList:
                    break;
                case PluginCuoProtocol.OnSdlEvent:
                    break;
                case PluginCuoProtocol.OnUpdatePlayerPos:
                    {
                        var req = new PluginUpdatePlayerPositionRequest();
                        req.Unpack(msg.Array, msg.Offset);

                        plugin.UpdatePlayerPosition(req.X, req.Y, req.Z);
                    }
                    break;
                case PluginCuoProtocol.OnPacketIn:
                    {
                        var packetLen = (int) BinaryPrimitives.ReadUInt16LittleEndian(msg.AsSpan(sizeof(byte), sizeof(ushort)));
                        var rentBuf = ArrayPool<byte>.Shared.Rent(packetLen);

                        try
                        {
                            msg.Array.AsSpan(sizeof(byte) + sizeof(ushort), packetLen).CopyTo(rentBuf);

                            var ok = plugin.ProcessRecvPacket(ref rentBuf, ref packetLen);

                            if (!ok)
                            {
                                packetLen = 0;
                            }

                            BinaryPrimitives.WriteUInt16LittleEndian(msg.AsSpan(sizeof(byte), sizeof(ushort)), (ushort)packetLen);
                            rentBuf.AsSpan(0, packetLen).CopyTo(msg.Array.AsSpan(sizeof(byte) + sizeof(ushort)));
                            return new ArraySegment<byte>(msg.Array, 0, sizeof(byte) + sizeof(ushort) + packetLen);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(rentBuf);
                        }   
                    }            
                case PluginCuoProtocol.OnPacketOut:
                    {
                        var packetLen = (int)BinaryPrimitives.ReadUInt16LittleEndian(msg.AsSpan(sizeof(byte), sizeof(ushort)));
                        var rentBuf = ArrayPool<byte>.Shared.Rent(packetLen);

                        try
                        {
                            msg.Array.AsSpan(sizeof(byte) + sizeof(ushort), packetLen).CopyTo(rentBuf);

                            var ok = plugin.ProcessSendPacket(ref rentBuf, ref packetLen);

                            if (!ok)
                            {
                                packetLen = 0;
                            }

                            BinaryPrimitives.WriteUInt16LittleEndian(msg.AsSpan(sizeof(byte), sizeof(ushort)), (ushort)packetLen);
                            rentBuf.AsSpan(0, packetLen).CopyTo(msg.Array.AsSpan(sizeof(byte) + sizeof(ushort)));
                            return new ArraySegment<byte>(msg.Array, 0, sizeof(byte) + sizeof(ushort) + packetLen);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(rentBuf);
                        }
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
            resp.Unpack(respMsg.Array, respMsg.Offset);

            if (respMsg.Array != null &&  respMsg.Array.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(respMsg.Array);
            }

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

        public void OnCastSpell(Guid id, int index)
        {
            var req = new PluginCastSpell()
            {
                Cmd = (byte)PluginCuoProtocol.OnCastSpell,
                SpellIndex = index
            };

            using var buf = req.PackToBuffer();

            var respMsg = Request(id, new ArraySegment<byte>(buf.Data, 0, buf.Size));
        }

        public void OnSetWindowTitle(Guid id, string title)
        {
            var req = new PluginSetWindowTitle()
            {
                Cmd = (byte)PluginCuoProtocol.OnSetWindowTitle,
                Title = title
            };

            using var buf = req.PackToBuffer();

            var respMsg = Request(id, new ArraySegment<byte>(buf.Data, 0, buf.Size));
        }

        public string OnGetCliloc(Guid id, int cliloc, string args, bool capitalize)
        {
            var req = new PluginGetCliloc()
            {
                Cmd = (byte)PluginCuoProtocol.OnGetCliloc,
                Cliloc = cliloc,
                Args = args,
                Capitalize = capitalize
            };

            using var buf = req.PackToBuffer();

            var respMsg = Request(id, new ArraySegment<byte>(buf.Data, 0, buf.Size));

            var resp = new PluginGetClilocResponse();
            resp.Unpack(respMsg.Array, respMsg.Offset);

            return resp.Text;
        }

        public bool OnRequestMove(Guid id, int dir, bool run) 
        {
            var req = new PluginRequestMove()
            {
                Cmd = (byte)PluginCuoProtocol.OnRequestMove,
                Direction = dir,
                Run = run
            };

            using var buf = req.PackToBuffer();

            var respMsg = Request(id, new ArraySegment<byte>(buf.Data, 0, buf.Size));

            var resp = new PluginRequestMoveResponse();
            resp.Unpack(respMsg.Array, respMsg.Offset);

            return resp.CanMove;
        }

        public bool OnGetPlayerPosition(Guid id, out int x, out int y, out int z)
        {
            var req = new PluginGetPlayerPosition()
            {
                Cmd = (byte)PluginCuoProtocol.OnGetPlayerPosition,
            };

            using var buf = req.PackToBuffer();

            var respMsg = Request(id, new ArraySegment<byte>(buf.Data, 0, buf.Size));

            var resp = new PluginGetPlayerPositionResponse();
            resp.Unpack(respMsg.Array, respMsg.Offset);

            x = resp.X;
            y = resp.Y;
            z = resp.Z;

            return resp.X != 0;
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

                if (respMsg.Array != null && respMsg.Array.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(respMsg.Array);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentBuf);
            }

            return true;
        }
    }
}
