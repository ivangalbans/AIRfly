using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIRflyWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            var ips = Dns.GetHostAddresses(Dns.GetHostName())
                        .Where(ip => !(ip.IsIPv6LinkLocal))
                        .Select(ip => $"http://{ip.ToString()}:80")
                        .Where(ip => !ip.EndsWith(".1:80"))
                        .Distinct()
                        .ToList();
            ips.Add("http://localhost:12465");

            return WebHost.CreateDefaultBuilder(args)
                     .UseStartup<Startup>()
                     .UseUrls(ips.ToArray())
                     .Build();
        }
    }
}