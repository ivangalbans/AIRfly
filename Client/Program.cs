using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DHTChord.NodeInstance;
using System.IO;
using System.Net;
using DHTChord.Node;
using DHTChord.Server;


namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
           ClientInstance c = new ClientInstance();
           c.Start();
        }
    }
}
