
using System;

namespace Sample
{
    public class EchoHandler
    {
        public string Echo(string value)
        {
            Console.WriteLine("Received " + value);

            return value;
        }
    }
}
