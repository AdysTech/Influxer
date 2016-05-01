using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    class ProcessStatus
    {
        public int PointsFound { get; set; }
        public int PointsProcessed { get; set; }
        public int PointsFailed { get; set; }
        public ExitCode ExitCode { get; set; }
    }
}
