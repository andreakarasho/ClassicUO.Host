using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

sealed class Rpc : IDisposable
{
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly ConcurrentDictionary<Guid, RpcMessage> _messages = new ConcurrentDictionary<Guid, RpcMessage>();

    public Rpc(Stream reader, Stream writer)
    {
        _reader = new BinaryReader(reader);
        _writer = new BinaryWriter(writer);
    }

    public void Dispose()
    {
        _messages?.Clear();
        _reader?.Dispose();
        _writer?.Dispose();
    }

    public RpcMessage Request(ArraySegment<byte> payload)
    {
        var reqId = SendMessage(payload);
       // var response = WaitForMessageAsync(reqId, TimeSpan.FromSeconds(10)).ConfigureAwait(false).GetAwaiter().GetResult(); // ReceiveMessage();
        var response = WaitForMessage(reqId, TimeSpan.FromSeconds(10));
        //var response = ReceiveMessage();

        Trace.Assert(reqId.Equals(response.ID));
        Trace.Assert(response.Command == RpcCommand.Response);

        return response;
    }

    RpcMessage WaitForMessage(Guid id, TimeSpan timeout)
    {
        RpcMessage msg = default;
        //var dt = DateTime.UtcNow;
        while (_reader.BaseStream.CanRead && !_messages.TryRemove(id, out msg))
        {
        }

        return msg;
    }

    async Task<RpcMessage> WaitForMessageAsync(Guid id, TimeSpan timeout)
    {
        await Task.Yield();
        return WaitForMessage(id, timeout);
    }

    internal void ResponseTo(RpcMessage request)
    {
        _writer.Write((byte)RpcCommand.Response);
        _writer.Write(request.ID.ToByteArray());
        _writer.Write((ushort)request.Payload.Count);
        _writer.Write(request.Payload.Array, request.Payload.Offset, request.Payload.Count);
        _writer.Flush();
    }

    private Guid SendMessage(ArraySegment<byte> payload)
    {
        var id = Guid.NewGuid();
        _writer.Write((byte)RpcCommand.Request);
        _writer.Write(id.ToByteArray());
        _writer.Write((ushort)payload.Count);
        _writer.Write(payload.Array, payload.Offset, payload.Count);
        _writer.Flush();
        return id;
    }

    internal RpcMessage ReceiveMessage()
    {
        var cmd = (RpcCommand)_reader.BaseStream.ReadByte();
        if (cmd == RpcCommand.Invalid)
        {
            return RpcMessage.Invalid;
        }

        var id = new Guid(
            _reader.ReadUInt32(),
            _reader.ReadUInt16(),
            _reader.ReadUInt16(),
            _reader.ReadByte(),
            _reader.ReadByte(),
            _reader.ReadByte(),
            _reader.ReadByte(),
            _reader.ReadByte(),
            _reader.ReadByte(),
            _reader.ReadByte(),
            _reader.ReadByte()
        );
        var payloadSize = _reader.ReadUInt16();
#if NETFRAMEWORK
        var payload = new ArraySegment<byte>(payloadSize == 0 ? Array.Empty<byte>() : new byte[payloadSize], 0, payloadSize);
#else
        var payload = payloadSize == 0 ? ArraySegment<byte>.Empty : new ArraySegment<byte>(new byte[payloadSize], 0, payloadSize);
#endif
        var read = payloadSize == 0 ? 0 : _reader.Read(payload.Array, payload.Offset, payload.Count);

        if (read != payloadSize)
        {

        }

        var msg = new RpcMessage(cmd, id, payload);

        if (!_messages.TryAdd(id, msg))
        {

        }

        return msg;
    }
}

static class RpcConst
{
    public const int READ_WRITE_TIMEOUT = 0;
}

sealed class TcpServerRpc
{
    private bool _accepting;
    private TcpListener _server;
    private readonly ConcurrentDictionary<Guid, ClientSession> _clients = new ConcurrentDictionary<Guid, ClientSession>();

    public Action<RpcMessage> OnClientMessage;
    public Action<Guid> OnClientConnected;
    public Action<Guid> OnClientDisconnected;

    public void Start(string address, int port)
    {
        _server = new TcpListener(IPAddress.Parse(address), port);
        _server.Server.NoDelay = true;
        _server.Server.ReceiveTimeout = RpcConst.READ_WRITE_TIMEOUT;
        _server.Server.SendTimeout = RpcConst.READ_WRITE_TIMEOUT;

        _accepting = true;
        _server.Start();

        Task.Run(AcceptClients);
    }

