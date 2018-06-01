using System;
using Akka.Actor;

namespace AkkaTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var system = ActorSystem.Create("MySystem");
            var greeter = system.ActorOf<GreetingActor>("greeter");
            greeter.Tell(new Greet("World"));
            Console.ReadLine();
        }
    }
}