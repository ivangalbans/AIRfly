using System;

using Core.DHT;
using DHTChord.NodeInstance;
using DHTChord.Server;
using static DHTChord.Logger.Logger;

namespace DHTChord.Node
{
    [Serializable]
    public class ChordNode
    {
        public string Host{ get; set; }
        public int Port { get ; set; }
        public ulong Id => ChordServer.GetHash(Host.ToUpper() + Port.ToString());

        public ChordNode(string host, int port)
        {
            Host = host;
            Port = port;
            
        }
        public ChordNodeInstance GetNodeInstance()
        {
            try
            {
                var retInstance = (ChordNodeInstance)Activator.GetObject(typeof(ChordNodeInstance), $"tcp://{Host}:{Port}/chord");
                return retInstance;
            }
            catch (Exception e)
            {
                Log("Navigation", $"Unable to activate remote server {Host}:{Port} ({e.Message}).");

                return null;
            }
        }
        public static bool IsInstanceValid(ChordNodeInstance instance)
        {
            try
            {
                if (instance.Port > 0 && instance.Successor != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Log("Instance",$"Incoming instance was not valid: ({e.Message}).");  // TODO; better logging
                return false;
            }
        }

        public static ChordNodeInstance Instance(ChordNode node)
        {
            if (node == null)
            {
                Log("Navigation", "Invalid Node (Null Argument)");
                return null;
            }

            try
            {
                ChordNodeInstance retInstance = (ChordNodeInstance)Activator.GetObject(typeof(ChordNodeInstance), $"tcp://{node.Host}:{node.Port}/chord");
                return retInstance;
            }
            catch (Exception e)
            {
                // perhaps instead we should just pass on the error?
                Log("Navigation", $"Unable to activate remote server {node.Host}:{node.Port} ({e.Message}).");
                return null;
            }
        }

        public ChordNode CallFindSuccessor(ulong id, int retryCount)
        {
            var state = GetNodeInstance();

            while(retryCount > 0)
            {
                try
                {
                    return state.FindSuccessor(id);
                }
                catch (Exception e)
                {
                    Log("Remote Invoker", $"CallFindSuccessor error: {e.Message}");
                    retryCount--;
                }
            }
            return null;
        }

        public ChordNode CallFindSuccessor(ulong id)
        {
            return CallFindSuccessor(id, 3);
        }

        public ChordNode GetSuccessor(int retryCount)
        {
            var nodeInstance = GetNodeInstance();

            while (retryCount-- > 0)
            {
                try
                {
                    return nodeInstance.Successor;
                }
                catch (Exception e)
                {
                    Log("Remote Accessor", $"GetSuccessor error: {e.Message}");
                }
            }
            return null;
        }

        public ChordNode GetSuccessor()
        {
            return GetSuccessor(3);
        }

        public ChordNode GetPredecessor(int retryCount)
        {
            var state = GetNodeInstance();

            while (retryCount-- > 0)
            {
                try
                {
                    return state.Predecessor;
                }
                catch (Exception e)
                {
                    Log("Remote Accessor", $"GetPredecessor error: {e.Message}");
                }
            }
            return null;
        }

        public ChordNode GetPredecessor()
        {
            return GetPredecessor(3);
        }

        public bool  CallNotify(ChordNode node, int retryCount)
        {
            var state = GetNodeInstance();
            while (retryCount-- > 0)
            {
                try
                {
                    state.Notify(node);
                    return true;
                }
                catch (Exception e)
                {
                    Log("Remote Invoker", $"CallNotify error: {e.Message}");
                }
            }
            return false;
        }
        public bool CallNotify(ChordNode node)
        {
            return CallNotify(node, 3);
        }

        public  ChordNode[] GetSuccessorCache()
        {
            return GetSuccessorCache(3);
        }

        /// <summary>
        /// Gets the remote SuccessorCache property, given a custom retry count.
        /// </summary>
        /// <param name="remoteNode">The remote node from which to access the property.</param>
        /// <param name="retryCount">The number of times to retry the operation in case of error.</param>
        /// <returns>The remote successorCache, or NULL in case of error.</returns>
        public ChordNode[] GetSuccessorCache(int retryCount)
        {
            ChordNodeInstance instance = GetNodeInstance();

            while (retryCount-- > 0)
            {
                try
                {
                    return instance.SuccessorCache;
                }
                catch (Exception ex)
                {
                    Log("Remote Accessor", $"GetSuccessorCache error: {ex.Message}");
                }
            }
            return null;
        }

        public override string ToString()
        {
            return $"Host {Host}:{Port}";
        }
    }
}
