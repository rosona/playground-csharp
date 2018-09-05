using System;
using System.Threading;
using Open.Nat;

namespace ConsoleAppTest.Network
{
    public class OPenNetTest
    {
        public async void Test()
        {
            var discoverer = new NatDiscoverer();
            var cts = new CancellationTokenSource(10000);
            var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

            var ip = await device.GetExternalIPAsync();
            Console.WriteLine("The external IP Address is: {0} ", ip);

            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1600, 1700, "Test port"));
            foreach (var mapping in await device.GetAllMappingsAsync())
            {
                Console.WriteLine(mapping);
            }

            await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, 1600, 1700, "Test port"));

            foreach (var mapping in await device.GetAllMappingsAsync())
            {
                Console.WriteLine(mapping);
            }
        }
    }
}