using System;
using System.Text;

using DHTChord.Node;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;
using System.Collections;
using System.Security.Cryptography;
using DHTChord.NodeInstance;

namespace DHTChord.Server
{
    public static class ChordServer
    {
        public static ChordNode LocalNode { get; set; }
        private static TcpChannel channel { get; set; }
        public static bool RegisterService(int Port)
        {
            try
            {
                if(channel != null)
                {
                    UnregisterService();    
                }
                
                channel = new TcpChannel(
                        new Hashtable { ["port"] = Port },
                        null,
                        new BinaryServerFormatterSinkProvider() { TypeFilterLevel = TypeFilterLevel.Full }
                );

                ChannelServices.RegisterChannel(channel, false);
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(ChordNodeInstance), "chord", WellKnownObjectMode.Singleton);
            }
            catch (Exception e)
            {
                throw e;
            }
            return true;
        }

        public static void UnregisterService()
        {
            if(channel != null)
            {
                ChannelServices.UnregisterChannel(channel);
                channel = null;
            }
        }
        public static ulong GetHash(string key)
        {
            var md5 = new MD5CryptoServiceProvider();
            byte[] bytes = Encoding.ASCII.GetBytes(key);
            return BitConverter.ToUInt64(md5.ComputeHash(bytes), 0);
        }

    }
}
