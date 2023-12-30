using ClassicUO.Host;
using System;
using System.Net;


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

var cuoServer = new ClassicUORpcServer();
cuoServer.Start(address, port);

Console.ReadLine();
Console.WriteLine("finished");

