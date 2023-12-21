using ClassicUO.Host;
using System;
using System.Linq;
using System.Net;
using System.Threading;

var address = "127.0.0.1";
var port = 7777;

if (args.Length >= 1)
{
    Console.WriteLine(args[0]);
    address = IPAddress.Parse(args[0]).ToString();
}

if (args.Length >= 2)
{
    Console.WriteLine(args[1]);
    port = int.Parse(args[1]);
}

var cuoServer = new CuoCustomServer();
cuoServer.Start(address, port);
var pluginLoader = new Plugin(cuoServer);
pluginLoader.Load(@"");

Console.ReadLine();

var server = new BaseServer();
server.Start(address, port);

Console.ReadLine();

var client2 = new BaseClient();
client2.Connect(address, port);

Thread.Sleep(1000);

server.SendTest();

while (true)
{
    var key = Console.ReadKey();
    if (key.Key == ConsoleKey.A)
    {
        server.Stop();
        //resp = client2.Request(new ArraySegment<byte>(buf));
        //client2.Disconnect();
    }

    Thread.Sleep(1);
}


sealed class BaseServer : TcpServerRpc
{
    protected override void OnClientConnected(Guid id)
    {
        Console.WriteLine("[SERVER] Client connected {0}", id);
    }

    protected override void OnClientDisconnected(Guid id)
    {
        Console.WriteLine("[SERVER] Client disconnected {0}", id);
    }

    protected override void OnMessage(RpcMessage msg)
    {
#if DEBUG
        Console.WriteLine("[SERVER] client msg received << cmd: {0}, id: {1}, size: {2} bytes", msg.Command, msg.ID, msg.Payload.Count);
#endif

        switch (msg.Command)
        {
            case RpcCommand.Request:
                //Array.Clear(msg.Payload.Array, msg.Payload.Offset, msg.Payload.Count);
                break;

            case RpcCommand.Response:
                break;
        }
    }

    public RpcMessage SendTest()
    {
        if (Clients.Count <= 0)
            return default;

        var buf = new byte[6000];
        buf[0] = 123;
        return Request(Clients.FirstOrDefault().Key, new ArraySegment<byte>(buf));
    }
}

sealed class BaseClient : TcpClientRpc
{
    protected override void OnConnected()
    {
        Console.WriteLine("[CLIENT] connected!");
    }

    protected override void OnDisconnected()
    {
        Console.WriteLine("[CLIENT] disconnected!");
    }

    protected override void OnMessage(RpcMessage msg)
    {
#if DEBUG
        Console.WriteLine("[CLIENT] server msg received << cmd: {0}, id: {1}", msg.Command, msg.ID);
#endif
    }
}