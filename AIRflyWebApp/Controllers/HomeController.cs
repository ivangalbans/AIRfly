using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AIRflyWebApp.AIRfly;
using Microsoft.AspNetCore.Mvc;
using AIRflyWebApp.Models;

namespace AIRflyWebApp.Controllers
{
    public class HomeController : Controller
    {
        private AIRflyService service;

        public HomeController(AIRflyService service)
        {
            this.service = service;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

           // service.FindFile("text.txt", @"D:\_3000-Down\");

            var a = service.GetAllFilesInSystem();
            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            var directory = Directory.EnumerateFiles(@"D:\toSend");
           

            return View();
        }
        public IActionResult AllMusic()
        {
            ViewBag.songs = service.GetAllFilesInSystem();
            
            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
