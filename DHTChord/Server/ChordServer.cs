using System;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;
using System.Collections;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Channels;
using DHTChord.Node;
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
                Log(LogLevel.Error, "Configuration", $"Unable to register Chord Service ({e.Message}).");
                return false;
            }
            Log(LogLevel.Info, "Configuration", $"Chord Service registered on port {port}.");

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
        public static ChordNode CallFindSuccessor(ChordNode node, ulong id, int retryCount)
        {
            var instance =Instance(node);

            while (retryCount-- > 0)
            {
                try
                {
                    return instance.FindSuccessor(id);
                }
                catch (Exception e)
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallFindSuccessor error: {e.Message}");
                }
            }
            return null;
        }

        #region Storage

        /// <summary>
        /// Calls AddKey() remotely, using a default retry value of three.
        /// </summary>
        /// <param name="remoteNode">The remote on which to call the method.</param>
        /// <param name="value">The string value to add.</param>
        public static void CallAddValue(ChordNode remoteNode, string value)
        {
            CallAddValue(remoteNode, value, 3);
        }

        /// <summary>
        /// Calls AddKey remotely.
        /// </summary>
        /// <param name="remoteNode">The remote node on which to call AddKey.</param>
        /// <param name="value">The string value to add.</param>
        /// <param name="retryCount">The number of retries to attempt.</param>
        public static void CallAddValue(ChordNode remoteNode, string value, int retryCount)
        {
            var instance =Instance(remoteNode);

            try
            {
                instance.AddValue(value);
                return;
            }
            catch (System.Exception ex)
            {
                Log(LogLevel.Debug, "Remote Invoker", $"CallAddKey error: {ex.Message}");

                if (retryCount > 0)
                {
                    CallAddValue(remoteNode, value, --retryCount);
                }
                else
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallAddKey failed - error: {ex.Message}");
                }
            }
        }


        /// <summary>
        /// Calls FindKey() remotely, using a default retry value of three.
        /// </summary>
        /// <param name="remoteNode">The remote on which to call the method.</param>
        /// <param name="key">The key to look up.</param>
        /// <returns>The value corresponding to the key, or empty string if not found.</returns>
        public static string CallGetValue(ChordNode remoteNode, ulong key, out ChordNode nodeOut)
        {
            return CallGetValue(remoteNode, key, 3, out nodeOut);
        }


        /// <summary>
        /// Calls FindKey remotely.
        /// </summary>
        /// <param name="remoteNode">The remote node on which to call FindKey.</param>
        /// <param name="key">The key to look up.</param>
        /// <param name="retryCount">The number of retries to attempt.</param>
        /// <returns>The value corresponding to the key, or empty string if not found.</returns>
        public static string CallGetValue(ChordNode remoteNode, ulong key, int retryCount, out ChordNode nodeOut)
        {
            var instance = Instance(remoteNode);

            try
            {
                return instance.GetValue(key, out nodeOut);
            }
            catch (System.Exception ex)
            {
                Log(LogLevel.Debug, "Remote Invoker", $"CallFindKey error: {ex.Message}");

                if (retryCount > 0)
                {
                    return CallGetValue(remoteNode, key, --retryCount, out nodeOut);
                }
                else
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallFindKey failed - error: {ex.Message}");
                    nodeOut = null;
                    return string.Empty;
                }
            }
        }

        public static ChordNode CallFindContainerKey(ChordNode remoteNode, ulong key)
        {
            return CallFindContainerKey(remoteNode, key, 3);
        }

        public static ChordNode CallFindContainerKey(ChordNode remoteNode, ulong key, int retryCount)
        {
            var instance =Instance(remoteNode);
            try
            {
                var a =  instance.FindContainerKey(key);
                return a;
            }
            catch (System.Exception ex)
            {

                Log(LogLevel.Error, "Remote Invoker", $"CallFindKey error: {ex.Message}");

                if (retryCount > 0)
                {
                    return CallFindContainerKey(remoteNode, key, --retryCount);
                }
                else
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallFindKey failed - error: {ex.Message}");
                    return null;
                }
            }
        }


        /// <summary>
        /// Calls ReplicateKey() remotely, using a default retry value of three.
        /// </summary>
        /// <param name="remoteNode">The remote on which to call the method.</param>
        /// <param name="key">The key to replicate.</param>
        /// <param name="value">The string value to replicate.</param>
        public static void CallReplicateKey(ChordNode remoteNode, ulong key, string value)
        {
            CallReplicateKey(remoteNode, key, value, 3);
        }

        /// <summary>
        /// Calls ReplicateKey remotely.
        /// </summary>
        /// <param name="remoteNode">The remote node on which to call ReplicateKey.</param>
        /// <param name="key">The key to replicate.</param>
        /// <param name="value">The string value to replicate.</param>
        /// <param name="retryCount">The number of retries to attempt.</param>
        public static void CallReplicateKey(ChordNode remoteNode, ulong key, string value, int retryCount)
        {
            var instance = Instance(remoteNode);

            try
            {
                instance.ReplicateKey(key, value);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Debug, "Remote Invoker", $"CallReplicateKey error: {ex.Message}");

                if (retryCount > 0)
                {
                    CallReplicateKey(remoteNode, key, value, --retryCount);
                }
                else
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallReplicateKey failed - error: {ex.Message}");
                }
            }
        }

        #endregion
        private const int RetryCount = 6;

        public static IChordNodeInstance Instance(ChordNode node)
        {
            if (node == null)
            {
                Log(LogLevel.Error, "Navigation", "Invalid Node (Null Argument)");
                return null;
            }

            try
            {

                NetTcpBinding binding = new NetTcpBinding(SecurityMode.None);
                EndpointAddress address = new EndpointAddress($"net.tcp://{node.Host}:{node.Port}/chord");
                ChannelFactory<IChordNodeInstance> channelFactory =
                    new ChannelFactory<IChordNodeInstance>(ChordServer.CreateStreamingBinding(), address);
                var server = channelFactory.CreateChannel();
                return server;
            }
            catch (Exception e)
            {
                // perhaps instead we should just pass on the error?
                Log(LogLevel.Error, "Navigation",
                    $"Unable to activate remote server {node.Host}:{node.Port} ({e.Message}).");
                return null;
            }
        }

        public static ChordNode GetPredecessor(ChordNode node, int retryCount = RetryCount)
        {
            var instance = Instance(node);

            while (retryCount-- > 0)
            {
                try
                {
                    return instance.Predecessor;
                }
                catch (Exception e)
                {
                    Log(LogLevel.Debug, "Remote Accessor", $"GetPredecessor error: {e.Message}");
                }
            }
            return null;
        }

        public static bool CallNotify(ChordNode remoteNode, ChordNode node, int retryCount = RetryCount)
        {

            var state = Instance(remoteNode);
            while (retryCount-- > 0)
            {
                try
                {
                    state.Notify(node);
                    return true; 
                }
                catch (Exception e)
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallNotify error: {e.Message}");
                }
            }
            return false;
        }

        public static ChordNode[] GetSuccessorCache(ChordNode node, int retryCount = RetryCount)
        {
            var instance = Instance(node);

            while (retryCount-- > 0)
            {
                try
                {
                    return instance.SuccessorCache;
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Debug, "Remote Accessor", $"GetSuccessorCache error: {ex.Message}");
                }
            }
            return null;
        }
        public static Binding CreateStreamingBinding()
        {
            TcpTransportBindingElement transport = new TcpTransportBindingElement();
            transport.TransferMode = TransferMode.Streamed;
            BinaryMessageEncodingBindingElement encoder = new BinaryMessageEncodingBindingElement();
            CustomBinding binding = new CustomBinding(encoder, transport);
            return binding;
        }
    }
}
