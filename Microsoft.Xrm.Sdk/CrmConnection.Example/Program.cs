using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrmConnection.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Connecting to crm...");

            // this example shows how to use existing user auth (no need to explicitly state user/pass). Take a look at the overloaded methods for explicit user/pass auth
            var osm = new OrganizationServiceManager(ConfigurationManager.AppSettings["crm.sdkurl.org"]);

            // test the connection first
            var response = osm.GetProxy().Execute(new WhoAmIRequest()) as WhoAmIResponse;
            Console.WriteLine(response.UserId);

            // lets look at doing 100 retrieves in parallel properly :)
            Console.WriteLine("starting to execute 100 retrives...");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            Task.Run(() =>
            {
                List<Task> tasks = new List<Task>();

                // loop through and create a bunch of things we want to execute asynchornously
                for (int i = 0; i < 100; i++)
                {
                    Task<EntityCollection> m = TestRetrieveMultiple(osm, i);

                    tasks.Add(m); // add the task to our list
                }

                Task.WaitAll(tasks.ToArray());
            }).Wait();

            sw.Stop();
            Console.WriteLine($"done!, took {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / 100m:N2}ms per retrieve)");
        }

        private static Task<EntityCollection> TestRetrieveMultiple(OrganizationServiceManager osm, int i)
        {
            QueryExpression qe = new QueryExpression("account") { ColumnSet = new ColumnSet("accountid", "name") };
            qe.PageInfo = new PagingInfo() { Count = i * 100, PageNumber = 1 };

            // this is the important part - using the OrganizationServiceManager, we'll call GetProxy, this creates a clone of the original connection. this is what allows us to execute stuff in parallel...
            Task<EntityCollection> m = osm.GetProxy().RetrieveMultipleAsync(qe); // the *Async methods are defined inside the OptimizedConnectionExtensions.cs

            return m;
        }
    }
}
