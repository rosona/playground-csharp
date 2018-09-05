using ConsoleAppTest.Signature;

namespace ConsoleAppTest
{
    static class Program
    {
        static void Main(string[] args)
        {
            SignatureTest.DoTest(100000, 1000);
        }
    }
}