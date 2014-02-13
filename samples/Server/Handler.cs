
using System;
namespace Server
{
    public class Handler
    {
        public int Increment(int value)
        {
            Console.WriteLine("Received " + value);

            return value + 1;
        }
    }
}
