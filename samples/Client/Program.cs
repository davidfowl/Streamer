using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Sample;
using Streamer;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new TcpClient();
            Go(client).Wait();
        }

        private static async Task Go(TcpClient client)
        {
            Console.WriteLine("Press any key to connect");

            Console.ReadLine();

            Console.WriteLine("Connecting to :1335...");

            await client.ConnectAsync(IPAddress.Loopback, 1335);

            Console.WriteLine("Connected!");

            var channel = Channel.CreateClient(client.GetStream());

            var echoHandler = channel.As<IEchoHandler>();

            while (true)
            {
                var value = await echoHandler.EchoAsync(Console.ReadLine());

                Console.WriteLine(value);
            }
        }
    }
}
