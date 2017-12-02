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
using static DHTChord.Logger.Logger;
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
                        new BinaryServerFormatterSinkProvider() { TypeFilterLevel = TypeFilterLevel.Full }
                );

                ChannelServices.RegisterChannel(Channel, false);
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(ChordNodeInstance), "chord", WellKnownObjectMode.Singleton);
            }
            catch (Exception e)
            {
                Log("Configuration", $"Unable to register Chord Service ({e.Message}).");
                return false;
            }
            Log("Configuration", $"Chord Service registered on port {port}.");

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
            byte[] bytes = Encoding.ASCII.GetBytes(key);
            return BitConverter.ToUInt64(md5.ComputeHash(bytes), 0);
        }

    }
}
