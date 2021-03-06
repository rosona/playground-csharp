﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppTest.Utils;
using Secp256k1Net;

namespace ConsoleAppTest.Signature
{
    public class Secp256k1Test
    {
        private static readonly Random RandomInstance = new Random();
        private static readonly Secp256k1 secp256K1 = new Secp256k1();
        private static ReaderWriterLock _rwLock = new ReaderWriterLock();

        public static Dictionary<byte[], byte[]> GenKeyPairs(int count)
        {
            var keyPairs = new Dictionary<byte[], byte[]>();
            for (var i = 0; i < count; i++)
            {
                var privateKey = new byte[32];
                var rnd = RandomNumberGenerator.Create();
                rnd.GetBytes(privateKey);
                var publicKey = new byte[64];
                // using (var secp256K1 = new Secp256k1())
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
                // using (var secp256K1 = new Secp256k1())
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
            var taskArray = new List<Task>();
            foreach (var signature in signatures)
            {
                var s = new Dictionary<string, byte[]>(signature);
                taskArray.Add(Task.Factory.StartNew(DoVerify, s));
            }

            Task.WaitAll(taskArray.ToArray());
        }

        public static void DoVerify(object data)
        {
            _rwLock.AcquireWriterLock(Int32.MaxValue);
            var signature = data as Dictionary<string, byte[]>;
            var s = signature?["signature"].ToArray();
            var m = signature?["messageHash"].ToArray();
            var k = new byte[64];

            Debug.Assert(secp256K1.Recover(k, s, m));
            Debug.Assert(secp256K1.Verify(s, m, k));
            _rwLock.ReleaseWriterLock();
        }

        public static List<Dictionary<string, byte[]>> SignatureConcurrent(int count, Dictionary<byte[], byte[]> keyPairs)
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
                    // using (var secp256K1 = new Secp256k1())
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

        public static void SignatureVerifyConcurrent(List<Dictionary<string, byte[]>> signatures)
        {
            var tasks = new Task[signatures.Count];
            var i = 0;
            foreach (var signature in signatures)
            {
                tasks[i++] = Task.Factory.StartNew(() =>
                {
                    var s = signature["signature"];
                    var m = signature["messageHash"];
                    var k = new byte[64];
                    // using (var secp256K1 = new Secp256k1())
//                    lock (secp256K1)
                    {
                        Debug.Assert(secp256K1.Recover(k, s, m));
                        Debug.Assert(secp256K1.Verify(s, m, k));
                    }
                });
            }

            Task.WaitAll(tasks);
        }
    }
}