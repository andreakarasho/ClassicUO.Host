using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
    Console.WriteLine("Client connected {0}", id);
    clientId = id;
};
server2.OnClientDisconnected += id =>
{
    Console.WriteLine("Client disconnected {0}", id);
};
server2.OnClientMessage += msg =>
{
#if DEBUG
    Console.WriteLine("[SERVER] client msg received << cmd: {0}, id: {1}, size: {2} bytes", msg.Command, msg.ID, msg.Payload.Count);
#endif
};
server2.Start(address, port);

//var client2 = new TcpClientRpc();
//client2.OnServerMessage += msg =>
//{
//    Console.WriteLine("[CLIENT] server msg received << cmd: {0}, id: {1}", msg.Command, msg.ID);
//};
//client2.Connect(address, port);


Thread.Sleep(1000);


while (true)
{
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

//Console.WriteLine();
// const int ReadWriteTimeout = 60000;

//var server = new TcpListener(IPAddress.Parse(address), port);
//server.Start();
//server.Server.ReceiveTimeout = ReadWriteTimeout;
//server.Server.SendTimeout = ReadWriteTimeout;

//#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
//Task.Run(() => {
//    var clientRpcs = new List<Rpc>();
//    //using var serverWriter = new MemoryStream(4096);
//    //using var serverReader = new MemoryStream(4096);
//    //var rpcHost = new Rpc(serverReader, serverWriter);

//    var request = new byte[32];
//    request[0] = 1;

//    while (true)
//    {
//        var client = server.AcceptTcpClient();

//        var clientRpc = new Rpc(client.GetStream(), client.GetStream());
//        clientRpcs.Add(clientRpc);

//        //clientRpc.OnMessage += (rpc, msg) =>
//        //{

//        //};

//        //client.Close();
//        //client.Dispose();
//        //continue;


//        while (true)
//        {
//            var requestGuid = clientRpc.SendMessage(new ArraySegment<byte>(request));
//            var requestGuid2 = clientRpc.SendMessage(new ArraySegment<byte>(request));
//            var requestGuid3 = clientRpc.SendMessage(new ArraySegment<byte>(request));
//            var response = clientRpc.ReceiveMessage();
//            var response2 = clientRpc.ReceiveMessage();
//            var response3 = clientRpc.ReceiveMessage();

//            Trace.Assert(requestGuid.Equals(response.ID));
//            Trace.Assert(requestGuid2.Equals(response2.ID));
//            Trace.Assert(requestGuid3.Equals(response3.ID));
//        }

//    }
//});
//#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed



//var tcp = new TcpClient() { NoDelay = true, SendTimeout = ReadWriteTimeout, ReceiveTimeout = ReadWriteTimeout };
//tcp.Connect(address, port);
//#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
//Task.Run(() =>
//{
//    var clientRpc = new Rpc(tcp.GetStream(), tcp.GetStream());

//    //var id = clientRpc.SendMessage(new ArraySegment<byte>(Array.Empty<byte>()));

//    while (tcp.Connected)
//    {
//        var available = tcp.Available;
//        if (available <= 0)
//        {
//            Thread.Sleep(1);
//            continue;
//        }

//        var msg = clientRpc.ReceiveMessage();
//        Console.WriteLine("received: {0}", available);
//        //Thread.Sleep(500);
//        msg.Payload[0]++;
//        clientRpc.ResponseTo(msg);

//    }

//    Console.WriteLine("client disconnected");
//});
//#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

