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


    public Rpc(Stream reader, Stream writer)
    {
        _reader = new BinaryReader(reader);
        _writer = new BinaryWriter(writer);
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
    }

    public RpcMessage Request(ArraySegment<byte> payload)
    {
        var reqId = SendMessage(payload);
        var response = ReceiveMessage();

        Trace.Assert(reqId.Equals(response.ID));
        Trace.Assert(response.Command == RpcCommand.Response);

        return response;
    }

    public void ResponseTo(RpcMessage request)
    {
        _writer.Write((byte)RpcCommand.Response);
        _writer.Write(request.ID.ToByteArray());
        _writer.Write((ushort)request.Payload.Length);
        _writer.Write(request.Payload, 0, request.Payload.Length);
        _writer.Flush();
    }

    public Guid SendMessage(ArraySegment<byte> payload)
    {
        var id = Guid.NewGuid();
        _writer.Write((byte)RpcCommand.Request);
        _writer.Write(id.ToByteArray());
        _writer.Write((ushort)payload.Count);
        _writer.Write(payload.Array, payload.Offset, payload.Count);
        _writer.Flush();
        return id;
    }

    public RpcMessage ReceiveMessage()
    {
        var cmd = (RpcCommand)_reader.ReadByte();
        if (cmd == RpcCommand.Invalid)
        {
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
        var payload = new byte[payloadSize];
        var read = _reader.Read(payload, 0, payloadSize);

        if (read != payloadSize)
        {

        }

        return new RpcMessage(cmd, id, payload);
    }
}

static class RpcConst
{
    public const int READ_WRITE_TIMEOUT = 60000;
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
    }

    public RpcMessage Broadcast(ArraySegment<byte> payload)
    {
        foreach (var c in _clients)
        {
            var id = c.Value.Rpc.SendMessage(payload);
        }

        return default;
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
        while (_accepting)
        {
            var client = _server.AcceptTcpClient();
            Task.Run(() => ProcessClient(client));
        }

        _server.Stop();

        foreach (var l in _clients)
        {
            l.Value.Client.Close();
            l.Value.Client.Dispose();
        }

        _clients.Clear();
    }

    void ProcessClient(TcpClient client)
    {
        var stream = client.GetStream();
        using var rpc = new Rpc(stream, stream);

        var session = new ClientSession(Guid.NewGuid(), client, rpc);
        Console.WriteLine("accepted client: {0} [{1}]", session.Guid, client.Client.RemoteEndPoint.ToString());

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
            }
        }

        client.Close();
        client.Dispose();

        Console.WriteLine("client {0} [{1}] disconnected", session.Guid, client.Client.RemoteEndPoint.ToString());

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

        var stream = _tcp.GetStream();
        _rpc = new Rpc(stream, stream);

        Task.Run(ProcessServerRequest);

        return _tcp.Connected;
    }

    public RpcMessage Request(ArraySegment<byte> payload)
    {
        var id = _rpc.SendMessage(payload);



        //var msg = _rpc.ReceiveMessage();

        //Trace.Assert(id.Equals(msg.ID));
        //Trace.Assert(msg.Command == RpcCommand.Response);

        return default;
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
            }
        }

        _tcp.Close();
        _tcp.Dispose();
    }
}

readonly struct RpcMessage
{
    public readonly RpcCommand Command;
    public readonly Guid ID;
    public readonly byte[] Payload;

    public RpcMessage(RpcCommand cmd, Guid id, byte[] payload)
        => (Command, ID, Payload) = (cmd, id, payload);
}

enum RpcCommand
{
    Invalid = -1,
    Request,
    Response
}