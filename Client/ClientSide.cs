using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DHTChord.Server;
using System.IO;
using System.Threading;
using DHTChord.Logger;
using DHTChord.NodeInstance;
using DHTChord.Node;


namespace Client
{
    public enum Download
    {
        Cache,
        DataBase,
        Error
    }
    public static class ClientSide
    {
        public static void Send(string fileName, string path, ChordNode node)
        {
            var key = ChordServer.GetHash(fileName);

            Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

            var request = new FileUploadMessage();

            var fileMetadata = new FileMetaData(fileName);
            request.Metadata = fileMetadata;
            request.FileByteStream = fileStream;

            var conteinerNode = ChordServer.CallFindContainerKey(node,key);

            ChordServer.Instance(conteinerNode).AddNewFile(request);            
        }

        public static void Find(string fileName, ChordNode node, string pathToDownload)
        {
            var where = Client.Download.Error;
            var key = ChordServer.GetHash(fileName);

            var instance = ChordServer.Instance(node);
            if(instance.ContainKey(key))
            {
                where = Client.Download.DataBase;
            }

            if(instance.ConteinInCache(fileName))
            {
                where = Client.Download.Cache;
            }

            var conteinerNodeInstance = ChordServer.Instance(ChordServer.CallFindContainerKey(node, key));

            

            if(conteinerNodeInstance.ContainKey(key))
            {

                var fileStream = conteinerNodeInstance.GetStream(fileName,false);

                var request = new FileUploadMessage();


                var fileMetadata = new FileMetaData(fileName);
                request.Metadata = fileMetadata;
                request.FileByteStream = fileStream;

                var nodeInstance = ChordServer.Instance(node);
                nodeInstance.AddCacheFile(request);
                where = Client.Download.Cache;
            }


            if (Client.Download.Error != where)
            {
                if (where == Client.Download.Cache)
                    Download(node, fileName,pathToDownload, true);
                else
                    Download(node, fileName, pathToDownload,false);
            }
        }

        public static void Download(ChordNode node, string file, string pathToDownload, bool from)
        {
            var fileStream = ChordServer.Instance(node).GetStream(file, from);

            var request = new FileUploadMessage();

            var fileMetadata = new FileMetaData(file);
            request.Metadata = fileMetadata;
            request.FileByteStream = fileStream;

            FileStream outfile = null;

            outfile = new FileStream(pathToDownload + file , FileMode.Create);


            const int bufferSize = 65536; // 64K

            byte[] buffer = new byte[bufferSize];
            int bytesRead = request.FileByteStream.Read(buffer, 0, bufferSize);

            while (bytesRead > 0)
            {
                outfile.Write(buffer, 0, bytesRead);
                bytesRead = request.FileByteStream.Read(buffer, 0, bufferSize);
            }

        }

        public static IEnumerable<string> GetAllFilesInSystem(ChordNode node)
        {
            var initInstance = ChordServer.Instance(node);

            SortedSet<string> result = new SortedSet<string>();

            foreach (var item in initInstance.GetDb())
            {
                result.Add(item);
            }

            initInstance = ChordServer.Instance(initInstance.Successor);

            while(initInstance.Id != node.Id)
            {
                foreach (var item in initInstance.GetDb())
                {
                    result.Add(item);
                }

                initInstance = ChordServer.Instance(initInstance.Successor);
            }

            return result;
        }
    }

    public class ClientInstance
    {
        private readonly BackgroundWorker _findServers = new BackgroundWorker();

        private List<ChordNode> _nodeCache = new List<ChordNode>();

        public ClientInstance()
        {
            _nodeCache = ChordServer.FindServiceAddress();
            _findServers.DoWork += Discover;
            _findServers.WorkerSupportsCancellation = true;
            _findServers.RunWorkerAsync();
        }

        public void Start()
        {
            while (true)
            {
                var o = Console.ReadLine()?.Split(' ');
                if (o != null && o[0] == "download")
                {
                    ClientSide.Find(o[1],_nodeCache[0],Directory.GetCurrentDirectory()+ "\\Download\\");
                    Logger.Log(Logger.LogLevel.Info, "Download Finish", $"Download a new file {o[1]}");
                }
                else if (o != null && o[0] == "upload")
                {
                    if (o[1] == "-d")
                    {
                        var directory = Directory.EnumerateFiles(o[2]);
                        foreach (var file in directory)
                        {
                            string fileName = Path.GetFileName(file);
                            ClientSide.Send(fileName, file, _nodeCache[0]);                            
                        }
                        Logger.Log(Logger.LogLevel.Info, "Upload Finish", $"Upload a new directory {o[2]}");

                    }
                    else if (o[1] == "-f")
                    {
                        string path = o[2];
                        string fileName = Path.GetFileName(path);

                        ClientSide.Send(fileName, path, _nodeCache[0]);
                        Logger.Log(Logger.LogLevel.Info, "Upload Finish", $"Upload a new file {fileName}");

                    }
                }
                else if (o != null && o[0] == "show")
                {
                    var result = ClientSide.GetAllFilesInSystem(_nodeCache[0]);
                    foreach (var file in result)
                    {
                        Console.WriteLine(file);
                    }

                }
            }
        }

        private void Discover(object sender, DoWorkEventArgs ea)
        {
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                Thread.Sleep(10000);
                _nodeCache= ChordServer.FindServiceAddress();
            }
        }


    }
}
