using System;

using Core.DHT;
using DHTChord.NodeInstance;
using DHTChord.Server;
using static DHTChord.Logger.Logger;

namespace DHTChord.Node
{
    public class ChordNode
    {
        public string Host { get; set; }
        public int Port { get ; set; }
        public ulong Id { get => ChordServer.GetHash(Host.ToUpper() + Port.ToString());}
        public ChordNode(string host, int port)
        {
            Host = host;
            Port = port;
            
        }
        public ChordNodeInstance GetState()
        {
            if(this == null)
            {
                throw new Exception("Invalid Node");
            }
            try
            {
                return (ChordNodeInstance)Activator.GetObject(typeof(ChordNodeInstance), $"tcp://{Host} : {Port}/chord");
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public ChordNode CallFindSuccessor(ulong id, int retryCount)
        {
            var state = GetState();

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
            var state = GetState();

            while (retryCount > 0)
            {
                try
                {
                    return state.Successor;
                }
                catch (Exception e)
                {
                    Log("Remote Accessor", $"GetSuccessor error: {e.Message}");
                    retryCount--;
                    throw;
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
            var state = GetState();

            while (retryCount > 0)
            {
                try
                {
                    return state.Predecessor;
                }
                catch (Exception e)
                {
                    Log("Remote Accessor", $"GetPredecessor error: {e.Message}");
                    retryCount--;
                    throw;
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
            var state = GetState();
            while (retryCount > 0)
            {
                try
                {
                    state.Notify(node);
                    return true;
                }
                catch (Exception e)
                {
                    Log("Remote Invoker", $"CallNotify error: {e.Message}");
                    retryCount--;
                    throw;
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
            ChordNodeInstance instance = GetState();

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
    }
}
