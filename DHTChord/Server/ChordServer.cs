using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DHTChord.Node;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;
using System.Collections;
using DHTChord.State;
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
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(ChordState), "chord", WellKnownObjectMode.Singleton);
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

    }
}
