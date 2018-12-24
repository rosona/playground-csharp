using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Base58Check;
using ConsoleAppTest.Utils;

namespace ConsoleAppTest.base58
{
    public static class Base58Test
    {
        public static void Do()
        {
            for (var i = 0; i < 100; i++)
            {
                BigInteger randomBigInt = i;
                var randomBytes = new byte[] {0x00, 0x00, 0x00, 0x00};
                var bigIntByteArray = randomBigInt.ToByteArray();
                if (bigIntByteArray.Length == 1)
                {
                    randomBytes[3] = bigIntByteArray[0];
                }

                if (bigIntByteArray.Length == 2)
                {
                    randomBytes[3] = bigIntByteArray[0];
                    randomBytes[2] = bigIntByteArray[1];
                }

                if (bigIntByteArray.Length == 3)
                {
                    randomBytes[3] = bigIntByteArray[0];
                    randomBytes[2] = bigIntByteArray[1];
                    randomBytes[1] = bigIntByteArray[2];
                }

                var base58String = Base58CheckEncoding.EncodePlain(randomBytes);
                Console.WriteLine($"Bytes: {string.Concat(randomBytes.Select(b => b.ToString("X2")))} Byte length: {bigIntByteArray.Length}, {base58String}");
            }
        }

        public static void Do4()
        {
            for (var i = 195112; i < 11316496; i++)
            {
                BigInteger randomBigInt = i;
                var randomBytes = new byte[3];
                var bigIntByteArray = randomBigInt.ToByteArray();
                randomBytes[0] = bigIntByteArray[2];
                randomBytes[1] = bigIntByteArray[1];
                randomBytes[2] = bigIntByteArray[0];
                var base58String = Base58CheckEncoding.EncodePlain(randomBytes);
                if (base58String.Length != 4)
                {
                    Console.WriteLine($"Bytes: {string.Concat(randomBytes.Select(b => b.ToString("X2")))} Byte length: {randomBytes.Length}, {base58String}");
                }
            }
        }

        public static void Do3()
        {
            for (var i = 0; i < 10000; i++)
            {
                var random = new Random();
                BigInteger randomBigInt = random.Next(195112, 11316495);
                var randomBytes = new byte[3];
                var bigIntByteArray = randomBigInt.ToByteArray();
                randomBytes[0] = bigIntByteArray[2];
                randomBytes[1] = bigIntByteArray[1];
                randomBytes[2] = bigIntByteArray[0];
                var base58String = Base58CheckEncoding.EncodePlain(randomBytes);
                Console.WriteLine($"Bytes: {string.Concat(randomBytes.Select(b => b.ToString("X2")))} Byte length: {randomBytes.Length}, {base58String}");
            }
        }
        
        public static void Do2()
        {
            var lengths = new Dictionary<int, List<string>>();

            for (var i = 0; i < 100; i++)
            {
                var b = ByteUtils.RandomBytes(4);
                Console.WriteLine(BitConverter.ToString(b).Replace("-", string.Empty));

                var byteString = Base58CheckEncoding.EncodePlain(b);
                var length = byteString.Length;

                // Console.WriteLine(byteString);
                if (lengths.ContainsKey(length))
                {
                    lengths[length].Append(byteString);
                }
                else
                {
                    lengths.Add(length, new List<string> {byteString});
                }
            }

            foreach (var v in lengths.Keys)
            {
                Console.WriteLine($"{v}: {lengths[v][0]}");
            }

            foreach (var length in lengths)
            {
                Console.WriteLine($"Kvp - [length, occurrences] {length}.");
            }
        }
    }
}