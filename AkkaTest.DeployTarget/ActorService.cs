using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Akka.Actor;

namespace AkkaTest.DeployTarget
{
    public class ActorService
    {
        protected ActorSystem ClusterSystem;

        public Task WhenTerminated => ClusterSystem.WhenTerminated;


        public bool Start()
        {
            var configuration = @"
                akka {
                    actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                    remote {
                        helios.tcp {
                            port = 8099
                            hostname = {localIp}
                        }
                    }
                }".Replace("{localIp}", GetLocalIpAddress());
            ClusterSystem = ActorSystem.Create("DeployTarget", configuration);
            return true;
        }

        public Task Stop()
        {
            return CoordinatedShutdown.Get(ClusterSystem).Run();
        }
        
        string GetLocalIpAddress()
        {
            string localIp = null;
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = (IPEndPoint) socket.LocalEndPoint;
                if (endPoint != null) localIp = endPoint.Address.ToString();
            }

            return localIp;
        }
    }
}