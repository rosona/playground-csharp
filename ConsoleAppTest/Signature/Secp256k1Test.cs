using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ConsoleAppTest.Utils;
using NServiceKit.Text;
using Secp256k1Net;

namespace ConsoleAppTest.Signature
{
    public class Secp256k1Test
    {
        private static readonly Random RandomInstance = new Random();
        private static readonly Secp256k1 secp256K1 = new Secp256k1();
        
        public static Dictionary<byte[], byte[]> GenKeyPairs(int count)
        {
            var keyPairs = new Dictionary<byte[], byte[]>();
            for (var i = 0; i < count; i++)
            {
                var privateKey = new byte[32];
                var rnd = RandomNumberGenerator.Create();
                rnd.GetBytes(privateKey);
                var publicKey = new byte[64];
                lock (secp256K1)
                {
                    Debug.Assert(secp256K1.PublicKeyCreate(publicKey, privateKey));
                }
                keyPairs.Add(publicKey, privateKey);
            }

            return keyPairs;
        }

        public static List<Dictionary<string, byte[]>> Signature(int count, Dictionary<byte[], byte[]> keyPairs)
        {
            var signatures = new List<Dictionary<string, byte[]>>();
            for (var i = 0; i < count; i++)
            {
                var randomString = StringUtils.RandomString(20);
                var messageBytes = Encoding.UTF8.GetBytes(randomString);
                var messageHash = SHA256.Create().ComputeHash(messageBytes);
                var signature = new byte[65];
                var publicKey = keyPairs.ElementAt(RandomInstance.Next(0, keyPairs.Count)).Key;
                lock (secp256K1)
                {
                    Debug.Assert(secp256K1.SignRecoverable(signature, messageHash, keyPairs[publicKey]));
                }
                signatures.Add(new Dictionary<string, byte[]>
                {
                    {"signature", signature},
                    {"messageHash", messageHash},
                    {"publicKey", publicKey},
                    {"privateKey", keyPairs[publicKey]}
                });
            }

            return signatures;
        }

        public static void SignatureVerify(List<Dictionary<string, byte[]>> signatures)
        {
            foreach (var signature in signatures)
            {
                var s = signature["signature"];
                var m = signature["messageHash"];
                // m[9] = "k".ToAsciiBytes()[0];
                var k = new byte[64];

                lock (secp256K1)
                {
                    Debug.Assert(secp256K1.Recover(k, s, m));
                    var ok = signature["publicKey"];
                    if (!k.SequenceEqual(ok))
                    {
                        Console.WriteLine($"NOT match pubkey");
                    }

                    //m[9] = "o".ToAsciiBytes()[0];
                    // k[8] = "8".ToAsciiBytes()[0];
                    if (!secp256K1.Verify(s, m, k))
                    {
                        Console.WriteLine("secp256K1.Verify failed.");
                    }
                    // Debug.Assert(secp256K1.Verify(s, m, k));
                }
            }
        }

        public static List<Dictionary<string, byte[]>> SignatureCon(int count, Dictionary<byte[], byte[]> keyPairs)
        {
            var signatures = new List<Dictionary<string, byte[]>>();
            var tasks = new Task[count];
            for (var i = 0; i < count; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    var randomString = StringUtils.RandomString(20);
                    var messageBytes = Encoding.UTF8.GetBytes(randomString);
                    var messageHash = SHA256.Create().ComputeHash(messageBytes);
                    var signature = new byte[65];
                    var publicKey = keyPairs.ElementAt(RandomInstance.Next(0, keyPairs.Count)).Key;
                    lock (secp256K1)
                    {
                        Debug.Assert(secp256K1.SignRecoverable(signature, messageHash, keyPairs[publicKey]));
                    }
                    signatures.Add(new Dictionary<string, byte[]>
                    {
                        {"signature", signature},
                        {"messageHash", messageHash},
                        {"publicKey", publicKey},
                        {"privateKey", keyPairs[publicKey]}
                    });
                });
            }
            Task.WaitAll(tasks);
            return signatures;
        }

        public static void SignatureVerifyCon(List<Dictionary<string, byte[]>> signatures)
        {
            var tasks = new Task[signatures.Count];
            var i = 0;
            foreach (var signature in signatures)
            {
                tasks[i++] = Task.Factory.StartNew(() =>
                {
                    var s = signature["signature"];
                    var m = signature["messageHash"];
                    lock (secp256K1)
                    {
                        var k = new byte[64];
                        Debug.Assert(secp256K1.Recover(k, s, m));
                        Debug.Assert(secp256K1.Verify(s, m, k));
                    }
                });
            }
            Task.WaitAll(tasks);
        }
    }
}