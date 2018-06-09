using System;
using Akka.Actor;
using Akka.Configuration;

namespace AkkaTest.Cluster
{
    class Program
    {
        private static void Main(string[] args)
        {
            StartUp(args.Length == 0 ? new String[] {"2551", "2552"} : args);
            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
        }

        static void StartUp(string[] ports)
        {
            var akkaConfig = ConfigurationFactory.ParseString(@"
                akka {
                    actor {
                        provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                        debug {  
                          receive = on 
                          autoreceive = on
                          lifecycle = on
                          event-stream = on
                          unhandled = on
                        }
                    }
                    remote {
                        log-remote-lifecycle-events = DEBUG
                        dot-netty.tcp {
                            hostname = ""127.0.0.1""
                            port = 0
                        }
                    }
                    cluster {
                        seed-nodes = [
                            ""akka.tcp://ClusterSystem@127.0.0.1:2551"",
                        ]
                        auto-down-unreachable-after = 30s
                        roles = [server]
                    }
                }");
            foreach (var port in ports)
            {
                var config =
                    ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.port=" + port)
                        .WithFallback(akkaConfig);
                ActorSystem.Create("ClusterSystem", config);
            }
        }
    }
}