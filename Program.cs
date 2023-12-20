using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VoltRpc.Communication;

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


var server2 = new TcpServerRpc();
var clientId = Guid.Empty;
server2.OnClientConnected += id =>
{
    Console.WriteLine("[SERVER] Client connected {0}", id);
    clientId = id;
};
server2.OnClientDisconnected += id =>
{
    Console.WriteLine("[SERVER] Client disconnected {0}", id);
};
server2.OnClientMessage += msg =>
{
#if DEBUG
    Console.WriteLine("[SERVER] client msg received << cmd: {0}, id: {1}, size: {2} bytes", msg.Command, msg.ID, msg.Payload.Count);
#endif
};
server2.Start(address, port);

Console.ReadLine();

var client2 = new TcpClientRpc();
client2.OnServerMessage += msg =>
{
    Console.WriteLine("[CLIENT] server msg received << cmd: {0}, id: {1}", msg.Command, msg.ID);
};
client2.Connect(address, port);

Thread.Sleep(1000);

var buf = new byte[6000];
buf[0] = 123;
var resp = server2.Request( clientId,new ArraySegment<byte>(buf));

while (true)
{

    var key = Console.ReadKey();
    if (key.Key == ConsoleKey.A)
    {
        //server2.Stop();
        //resp = client2.Request(new ArraySegment<byte>(buf));
        client2.Disconnect();
    }
    //
    //var respFromClient = server2.Request(clientId, new ArraySegment<byte>(Array.Empty<byte>()));
    //var respoFromServer = client2.Request(new ArraySegment<byte>(Array.Empty<byte>())); 

    Thread.Sleep(1);
}


// Assistant is asking for some data from CUO
int GetPlayerPosition(Guid guid)
{
    var msg = server2.Request(guid, new ArraySegment<byte>(Array.Empty<byte>()));
    if (msg.Payload.Count > 0)
    {
        // parse payload
        return 123;
    }
    return 0;
}