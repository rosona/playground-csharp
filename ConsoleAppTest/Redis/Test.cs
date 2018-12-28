using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using ConsoleAppTest.Redis.RedisProtocol;
using ConsoleAppTest.Utils;
using NServiceKit.Redis;
using StackExchange.Redis;

namespace ConsoleAppTest.Redis
{
    public class Test
    {
        private const int MaxThread = 10;
        private const int MaxKeysPerThread = 5000;

        public static void Run()
        {
            NormalTest();
            // TestSimpleRedis();
            // TestNServiceKitRedis();
            // TestStackExchangeRedis();
        }

        public static void NormalTest()
        {
            var redis = new PooledRedisLite("192.168.9.9");
            var value = redis.GetString("xxxxxxx");
            Console.WriteLine($"#### {value}");
        }
        private static void TestNServiceKitRedis()
        {
            var nkRedis = new NServiceKitRedis();
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var taskArray = new Task[MaxThread];
            for (var i = 0; i < taskArray.Length; i++)
            {
                taskArray[i] = Task.Factory.StartNew(TestNServiceKitRedisWorker, nkRedis);
            }
            Task.WaitAll(taskArray);
            stopWatch.Stop();
            Console.WriteLine($"NServiceKitRedis::Thread {MaxThread}, spend: {stopWatch.ElapsedMilliseconds} ms.");
        }

        private static void TestNServiceKitRedisWorker(object nkRedis)
        {
            for (var i = 0; i < MaxKeysPerThread; i++)
            {
                var key = StringUtils.RandomString(30);
                var value = StringUtils.RandomString(20);
                ((NServiceKitRedis)nkRedis).Set(key, Encoding.ASCII.GetBytes(value));
                var getValue = ((NServiceKitRedis)nkRedis).Get(key);
                if (getValue != value)
                {
                    Console.WriteLine($"TestNServiceKitRedisWorker::Error: {key}: {value} != {getValue}");
                }
            }
        }

        private static void TestSimpleRedis()
        {
            var redis = new PooledRedisLite("192.168.9.9");
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var taskArray = new Task[MaxThread];
            for (var i = 0; i < taskArray.Length; i++)
            {
                taskArray[i] = Task.Factory.StartNew(TestSimpleRedisWorker, redis);
            }
            Task.WaitAll(taskArray);
            stopWatch.Stop();
            Console.WriteLine($"SimpleRedis::Thread {MaxThread}, spend: {stopWatch.ElapsedMilliseconds} ms.");
        }

        private static void TestSimpleRedisWorker(object redis)
        {
            for (var i = 0; i < MaxKeysPerThread; i++)
            {
                var key = StringUtils.RandomString(30);
                var value = StringUtils.RandomString(20);
                ((PooledRedisLite)redis).Set(key, Encoding.UTF8.GetBytes(value));
                var getValue = ((PooledRedisLite)redis).GetString(key);
                if (getValue != value)
                {
                    Console.WriteLine($"TestSimpleRedisWorker::Error: {key}: {value} != {getValue}");
                }
            }
        }

        private static void TestStackExchangeRedis()
        {
            var seRedis = new StackExchangeRedis();
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var taskArray = new Task[MaxThread];
            for (var i = 0; i < taskArray.Length; i++)
            {
                taskArray[i] = Task.Factory.StartNew(TestStackExchangeRedisWorker, seRedis);
            }
            Task.WaitAll(taskArray);
            stopWatch.Stop();
            Console.WriteLine($"StackExchangeRedis::Thread {MaxThread}, spend: {stopWatch.ElapsedMilliseconds} ms.");
        }

        private static void TestStackExchangeRedisWorker(object seRedis)
        {
            for (var i = 0; i < MaxKeysPerThread; i++)
            {
                var key = StringUtils.RandomString(30);
                var value = StringUtils.RandomString(20);
                ((StackExchangeRedis)seRedis).Set(key, value);
                var getValue = ((StackExchangeRedis)seRedis).Get(key);
                if (getValue != value)
                {
                    Console.WriteLine($"TestStackExchangeRedisWorker::Error: {key}: {value} != {getValue}");
                }
            }
        }
    }

    public class NServiceKitRedis
    {
        PooledRedisClientManager _client = new PooledRedisClientManager("192.168.9.9:6379");

        public void Set(string key, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("key is empty");
            }

            _client.GetCacheClient().Set(key, bytes);
        }

        public string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("key is empty");
            }

            return _client.GetCacheClient().Get<string>(key);
        }

        public void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("key is empty");
            }

            _client.GetCacheClient().Remove(key);
        }

        public bool PipelineSet(Dictionary<string, byte[]> cache)
        {
            if (cache.Count == 0)
            {
                return true;
            }

            _client.GetCacheClient().SetAll(cache);
            return true;
        }
    }

    public class StackExchangeRedis
    {
        static readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect("192.168.9.9");

        public void Set(string key, string value)
        {
            _redis.GetDatabase(0).StringSet(key, value);
        }

        public string Get(string key)
        {
            return _redis.GetDatabase(0).StringGet(key);
        }
    }
}