    public void Stop()
    {
        _accepting = false;
        _server.Stop();
    }

    public RpcMessage Request(Guid clientId, ArraySegment<byte> payload)
    {
        if (_clients.TryGetValue(clientId, out var client))
        {
            return client.Rpc.Request(payload);
        }

        return default;
    }

    void AcceptClients()
    {
        var threads = new List<Thread>();
        try
        {
            while (_accepting)
            {
                var client = _server.AcceptTcpClient();

                var thread = new Thread(() => ProcessClient(client));
                thread.IsBackground = true;
                thread.Start();

                threads.Add(thread);

                //Task.Run(() => ProcessClient(client)).ConfigureAwait(false);
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine("[SERVER] socket exception:\n{0}", ex);
        }

        foreach (var thread in threads)
        {
            if (thread.IsAlive)
            {
                thread.Abort();
            }
        }

        foreach (var l in _clients)
        {
            l.Value.Rpc.Dispose();
            l.Value.Client.Close();
            l.Value.Client.Dispose();
        }

        _server.Server.Dispose();
        _clients.Clear();
    }

    void ProcessClient(TcpClient client)
    {
        var stream = client.GetStream();
        var rpc = new Rpc(stream, stream);

        var session = new ClientSession(Guid.NewGuid(), client, rpc);
        _clients.TryAdd(session.Guid, session);

        OnClientConnected?.Invoke(session.Guid);

        while (client.Connected)
        {
            var msg = rpc.ReceiveMessage();
            OnClientMessage?.Invoke(msg);

            switch (msg.Command)
            {
                case RpcCommand.Request:
                    // Client is asking for some data, return the data to the client
                    rpc.ResponseTo(msg);
                    break;

                case RpcCommand.Response:
                    // Client is responding from a request we did
                    break;

                case RpcCommand.Invalid:
                    client.Close();
                    break;
            }
        }

        rpc.Dispose();
        client.Close();
        client.Dispose();

        _clients.TryRemove(session.Guid, out var _);

        OnClientDisconnected?.Invoke(session.Guid);
    }


    sealed class ClientSession
    {
        public ClientSession(Guid guid, TcpClient client, Rpc rpc)
        {
            Guid = guid;
            Client = client;
            Rpc = rpc;
        }

        public Guid Guid { get; }
        public TcpClient Client { get; }
        public Rpc Rpc { get; }
    }
}

sealed class TcpClientRpc
{
    private TcpClient _tcp;
    private Rpc _rpc;

    public Action<RpcMessage> OnServerMessage;

    public bool Connect(string address, int port)
    {
        _tcp = new TcpClient()
        {
            NoDelay = true,
            SendTimeout = RpcConst.READ_WRITE_TIMEOUT,
            ReceiveTimeout = RpcConst.READ_WRITE_TIMEOUT
        };

        _tcp.Connect(address, port);

        var connected = _tcp.Connected;

        if (connected)
        {
            var stream = _tcp.GetStream();
            _rpc = new Rpc(stream, stream);
            Task.Run(ProcessServerRequest);
        }
      
        return connected;
    }

    public void Disconnect()
    {
        _tcp?.Client?.Disconnect(false);
    }

    public RpcMessage Request(ArraySegment<byte> payload)
    {
        return _rpc.Request(payload);
    }


    void ProcessServerRequest()
    {
        while (_tcp.Connected)
        {
            var msg = _rpc.ReceiveMessage();
            OnServerMessage?.Invoke(msg);

            switch (msg.Command)
            {
                case RpcCommand.Request:
                    // Server is asking for some data, return the data to the server
                    _rpc.ResponseTo(msg);
                    break;

                case RpcCommand.Response:
                    // Server is responding from a request we did
                    break;

                case RpcCommand.Invalid:
                    _tcp.Close();
                    break;
            }
        }

        _rpc.Dispose();
        _tcp.Close();
        _tcp.Dispose();
    }
}

readonly struct RpcMessage
{
    public readonly RpcCommand Command;
    public readonly Guid ID;
    public readonly ArraySegment<byte> Payload;

    public RpcMessage(RpcCommand cmd, Guid id, ArraySegment<byte> payload)
        => (Command, ID, Payload) = (cmd, id, payload);

    public static readonly RpcMessage Invalid = new RpcMessage(RpcCommand.Invalid, Guid.Empty, new ArraySegment<byte>(Array.Empty<byte>()));
}

enum RpcCommand
{
    Invalid = -1,
    Request,
    Response
}