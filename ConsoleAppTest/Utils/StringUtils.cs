using System;
using System.Linq;

namespace ConsoleAppTest.Utils
{
    public static class StringUtils
    {
        private static readonly Random RandomInstance = new Random();
        private const string SeedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string RandomString(int length)
        {
            return new string(Enumerable.Repeat(SeedChars, length)
                .Select(s => s[RandomInstance.Next(s.Length)]).ToArray());
        }
    }
}