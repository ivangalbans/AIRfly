using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIRflyWebApp.AIRfly;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AIRflyWebApp.Controllers
{
    public class DataTransferController : Controller
    {
        private AIRflyService service;

        public DataTransferController(AIRflyService service)
        {
            this.service = service;
        }

        [HttpGet("/getfile/{song}")]
        public FileStreamResult GetFile(string song)
        {
            return new FileStreamResult(service.FindFile(song), "audio/mpeg");
        }



        [HttpPost("UploadFiles")]
        [RequestSizeLimit(1297286400)]
        public async Task<IActionResult> Post(List<IFormFile> files)
        {
            long size = files.Sum(f => f.Length);
            // full path to file in temp location
            var filePath = Path.GetTempFileName();
            foreach (var formFile in files)
            {
                service.SendFile(formFile);
               
            }
           
            return new RedirectToActionResult("AllMusic", "Home", new { });
        }


    }
}