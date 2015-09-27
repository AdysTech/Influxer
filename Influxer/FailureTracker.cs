using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    class FailureTracker
    {
        public Type ExceptionType { get; set; }
        public int Count { get; set; }
        public string Message { get; set; }
    }
}
