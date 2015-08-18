using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    class PerfmonCounter
    {
        public int ColumnIndex { get; set; }
        public string Host { get; set; }
        public string PerformanceObject { get; set; }
        public string CounterName { get; set; }
    }
}
