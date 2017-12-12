using System;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Discovery;
using System.Text;
using DHTChord.Node;
using DHTChord.NodeInstance;
using static DHTChord.Logger.Logger;

namespace DHTChord.Server
{
    public static class ChordServer
    {
        public static ChordNode LocalNode { get; set; }
   
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
        private const int RetryCount = 3;
        /// 
        public static ChordNode CallFindSuccessor(ulong id)
        {
            return CallFindSuccessor(LocalNode, id);
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
        public static ChordNode CallFindSuccessor(ChordNode node, ulong id, int retryCount = RetryCount)
        {
            var instance =Instance(node);

            while (retryCount-- > 0)
            {
                try
                {
                    if (ChordNodeInstance.IsInstanceValid(instance, "CallFindSuccessor111111111"))
                    {
                        var retVal = instance.FindSuccessor(id);
                        instance.Close();
                        return retVal;
                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallFindSuccessor error: {e.Message}");
                }
                
            }
            if (ChordNodeInstance.IsInstanceValid(instance, "CallFindSuccessor222222222222"))
            {
                instance.Close();
            }
            return null;
        }

        #region Storage

        /// <summary>
        /// Calls FindKey remotely.
        /// </summary>
        /// <param name="remoteNode">The remote node on which to call FindKey.</param>
        /// <param name="key">The key to look up.</param>
        /// <param name="retryCount">The number of retries to attempt.</param>
        /// <returns>The value corresponding to the key, or empty string if not found.</returns>
        public static string CallGetValue(ChordNode remoteNode, ulong key, out ChordNode nodeOut, int retryCount = RetryCount)
        {
            var instance = Instance(remoteNode);

            try
            {
                return instance.GetValue(key, out nodeOut);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Debug, "Remote Invoker", $"CallFindKey error: {ex.Message}");

                if (retryCount > 0)
                {
                    return CallGetValue(remoteNode, key,  out nodeOut, --retryCount);
                }
                Log(LogLevel.Debug, "Remote Invoker", $"CallFindKey failed - error: {ex.Message}");
                nodeOut = null;
                return string.Empty;
            }
            finally
            {
                instance.Close();
            }
        }

        public static ChordNode CallFindContainerKey(ChordNode remoteNode, ulong key, int retryCount = RetryCount)
        {
            var instance =Instance(remoteNode);
            try
            {
                return instance.FindContainerKey(key);
            }
            catch (Exception ex)
            {

                Log(LogLevel.Error, "Remote Invoker", $"CallFindKey error: {ex.Message}");

                if (retryCount > 0)
                {
                    return CallFindContainerKey(remoteNode, key, --retryCount);
                }
                Log(LogLevel.Debug, "Remote Invoker", $"CallFindKey failed - error: {ex.Message}");
                return null;
            }
            finally
            {
                instance.Close();
            }
        }


        #endregion

        public static void CallReplicationFile(ChordNode remoteNode, string fileName, 
            int retryCount = RetryCount)
        {

            try
            {
                CallSendFile(fileName,null, remoteNode);
                          
            }
            catch (Exception ex)
            {
                Log(LogLevel.Debug, "Remote Invoker", $"CallReplicateFile error: {ex.Message}");

                if (retryCount > 0)
                {
                    CallReplicationFile(remoteNode, fileName, --retryCount);
                }
                else
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallReplicateFile failed - error: {ex.Message}");
                }
            }
         
        }

        public static ChordNodeInstanceClient Instance(ChordNode node)
        {
            if (node == null)
            {
                Log(LogLevel.Error, "Navigation", "Invalid Node (Null Argument)");
                return null;
            }

            try
            {
                return new ChordNodeInstanceClient(CreategBinding(), new EndpointAddress($"net.tcp://{node.Host}:{node.Port}/chord"));
            }
            catch (Exception e)
            {
                // perhaps instead we should just pass on the error?
                Log(LogLevel.Error, "Navigation",
                    $"Unable to activate remote server {node.Host}:{node.Port} ({e.Message}).");
                return null;
            }
        }
        public static ChordNodeInstanceClient Instance(EndpointAddress address)
        {
            if (address == null)
            {
                Log(LogLevel.Error, "Navigation", "Invalid address (Null Argument)");
                return null;
            }

            try
            {
                return new ChordNodeInstanceClient(CreategBinding(), address);
            }
            catch (Exception e)
            {
                // perhaps instead we should just pass on the error?
                Log(LogLevel.Error, "Navigation",
                    $"Unable to activate remote server {address.Uri} ({e.Message}).");
                return null;
            }
        }

