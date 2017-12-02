using System;
using Core.DHT;
using DHTChord.Server;
using DHTChord.State;
using static DHTChord.Logger.Logger;

namespace DHTChord.Node
{
    public class ChordNode : IDhtNode
    {
        public string Host { get; set; }
        public int Port { get ; set; }
        public ulong Id => ChordServer.GetHash(Host.ToUpper() + Port);

        public ChordNode(string host, int port)
        {
            Host = host;
            Port = port;
            
        }
        public ChordState GetState()
        {
            if(this == null)
            {
                throw new Exception("Invalid Node");
            }
            try
            {
                return (ChordState)Activator.GetObject(typeof(ChordState), $"tcp://{Host} : {Port}/chord");
            }
            catch (Exception)
            {
                throw;
            }
        }

        public ChordNode CallFindSuccessor(ulong id, int retryCount)
        {
            var state = GetState();

            while(retryCount-- > 0)
            {
                try
                {
                    return state.FindSuccessor(id);
                }
                catch (Exception e)
                {
                    Log("Remote Invoker", $"CallFindSuccessor error: {e.Message}");
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

            while (retryCount-- > 0)
            {
                try
                {
                    return state.Successor;
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
            var state = GetState();

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
            var state = GetState();
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
    }
}
