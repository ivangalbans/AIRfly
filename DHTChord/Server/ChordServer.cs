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
            var bytes = Encoding.ASCII.GetBytes(key);
            return BitConverter.ToUInt64(md5.ComputeHash(bytes), 0);
        }

        /// <summary>
        /// Convenience function to call FindSuccessor using ChordServer.LocalNode as the
        /// "remote" node.
        /// </summary>
        /// <param name="id"> The ID to look up (ChordServer.LocalNode is used as the remoteNode).</param>
        /// <returns>The Successor of ID, or NULL in case of error.</returns>
        public static ChordNode CallFindSuccessor(UInt64 id)
        {
            return CallFindSuccessor(ChordServer.LocalNode, id);
        }

        /// <summary>
        /// Calls FindSuccessor() remotely, using a default retry value of three
        /// </summary>
        /// <param name="remoteNode">The remote on which to call the method.</param>
        /// <param name="id">The ID to look up.</param>
        /// <returns>The Successor of ID, or NULL in case of error.</returns>
        public static ChordNode CallFindSuccessor(ChordNode remoteNode, UInt64 id)
        {
            return CallFindSuccessor(remoteNode, id, 3);
        }

        /// <summary>
        /// Calls FindSuccessor() remotely, using a default retry value of three.
        /// </summary>
        /// <param name="remoteNode">The remote node on which to call FindSuccessor().</param>
        /// <param name="id">The ID to look up.</param>
        /// <param name="retryCount">The number of times to retry the operation in case of error.</param>
        /// <param name="hopCountIn">The known hopcount prior to calling FindSuccessor on this node.</param>
        /// <param name="hopCountOut">The total hopcount of this operation (either returned upwards, or reported for hopcount efficiency validation).</param>
        /// <returns>The Successor of ID, or NULL in case of error.</returns>
        public static ChordNode CallFindSuccessor(ChordNode remoteNode, UInt64 id, int retryCount)
        {
            ChordNodeInstance instance = ChordNode.Instance(remoteNode);

            try
            {
                return instance.FindSuccessor(id);
            }
            catch (System.Exception ex)
            {
                Log("Remote Invoker", $"CallFindSuccessor error: {ex.Message}");

                if (retryCount > 0)
                {
                    return CallFindSuccessor(remoteNode, id, --retryCount);
                }
                else
                {
                    return null;
                }
            }
        }


        /// <summary>
        /// Calls AddKey() remotely, using a default retry value of three.
        /// </summary>
        /// <param name="remoteNode">The remote on which to call the method.</param>
        /// <param name="value">The string value to add.</param>
        public static void CallAddKey(ChordNode remoteNode, string value)
        {
            CallAddKey(remoteNode, value, 3);
        }

        /// <summary>
        /// Calls AddKey remotely.
        /// </summary>
        /// <param name="remoteNode">The remote node on which to call AddKey.</param>
        /// <param name="value">The string value to add.</param>
        /// <param name="retryCount">The number of retries to attempt.</param>
        public static void CallAddKey(ChordNode remoteNode, string value, int retryCount)
        {
            ChordNodeInstance instance = ChordNode.Instance(remoteNode);

            try
            {
                instance.AddKey(value);
            }
            catch (System.Exception ex)
            {
                Log("Remote Invoker", $"CallAddKey error: {ex.Message}");

                if (retryCount > 0)
                {
                    CallAddKey(remoteNode, value, --retryCount);
                }
                else
                {
                    Log("Remote Invoker", $"CallAddKey failed - error: {ex.Message}");
                }
            }
        }

    }
}
