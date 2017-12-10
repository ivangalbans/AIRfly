using DHTChord.Node;

namespace DHTChord.InitServices
{
    public static class StartClient
    {
        const int BufferSize = 1024;

        public static void Send(string path, ChordNode remoteNode)
        {
            //Console.WriteLine(path);
            //int last = path.LastIndexOf('/') + 1;
            //string musicName = path.Substring(last);

            //Log(LogLevel.Info, "Sending Music", $"New Music {musicName}");
            //FileStream Fs = new FileStream(path, FileMode.Open, FileAccess.Read);


            ////byte[] buffer = new byte[4];
            //byte[] buffer = new byte[Fs.Length];
            //Fs.Read(buffer, 0, buffer.Length);
            //Fs.Close();
            //CallAddMusic(remoteNode, musicName, buffer);

            //Log(LogLevel.Info, "Finish Sending", $"File Sendig succefuly {musicName}");
            
        }
    }
}
