using System;

namespace AkkaTest.DeployTarget
{
    class Program
    {
        static void Main(string[] args)
        {
            var actorService = new ActorService();
            actorService.Start();

            Console.CancelKeyPress += (sender, eventArgs) => { actorService.Stop(); };
            actorService.WhenTerminated.Wait();
        }
    }
}