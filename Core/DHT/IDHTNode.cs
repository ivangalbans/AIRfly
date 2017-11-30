using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DHT
{
    public interface IDHTNode
    {
        string Host { get; set; }
        int Port { get; set; }
        ulong ID { get; }
    }
}