        public static EndpointAddress FindServiceAddress()
        {
            var discoveryClient = new DiscoveryClient(new UdpDiscoveryEndpoint());
            while (true)
            {
                Log(LogLevel.Warn, "Discovery", "Discovering ChordNode Instances");
                try
                {
                    var endpoints = discoveryClient.Find(new FindCriteria(typeof(IChordNodeInstance))).Endpoints
                        .Where(x => x.Address.Uri.AbsoluteUri.StartsWith("net.tcp")).ToList();

                    if (endpoints.Count > 0)
                    {
                        Log(LogLevel.Info, "Discovery", $"{endpoints.Count} nodes found");
                        return endpoints[0].Address;
                    }
                    else
                    {
                        Log(LogLevel.Error, "Discovery", "Nothing found");

                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Discovery", "Nothing found");
                }
            }
        }

        public static ChordNode GetPredecessor(ChordNode node, int retryCount = RetryCount)
        {
            var instance = Instance(node);

            while (retryCount-- > 0)
            {
                try
                {
                    if (ChordNodeInstance.IsInstanceValid(instance, "GetPredecessor111111111111"))
                    {
                        var retVal = instance.Predecessor;
                        instance.Close();
                        return retVal;
                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Debug, "Remote Accessor", $"GetPredecessor error: {e.Message}");
                }
            }
            if (ChordNodeInstance.IsInstanceValid(instance, "GetPredecessor222222222222222") && instance.State!=CommunicationState.Closed)
            {
                instance.Close();
            }
            return null;
        }

        public static void CallNotify(ChordNode remoteNode, ChordNode node, int retryCount = RetryCount)
        {

            var instance = Instance(remoteNode);
            while (retryCount-- > 0)
            {
                try
                {
                    if (ChordNodeInstance.IsInstanceValid(instance, "CallNotify1111111111111111111"))
                    {
                        instance.Notify(node);
                        instance.Close();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallNotify error: {e.Message}");
                }
            }
            if (ChordNodeInstance.IsInstanceValid(instance, "CallNotify2222222222222222") && instance.State!=CommunicationState.Closed)
                instance.Close();
        }

        public static ChordNode[] GetSuccessorCache(ChordNode node, int retryCount = RetryCount)
        {
            var instance = Instance(node);

            while (retryCount-- > 0)
            {
                try
                {
                    var retVal = instance.SuccessorCache;
                    instance.Close();
                    return retVal;
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Debug, "Remote Accessor", $"GetSuccessorCache error: {ex.Message}");
                }
            }
            instance.Close();
            return null;
        }

     
        public static Binding CreategBinding()
        {
            return new NetTcpBinding(SecurityMode.None)
            {
                TransferMode = TransferMode.Streamed,
                MaxBufferSize = 2147483647,
                MaxReceivedMessageSize = 2147483647,
                SendTimeout = TimeSpan.MaxValue,
                OpenTimeout = TimeSpan.MaxValue,
                CloseTimeout = TimeSpan.MaxValue,
                ReceiveTimeout = TimeSpan.MaxValue
            };
        }

        public static void CallSendFile(string file, string path, ChordNode remoteNode, int retryCount = RetryCount)
        {
            var instance = Instance(LocalNode);
            try
            {
                instance.SendFile(file, remoteNode, path);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Debug, "Remote Invoker", $"CallAddFile error: {ex.Message}");

                if (retryCount > 0)
                {
                    CallSendFile(file,path, remoteNode, --retryCount);
                }
                else
                {
                    Log(LogLevel.Debug, "Remote Invoker", $"CallAddValue failed - error: {ex.Message}");
                }
            }
            finally
            {
                instance?.Close();
            }
        }
       
        public static void AddFile(string file,string path, ChordNode localNode)
        {
            var key = GetHash(file);
            CallSendFile(file,path,CallFindContainerKey(localNode,key));
        }
    }
}
