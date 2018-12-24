using System;
using System.Diagnostics;
using System.Linq;

namespace ConsoleAppTest.Signature
{
    public static class SignatureTest
    {
        public static void DoTest(int signatureCount, int keyPairCount)
        {
            DoBouncyCastleTest(signatureCount, keyPairCount);
            DoSecp256k1Test(signatureCount, keyPairCount);
        }

        private static void DoSecp256k1Test(int signatureCount, int keyPairCount)
        {
            Console.WriteLine($">>> {DateTime.Now:MM/dd/yyyy HH:mm:ss fff} Secp256k1 Start...");

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var keyPairs = Secp256k1Test.GenKeyPairs(keyPairCount);
            stopwatch.Stop();
            Console.WriteLine($"Secp256k1::GenKeyPairsTime={stopwatch.ElapsedMilliseconds}");

            stopwatch.Restart();
            var signatures = Secp256k1Test.Signature(signatureCount, keyPairs);
            stopwatch.Stop();
            Console.WriteLine($"Secp256k1::SignatureTime={stopwatch.ElapsedMilliseconds}");

            stopwatch.Restart();
            Secp256k1Test.SignatureVerify(signatures);
            stopwatch.Stop();
            Console.WriteLine($"Secp256k1::SignatureVerifyTime={stopwatch.ElapsedMilliseconds}");

            Console.WriteLine(">>> Secp256k1 Done.");
        }

        private static void DoBouncyCastleTest(int signatureCount, int keyPairCount)
        {
            Console.WriteLine($">>> {DateTime.Now:MM/dd/yyyy HH:mm:ss fff} BouncyCastle Start...");

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var keyPairs = BouncyCastleTest.GenKeyPairs(keyPairCount);
            stopwatch.Stop();
            Console.WriteLine($"BouncyCastle::GenKeyPairsTime={stopwatch.ElapsedMilliseconds}");

            stopwatch.Restart();
            var signatures = BouncyCastleTest.Signature(signatureCount, keyPairs);
            stopwatch.Stop();
            Console.WriteLine($"BouncyCastle::SignatureTime={stopwatch.ElapsedMilliseconds}");

            stopwatch.Restart();
            BouncyCastleTest.SignatureVerify(signatures);
            stopwatch.Stop();
            Console.WriteLine($"BouncyCastle::SignatureVerifyTime={stopwatch.ElapsedMilliseconds}");

            Console.WriteLine(">>> BouncyCastle Done.");
        }

    }
}