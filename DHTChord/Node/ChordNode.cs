using System;
using DHTChord.NodeInstance;
using DHTChord.Server;
using DHTChord.Logger;
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

        private static int _retryCount = 6;
      
        

        public static ChordNodeInstance Instance(ChordNode node)
        {
            if (node == null)
            {
                Log(LogLevel.Error, "Navigation", "Invalid Node (Null Argument)");
                return null;
            }

            try
            {
                var retInstance = (ChordNodeInstance)Activator.GetObject(typeof(ChordNodeInstance), $"tcp://{node.Host}:{node.Port}/chord");
                return retInstance;
            }
            catch (Exception e)
            {
                // perhaps instead we should just pass on the error?
                Log(LogLevel.Error, "Navigation", $"Unable to activate remote server {node.Host}:{node.Port} ({e.Message}).");
                return null;
            }
        }

        public static ChordNode CallFindSuccessor(ChordNode node, ulong id, int retryCount)
        {
            var instance = Instance(node);

            while(retryCount-- > 0)
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

        public static ChordNode CallFindSuccessor(ChordNode node, ulong id)
        {
            return CallFindSuccessor(node, id, _retryCount);
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
                    Log(LogLevel.Debug, "Remote Accessor", $"GetSuccessor error: {e.Message}");
                }
            }
            return null;
        }

        public static ChordNode GetSuccessor(ChordNode node)
        {
            return GetSuccessor(node,_retryCount);
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
                    Log(LogLevel.Debug, "Remote Accessor", $"GetPredecessor error: {e.Message}");
                }
            }
            return null;
        }

        public static ChordNode GetPredecessor(ChordNode node)
        {
            return GetPredecessor(node, _retryCount);
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
                    Log(LogLevel.Debug, "Remote Invoker", $"CallNotify error: {e.Message}");
                }
            }
            return false;
        }
        public static bool CallNotify(ChordNode remoteNode,ChordNode node)
        {
            return CallNotify(remoteNode,node, _retryCount);
        }

      
        public static ChordNode[] GetSuccessorCache(ChordNode node)
        {
            return GetSuccessorCache(node,_retryCount);
        }

        
        public static ChordNode[] GetSuccessorCache(ChordNode node, int retryCount)
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

        public override string ToString()
        {
            return $"Host {Host}:{Port}";
        }

        public override bool Equals(object obj)
        {
            if (obj is ChordNode tmp)
                return Id == tmp.Id;
            return false;
        }

    }
}
