using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public interface ISplit
    {
        bool CanSplit(string content);
        Dictionary<ColumnConfig, string> Split(string content);
        IList<ColumnConfig> SubColumns { get; }
    }


}
