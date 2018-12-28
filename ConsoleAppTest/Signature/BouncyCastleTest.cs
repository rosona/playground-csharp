using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ConsoleAppTest.Utils;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace ConsoleAppTest.Signature
{
    public class BouncyCastleTest
    {
        private static readonly X9ECParameters Curve = SecNamedCurves.GetByName("secp256k1");
        private static readonly Random RandomInstance = new Random();
        private static readonly ECDomainParameters DomainParams = new ECDomainParameters(Curve.Curve, Curve.G, Curve.N, Curve.H);
        private static readonly SecureRandom SecureRandom = new SecureRandom();

        public static Dictionary<ECPublicKeyParameters, ECPrivateKeyParameters> GenKeyPairs(int count)
        {
            var keyPairs = new Dictionary<ECPublicKeyParameters, ECPrivateKeyParameters>();
            for (var i = 0; i < count; i++)
            {
                var keygenParams = new ECKeyGenerationParameters(DomainParams, SecureRandom);
                var generator = new ECKeyPairGenerator();
                generator.Init(keygenParams);

                var keypair = generator.GenerateKeyPair();

                var privParams = (ECPrivateKeyParameters) keypair.Private;
                var pubParams = (ECPublicKeyParameters) keypair.Public;
                keyPairs.Add(pubParams, privParams);
            }

            return keyPairs;
        }

        public static List<Dictionary<string, object>> Signature(int count, Dictionary<ECPublicKeyParameters, ECPrivateKeyParameters> keyPairs)
        {
            var signatures = new List<Dictionary<string, object>>();
            for (var i = 0; i < count; i++)
            {
                var keyPair = keyPairs.ElementAt(RandomInstance.Next(0, keyPairs.Count));
                var ecdsaSigner = new ECDsaSigner();
                ecdsaSigner.Init(true, new ParametersWithRandom(keyPair.Value, SecureRandom));

                var randomString = StringUtils.RandomString(20);
                var messageBytes = Encoding.UTF8.GetBytes(randomString);
                var signature = ecdsaSigner.GenerateSignature(messageBytes);

                signatures.Add(new Dictionary<string, object>
                {
                    {"signature", signature},
                    {"messageHash", messageBytes},
                    {"publicKey", keyPair.Key},
                    {"privateKey", keyPair.Value}
                });
            }

            return signatures;
        }

        public static void SignatureVerify(List<Dictionary<string, object>> signatures)
        {            
            foreach (var s in signatures)
            {
                var verifier = new ECDsaSigner();
                verifier.Init(false, (ECPublicKeyParameters) s["publicKey"]);
                var signature = (BigInteger[]) s["signature"];
                Debug.Assert(verifier.VerifySignature((byte[]) s["messageHash"], signature[0], signature[1]));
            }
        }
    }
}