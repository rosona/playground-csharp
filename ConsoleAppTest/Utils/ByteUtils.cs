using System;

namespace ConsoleAppTest.Utils
{
    public static class ByteUtils
    {
        public static byte[] RandomBytes(int length)
        {
            var rnd = new Random();
            var hash = new byte[length];
            rnd.NextBytes(hash);
            return hash;
        }
    }
}