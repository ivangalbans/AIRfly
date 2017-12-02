using System;
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

        public static ChordNode CallFindSuccessor(ChordNode node, ulong id, int retryCount)
        {
            var instance = Instance(node);

            while(retryCount > 0)
            {
                try
                {
                    return instance.FindSuccessor(id);
                }
                catch (Exception e)
                {
                    Log("Remote Invoker", $"CallFindSuccessor error: {e.Message}");
                    retryCount--;
                }
            }
            return null;
        }

        public static ChordNode CallFindSuccessor(ChordNode node, ulong id)
        {
            return CallFindSuccessor(node, id, 3);
        }

        public static ChordNode GetSuccessor(ChordNode node, int retryCount)
        {
            var nodeInstance = Instance(node);


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

        public static ChordNode GetSuccessor(ChordNode node)
        {
            return GetSuccessor(node,3);
        }
     

        public static ChordNode GetPredecessor(ChordNode node, int retryCount)
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
                    Log("Remote Accessor", $"GetPredecessor error: {e.Message}");
                }
            }
            return null;
        }

        public static ChordNode GetPredecessor(ChordNode node)
        {
            return GetPredecessor(node, 3);
        }
      

        public static bool  CallNotify(ChordNode remoteNode,ChordNode node, int retryCount)
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
                    Log("Remote Invoker", $"CallNotify error: {e.Message}");
                }
            }
            return false;
        }
        public static bool CallNotify(ChordNode remoteNode,ChordNode node)
        {
            return CallNotify(remoteNode,node, 3);
        }

      
        public static ChordNode[] GetSuccessorCache(ChordNode node)
        {
            return GetSuccessorCache(node,3);
        }

        
        public static ChordNode[] GetSuccessorCache(ChordNode node, int retryCount)
        {
            ChordNodeInstance instance = Instance(node);

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
