using System;
using System.Net;
using System.Net.Sockets;
using Akka.Actor;

namespace AkkaTest.Local
{
    class Program
    {
        static void Main(string[] args)
        {            
            Console.WriteLine(GetLocalIPAddress());
            var system = ActorSystem.Create("MySystem");
            var greeter = system.ActorOf<GreetingActor>("greeter");
            greeter.Tell(new Greet("World"));
            Console.ReadLine();
        }
        
        static string GetLocalIPAddress()
        {
            string localIp = null;
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = (IPEndPoint)socket.LocalEndPoint;
                if (endPoint != null) localIp = endPoint.Address.ToString();
            }
            return localIp;
        }

    }
}