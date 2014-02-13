# Streamer

RPC over a network should be simpler so here you go. Streamer offers a simple API for RPC over any bidirectional stream.

## NuGet

```
Install-Package Streamer
```

## Server

```C#
var server = new TcpListener(IPAddress.Loopback, 1335);
server.Start();

while (true)
{
    var client = await server.AcceptTcpClientAsync();

    // Create a channel to communicate with the client
    var channel = new Channel(client.GetStream());

    // Bind the handler that will handle callbacks
    channel.Bind(new Handler());
}


public class Handler
{
    public int Increment(int value)
    {
        Console.WriteLine("Received " + value);

        return value + 1;
    }
}

```

## Client

```C#
var client = new TcpClient();
await client.ConnectAsync(IPAddress.Loopback, 1335);

// Create a channel so we can communicate with the server
var channel = new Channel(client.GetStream());

// Invoke a method and get a result
var result = await channel.Invoke<int>("SomeMethod");
```

## Typed Clients

```C#
public interface IAdder
{
    Task<int> Increment(int value);
}

var client = new TcpClient();
await client.ConnectAsync(IPAddress.Loopback, 1335);

// Create a channel so we can communicate with the server
var channel = new Channel(client.GetStream());

// Create a proxy to the adder interface
var adder = Channel.CreateProxy<IAdder>(channel);

// Invoke a method and get a result
var result = await adder.Increment(0);
```
