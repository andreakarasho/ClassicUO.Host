using ClassicUO.Host;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
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

        //var cuoClient = new BaseClient();
        //cuoClient.Connect(address, port);

        Console.ReadLine();

        var server = new BaseServer();
        server.Start(address, port);

        Console.ReadLine();

        var client2 = new BaseClient();
        client2.Connect(address, port);

        Thread.Sleep(1000);

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

    }
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

    protected override void OnMessage(Guid id, RpcMessage msg)
    {
#if DEBUG
        Console.WriteLine("[SERVER] client {0} msg sent << cmd: {1}, id: {2}, size: {3} bytes", id, msg.Command, msg.ID, msg.Payload.Count);
#endif

        switch (msg.Command)
        {
            case RpcCommand.Request:
                //Array.Clear(msg.Payload.Array, msg.Payload.Offset, msg.Payload.Count);
                break;

            case RpcCommand.Response:
                break;
        }

        return;
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