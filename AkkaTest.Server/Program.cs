using System;
using Akka.Actor;
using Akka.Configuration;
using AkkaTest.Common;

namespace AkkaTest.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ConfigurationFactory.ParseString(@"
                        akka {  
                            actor {
                                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                            }
                            remote {
                                helios.tcp {
                                    transport-class = ""Akka.Remote.Transport.Helios.HeliosTcpTransport, Akka.Remote""
                                    applied-adapters = []
                                    transport-protocol = tcp
                                    port = 8081
                                    hostname = localhost
                                }
                            }
                        }
                        ");
            using (var system = ActorSystem.Create("MyServer", config))
            {
                system.ActorOf<GreetingActor>("Greeting");

                Console.ReadLine();
            }
        }
    }
}