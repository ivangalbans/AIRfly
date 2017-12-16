using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DHTChord.Node;
using DHTChord.NodeInstance;
using DHTChord.Server;
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
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10)));

                return ChordServer.FindServiceAddress();
            });

            return nodes;
        }

        public void SendFile(string fileName, string path)
        {
            var node = GetNodes()[0];

            var key = ChordServer.GetHash(fileName);

            Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

            var request = new FileUploadMessage();

            var fileMetadata = new FileMetaData(fileName);
            request.Metadata = fileMetadata;
            request.FileByteStream = fileStream;

            var conteinerNode = ChordServer.CallFindContainerKey(node, key);

            ChordServer.Instance(conteinerNode).AddNewFile(request);
        }

        public void FindFile(string fileName, string pathToDownload)
        {
            var node = GetNodes()[0];

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
                Download(node, fileName, pathToDownload, where == AIRfly.Download.Cache);
        }

        private static void Download(ChordNode node, string file, string pathToDownload, bool from)
        {
            var fileStream = ChordServer.Instance(node).GetStream(file, from);

            var request = new FileUploadMessage();

            var fileMetadata = new FileMetaData(file);
            request.Metadata = fileMetadata;
            request.FileByteStream = fileStream;

            FileStream outfile = null;

            outfile = new FileStream(pathToDownload + file, FileMode.Create);


            const int bufferSize = 65536; // 64K

            byte[] buffer = new byte[bufferSize];
            int bytesRead = request.FileByteStream.Read(buffer, 0, bufferSize);

            while (bytesRead > 0)
            {
                outfile.Write(buffer, 0, bytesRead);
                bytesRead = request.FileByteStream.Read(buffer, 0, bufferSize);
            }

        }

        public IEnumerable<string> GetAllFilesInSystem()
        {
            var node = GetNodes()[0];

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
}
