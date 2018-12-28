using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleAppTest.SystemFunction
{
    
    public static class AsyncTest
    {
        private class Person
        {
            public string FirstName;
            public readonly string LastName;

            public Person(string firstName, string lastName)
            {
                FirstName = firstName;
                LastName = lastName;
            }

            public Person Clone()
            {
                return new Person(FirstName, LastName);
            }
        }

        private static readonly List<Person> Persons = new List<Person>();
        public static void DoTest()
        {
            Persons.Add(new Person("peng", "rong"));
            var person = GetPerson();

            Persons[0].FirstName = "Lina";
            Console.WriteLine(person.FirstName + " " + person.LastName);

            Thing1().Wait();
        }

        private static Person GetPerson()
        {
            return Persons[0].Clone();
        }
        
        private static async Task Thing1()
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} Before Thing11");

            await Thing11();
            
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} After Thing11");
        }

        private static async Task Thing11()
        {
            await Task.Factory.StartNew(() =>
            {
                Thread.Sleep(5000);
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} Do Thing 1");
                Thread.Sleep(5000);
            });
        }
    }
}