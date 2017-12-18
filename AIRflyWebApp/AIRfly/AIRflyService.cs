using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DHTChord.Node;
using DHTChord.NodeInstance;
using DHTChord.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace AIRflyWebApp.AIRfly
{
    public enum Download
    {
        Cache,
        DataBase,
        Error
    }

    public class AIRflyService
    {
        private IMemoryCache cache;

        private static readonly string cacheName = "chord_nodes";

        public AIRflyService(IMemoryCache cache)
        {
            this.cache = cache;
        }

        private List<ChordNode> GetNodes()
        {
            var nodes = cache.GetOrCreate(cacheName, e =>
            {
                e.SetOptions(new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30)));

                return ChordServer.FindServiceAddress();
            });

            return nodes;
        }

        public void SendFile(IFormFile fileStream)
        {
            var node = GetValidNode();

            try
            {
                if(node != null)
                {
                    var key = ChordServer.GetHash(fileStream.FileName);


                    var request = new FileUploadMessage();

                    var fileMetadata = new FileMetaData(fileStream.FileName);
                    request.Metadata = fileMetadata;
                    request.FileByteStream = fileStream.OpenReadStream();

                    var conteinerNode = ChordServer.CallFindContainerKey(node, key);

                    ChordServer.Instance(conteinerNode).AddNewFile(request);
                }
            }
            catch (Exception e)
            {
                DHTChord.Logger.Logger.Log(DHTChord.Logger.Logger.LogLevel.Error, "Sending file", $"Error during sending file {fileStream.FileName} {e.ToString()}");
                SendFile(fileStream);
            }
            
        }

        private ChordNode GetValidNode()
        {
            var list = GetNodes();

            ChordNode node = null;

            foreach (var n in list)
            {
                if (ChordNodeInstance.IsInstanceValid(ChordServer.Instance(n), "send file"))
                {
                    node = n;
                    break;
                }
            }

            return node;
        }

        public Stream FindFile(string fileName)
        {

            var node = GetValidNode();

            try
            {
                if(node != null)
                {
                    var where = AIRfly.Download.Error;
                    var key = ChordServer.GetHash(fileName);

                    var instance = ChordServer.Instance(node);
                    if (instance.ContainKey(key))
                    {
                        where = AIRfly.Download.DataBase;

                    }
                    else if (instance.ContainInCache(fileName))
                    {
                        where = AIRfly.Download.Cache;
                    }
                    else
                    {
                        var conteinerNodeInstance = ChordServer.Instance(ChordServer.CallFindContainerKey(node, key));

                        if (conteinerNodeInstance.ContainKey(key))
                        {

                            var fileStream = conteinerNodeInstance.GetStream(fileName, false);
                            var request = new FileUploadMessage();
                            var fileMetadata = new FileMetaData(fileName);

                            request.Metadata = fileMetadata;
                            request.FileByteStream = fileStream;

                            instance.AddCacheFile(request);
                            where = AIRfly.Download.Cache;
                        }
                    }

                    if (AIRfly.Download.Error != where)
                        return Download(node, fileName, where == AIRfly.Download.Cache);
                }
            }
            catch (Exception e)
            {
                DHTChord.Logger.Logger.Log(DHTChord.Logger.Logger.LogLevel.Error, "Find file", $"Error during finding file {fileName} {e.ToString()}");
                FindFile(fileName);
            }

            return null;
        }

        private static Stream Download(ChordNode node, string file, bool from)
        {
            return ChordServer.Instance(node).GetStream(file, from);

            //var request = new FileUploadMessage();

            //var fileMetadata = new FileMetaData(file);
            //request.Metadata = fileMetadata;
            //request.FileByteStream = fileStream;




            //const int bufferSize = 65536; // 64K

            //byte[] buffer = new byte[bufferSize];
            //int bytesRead = request.FileByteStream.Read(buffer, 0, bufferSize);

            //while (bytesRead > 0)
            //{
            //    outfile.Write(buffer, 0, bytesRead);
            //    bytesRead = request.FileByteStream.Read(buffer, 0, bufferSize);
            //}

        }

        public IEnumerable<string> GetAllFilesInSystem()
        {
            var node = GetValidNode();

            try
            {
                if(node != null)
                {
                    var initInstance = ChordServer.Instance(node);

                    SortedSet<string> result = new SortedSet<string>();

                    foreach (var item in initInstance.GetDb())
                    {
                        result.Add(item);
                    }

                    initInstance = ChordServer.Instance(initInstance.Successor);

                    while (initInstance.Id != node.Id)
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
            catch (Exception e)
            {
                DHTChord.Logger.Logger.Log(DHTChord.Logger.Logger.LogLevel.Error, "showing all files", $"Error during showing all files {e.ToString()}");
                GetAllFilesInSystem();
            }
            return new List<string>();
        }
    }
}
