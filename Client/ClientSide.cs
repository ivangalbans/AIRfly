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

            var key = ChordServer.GetHash(fileName);

            var instance = ChordServer.Instance(node);
            if(instance.ConteinInCache(fileName))
            {
                return true;
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
                return true;
            }


            return false;
        }

        public static Stream Download(ChordNode node, string file, string pathToDownload)
        {
            var fileStream = ChordServer.Instance(node).GetStream(file, true);

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
            return outfile;

        }

        
    }
}
