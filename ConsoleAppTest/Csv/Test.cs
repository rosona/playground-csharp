using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks.Dataflow;
using CsvHelper;
using Newtonsoft.Json.Linq;

namespace ConsoleAppTest.Csv
{
    public class Test
    {
        public class MyClass
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
        
        public static void Write()
        {
            var csvRecords = new List<object>();
            var header = new 
            {
                Address = "Address",
                Count = "Count",
                EmailList = "EmailList"
            };

            csvRecords.Add(header);
            csvRecords.Add(new
            {
                Address = "Address333",
                Count = "Count3333",
                EmailList = "EmailList333"
            });
            using(TextWriter writer = File.CreateText("/Users/peng/Downloads/test.csv"))
            using (var csv = new CsvWriter(writer))
            {
                csv.WriteRecords(csvRecords);
                csv.Flush();
            }
        }
    }
}