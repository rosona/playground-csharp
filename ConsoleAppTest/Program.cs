using System;
using System.Net.Mime;
using System.Reflection;
using ConsoleAppTest.base58;
using ConsoleAppTest.Javascript;
using ConsoleAppTest.Signature;

namespace ConsoleAppTest
{
    static class Program
    {
        static void Main(string[] args)
        {
            Redis.Test.Run();
//            SystemFunction.DoTest();
//            Base58Test.Do();
//            JavascriptTest.Run();
//            SignatureTest.DoTest(1000, 100);
        }
    }
}