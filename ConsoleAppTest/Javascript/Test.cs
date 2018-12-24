using System;
using System.IO;
using ChakraCore.NET;
using Jint;
using Jint.Native;

namespace ConsoleAppTest.Javascript
{
    public class JavascriptTest
    {
        public static int Print(string message)
        {
            Console.WriteLine(message);
            return 0;
        }

        private static void ChakraCoreTest()
        {
            var runtime = ChakraRuntime.Create();
            var context = runtime.CreateContext(true);
            context.GlobalObject.Binding.SetFunction<string, int>("print", Print); //js: function add(v){[native code]}
            context.RunScript(File.ReadAllText("/Users/peng/Downloads/test.js"));
            context.RunScript("var console = {};console.log = function(message, t){print(message)};console.error = function(message, t){print(message)};");
            context.RunScript("console.log('test');");
            var x = context.GlobalObject.CallFunction<int, int>("test", 4);
            Console.WriteLine(x);
        }

        private static void ChakraCoreTestForWeb3()
        {
            var runtime = ChakraRuntime.Create();
            var context = runtime.CreateContext(true);
            context.GlobalObject.Binding.SetFunction<string, int>("print", Print); //js: function add(v){[native code]}
            context.RunScript(File.ReadAllText("/Users/peng/Downloads/bignumber.js"));
            context.RunScript(File.ReadAllText("/Users/peng/Downloads/web3.min.js"));
            context.RunScript("var console = {}; console.log = function(message, t){print(message)};console.error = function(message, t){print(message)};");
            context.RunScript("console.log('test');");
            context.RunScript("var Web3 = require('web3');var web3 = new Web3(); console.log(web3.toWei(20, 'ether'));");
            // Console.WriteLine(x);
        }

        private static void ChakraCoreTestForAElf()
        {
            var runtime = ChakraRuntime.Create();
            var context = runtime.CreateContext(true);
            context.GlobalObject.Binding.SetFunction<string, int>("print", Print); //js: function add(v){[native code]}
            context.RunScript(File.ReadAllText("/Users/peng/Downloads/aelf.js"));
            // context.RunScript(File.ReadAllText("/Users/peng/Downloads/test.js"));
            // context.RunScript("function test(a){console.log('received ' + a); return a+a;}");
            context.RunScript("var console = {}; console.log = function(message, t){print(message)}; console.error = function(message, t){print(message)};");
            context.RunScript("var Aelf = require('aelf'); var aelf = new Aelf(new Aelf.providers.HttpProvider('http://localhost:8000/chain')); aelf.chain.connectChain(); console.log(aelf.isConnected());");
            // var x = context.GlobalObject.CallFunction<string, string>("salt", "xxx");
            // Console.WriteLine(x);
        }

        private static void JintTestForWeb3()
        {
            var engine = new Engine();

            JsValue require(string fileName)
            {
                var jsSource = File.ReadAllText(fileName);
                var res = engine.Execute(jsSource).GetCompletionValue();
                return res;
            }

            engine.SetValue("require", new Func<string, JsValue>(require))
                .SetValue("log", new Action<object>(Console.WriteLine));

            engine.Execute(@"require('/Users/peng/Downloads/bignumber.js')").GetCompletionValue();
            engine.Execute(@"require('/Users/peng/Downloads/web3.js')").GetCompletionValue();
            engine.Execute(@"var console = {}; console.log = test; console.error = log;");
            engine.Execute(@"console.log(1, 3);");
            engine.Execute(@"var Web3 = require('web3');var web3 = new Web3(); log(web3.toWei(20, 'ether'));");
        }

        private static void JintTestForAElf()
        {
            var engine = new Engine();

            JsValue require(string fileName)
            {
                var jsSource = File.ReadAllText(fileName);
                var res = engine.Execute(jsSource).GetCompletionValue();
                return res;
            }

            engine.SetValue("require", new Func<string, JsValue>(require))
                .SetValue("log", new Action<string>(Console.WriteLine));

            engine.Execute(@"require('/Users/peng/workspace/github.com/AElfProject/AElf/AElf.CLI2/Scripts/aelf.js')").GetCompletionValue();
            engine.Execute(@"var console = {}; console.log = function(message, t){log(message)}; console.error = function(message, t){log(message)};");
            engine.Execute(@"console.log(1, 3);");
        }

        public static void Run()
        {
//            ChakraCoreTestForWeb3();
//            ChakraCoreTestForAElf();
            JintTestForAElf();
        }
    }
}