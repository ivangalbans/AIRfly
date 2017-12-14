using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DHTChord.NodeInstance;
using System.IO;
using System.Net;
using DHTChord.Node;
using DHTChord.Server;


namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) args = new string[] { "Find", "009-imagine_dragons-bleeding_out.wav" };
            if (args[0] == "Upload")
            {
             

                var tmp = ChordServer.FindServiceAddress();
                var node = ChordServer.Instance(tmp[0]).LocalNode;


                string[] input = Console.ReadLine().Split(' ');
                if(input[0] == "1")//all directory
                {
                    string path = input[1];
                    var files = Directory.EnumerateFiles(path);

                    foreach (var f in files)
                    {
                        string fileName = Path.GetFileName(f);

                        ClientSide.Send(fileName, f, node);
                    }
                }
                if(input[0] == "2")//a single file
                {
                    string path = input[1];
                    string fileName = Path.GetFileName(path);

                    ClientSide.Send(fileName, path, node);
                }
            }
            if(args[0] == "Download")
            {
         

                var tmp = ChordServer.FindServiceAddress();
                var node = ChordServer.Instance(tmp[0]).LocalNode;


                string fileName = args[1];

                var result = ClientSide.Find(fileName, node);
                if (result != Download.Error)
                {
                    Console.WriteLine($"Find Succefully {fileName}");

                    string pathtoDownload = Directory.GetCurrentDirectory()+ "\\Download\\";
                    if (!Directory.Exists(pathtoDownload))
                        Directory.CreateDirectory(pathtoDownload);

                    Stream stream = null;
                    if (result == Download.Cache)
                         stream = ClientSide.Download(node, fileName, pathtoDownload, true);
                    if (result == Download.DataBase)
                        stream = ClientSide.Download(node, fileName, pathtoDownload, false);

                    stream.Position = 0;
                    System.Media.SoundPlayer player = new System.Media.SoundPlayer(stream);

                    
                    player.Play();
                    int i = 0;
                    while (i < player.Stream.Length) ;
                    
                }
                else
                {
                    Console.WriteLine($"{fileName} not found");
                }
            }
            if(args[0] == "Show")
            {
                var tmp = ChordServer.FindServiceAddress();
                var node = ChordServer.Instance(tmp[0]).LocalNode;

                var list = ClientSide.GetAllFilesInSystem(node);

                foreach (var item in list)
                {
                    Console.WriteLine(item);
                }
            }
            if(args[0] == "Listend")
            {
                //TODO:
            }
        }
    }
}
