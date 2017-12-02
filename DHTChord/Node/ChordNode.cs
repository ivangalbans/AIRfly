using System;

using Core.DHT;
using DHTChord.NodeInstance;
using DHTChord.Server;
using static DHTChord.Logger.Logger;

namespace DHTChord.Node
{
    public class ChordNode : IDHTNode
    {
        public string Host { get; set; }
        public int Port { get ; set; }
        public ulong ID { get => ChordServer.GetHash(Host.ToUpper() + Port.ToString());}
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

        public ChordNode CallFindSuccessor(ulong ID, int retryCount)
        {
            var state = this.GetState();

            while(retryCount > 0)
            {
                try
                {
                    return state.FindSuccessor(ID);
                }
                catch (Exception e)
                {
                    Log("Remote Invoker", $"CallFindSuccessor error: {e.Message}");
                    retryCount--;
                }
            }
            return null;
        }

        public ChordNode CallFindSuccessor(ulong ID)
        {
            return CallFindSuccessor(ID, 3);
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
    }
}
