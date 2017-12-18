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
        public static List<ChordNode> Nodes;
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

                return Nodes;
                return ChordServer.FindServiceAddress();
            });

            return nodes;
        }

        public void SendFile(IFormFile file)
        {
            var node = GetValidNode();

            try
            {
                if(node != null)
                {
                    var key = ChordServer.GetHash(file.FileName);


                    var request = new FileUploadMessage();

                    var fileMetadata = new FileMetaData(file.FileName);
                    request.Metadata = fileMetadata;
                    request.FileByteStream = file.OpenReadStream();

                    var conteinerNode = ChordServer.CallFindContainerKey(node, key);

                    ChordServer.Instance(conteinerNode).AddNewFile(request);
                }
            }
            catch (Exception e)
            {
                DHTChord.Logger.Logger.Log(DHTChord.Logger.Logger.LogLevel.Error, "Sending file", $"Error during sending file {file.FileName} {e.ToString()}");
                SendFile(file);
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
