using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;
using System.Security.Cryptography;
using System.Text;
using DHTChord.Node;
using DHTChord.State;

namespace DHTChord.Server
{
    public static class ChordServer
    {
        public static ChordNode LocalNode { get; set; }
        private static TcpChannel Channel { get; set; }
        public static bool RegisterService(int port)
        {
            try
            {
                if(Channel != null)
                {
                    UnregisterService();    
                }
                
                Channel = new TcpChannel(
                        new Hashtable { ["port"] = port },
                        null,
                        new BinaryServerFormatterSinkProvider { TypeFilterLevel = TypeFilterLevel.Full }
                );

                ChannelServices.RegisterChannel(Channel, false);
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(ChordState), "chord", WellKnownObjectMode.Singleton);
            }
            catch (Exception)
            {
                throw;
            }
            return true;
        }

        public static void UnregisterService()
        {
            if(Channel != null)
            {
                ChannelServices.UnregisterChannel(Channel);
                Channel = null;
            }
        }
        public static ulong GetHash(string key)
        {
            var md5 = new MD5CryptoServiceProvider();
            var bytes = Encoding.ASCII.GetBytes(key);
            return BitConverter.ToUInt64(md5.ComputeHash(bytes), 0);
        }

    }
}
