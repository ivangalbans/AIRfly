using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DHTChord.Server;
using System.IO;
using DHTChord.NodeInstance;
using DHTChord.Node;


namespace Client
{
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

        public static bool Find(string fileName, ChordNode node)
        {

            //var key = ChordServer.GetHash(fileName);

            //var conteinerNodeInstance = ChordServer.Instance(ChordServer.CallFindContainerKey(node, key));


            //if(conteinerNodeInstance.ContainKey(key))
            //{
            //    var request = conteinerNodeInstance.GetRequest(fileName);

            //    ChordServer.Instance(node).AddCacheFile(request);
            //    return true;
            //}

            return false;
        }
    }
}
