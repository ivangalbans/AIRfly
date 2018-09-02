using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AIRflyWebApp.AIRfly;
using DHTChord.Server;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIRflyWebApp
{
    public class Program
    {
        private static readonly BackgroundWorker _updateSeedCache = new BackgroundWorker();

        public static void StartMaintenance()
        {
            _updateSeedCache.DoWork += UpdateSeedCache;
            _updateSeedCache.WorkerSupportsCancellation = true;
            _updateSeedCache.RunWorkerAsync();
        }
        private static void UpdateSeedCache(object sender, DoWorkEventArgs ea)
        {
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                Thread.Sleep(5000);
                AIRflyService.Nodes = ChordServer.FindServiceAddress();
            }
        }


        public static void Main(string[] args)
        {
            AIRflyService.Nodes = ChordServer.FindServiceAddress();
            StartMaintenance();
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