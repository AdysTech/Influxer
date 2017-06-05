using System.Collections.Generic;

namespace AdysTech.Influxer.Config
{
    public interface ISplit
    {
        IList<ColumnConfig> SubColumns { get; }

        bool CanSplit(string content);

        Dictionary<ColumnConfig, string> Split(string content);
    }
}