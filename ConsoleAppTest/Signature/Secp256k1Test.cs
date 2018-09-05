using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Schema;
using Secp256k1Net;

namespace ConsoleAppTest
{
    public class Secp256k1Test
    {
        public static void Test()
        {
            using (var secp256K1 = new Secp256k1())
            {
                // Generate a private key.
                var privateKey = new byte[32];
                var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
                do { rnd.GetBytes(privateKey); }
                while (!secp256K1.SecretKeyVerify(privateKey));

                // Create public key from private key.
                var publicKey = new byte[64];
                Debug.Assert(secp256K1.PublicKeyCreate(publicKey, privateKey));

                // Sign a message hash.
                var messageBytes = Encoding.UTF8.GetBytes("Hello world.");
                var messageHash = System.Security.Cryptography.SHA256.Create().ComputeHash(messageBytes);
                var signature = new byte[64];
                Debug.Assert(secp256K1.Sign(signature, messageHash, privateKey));

                // Verify message hash.
                Debug.Assert(secp256K1.Verify(signature, messageHash, publicKey));
            }
        }

        public Dictionary<byte[], byte[]> GenKeyPairs()
        {
            var keyPairs = new Dictionary<byte[], byte[]>();
            using (var secp256K1 = new Secp256k1())
            {
                var privateKey = new byte[32];
                var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
                do
                {
                    rnd.GetBytes(privateKey);
                } while (!secp256K1.SecretKeyVerify(privateKey));

                // Create public key from private key.
                var publicKey = new byte[64];
                Debug.Assert(secp256K1.PublicKeyCreate(publicKey, privateKey));
                keyPairs.Add(publicKey, privateKey);
            }
            return keyPairs;
        }

        public void Signature()
        {
            var signatures = new List<Dictionary<string, byte[]>>();
            
        }
        
        public bool VerifySignature()
        {
            
        }
        
        private static var random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

    }
}