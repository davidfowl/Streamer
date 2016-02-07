using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
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

            var adder = channel.As<IAdder>();

            int value = 0;

            while (true)
            {
                Console.ReadLine();

                value = await adder.Increment(value);

                Console.WriteLine(value);
            }
        }
    }
}
