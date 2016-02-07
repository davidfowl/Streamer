using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Streamer;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new TcpListener(IPAddress.Loopback, 1335);
            server.Start();

            Go(server).Wait();
        }

        private static async Task Go(TcpListener server)
        {
            Console.WriteLine("Waiting for client to connect on :1335");

            while (true)
            {
                var client = await server.AcceptTcpClientAsync();

                Console.WriteLine("Client connected {0}", client.Client.LocalEndPoint);

                var channel = Channel.CreateServer(client.GetStream());
                channel.Bind(new Handler());
                await channel.StartAsync();
            }
        }
    }
}
