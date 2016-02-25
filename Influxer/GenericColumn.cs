using AdysTech.Influxer.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    class GenericColumn
    {
     
        public int ColumnIndex { get; set; }
        public string ColumnHeader { get; set; }
        public ColumnDataType Type { get; set; }
    }
}
