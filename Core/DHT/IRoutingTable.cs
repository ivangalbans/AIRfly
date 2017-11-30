using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DHT
{
    public interface IRoutingTable
    {
        void CreateTable(IDHTNode node);
    }
}
