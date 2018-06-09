using System;
using System.Threading;
using Akka.Actor;

namespace AkkaTest.Common
{
    /// <summary>
    /// Class Message.
    /// </summary>
    public class Ping
    {
        public string Message { private set; get; }

        public Ping(string message)
        {
            Message = message;
        }
    }
    
    public class Pong
    {
        public string Message { private set; get; }

        public Pong(string message)
        {
            Message = message;
        }
    }

    public class GreetingActor : ReceiveActor
    {
        public GreetingActor()
        {
            Console.WriteLine(Context.Self.Path + ": GreetingActor init ...");

            Receive<Ping>(greet =>
            {
                Console.WriteLine(Context.Self.Path + " ==> " + greet.Message);
                Sender.Tell(new Pong("Hi, I received <" + greet.Message + "> ^_^ "));
            });

            Receive<Pong>(greet =>
            {
                Console.WriteLine(Context.Self.Path + " ==> " + greet.Message);
            });
        }
    }
}