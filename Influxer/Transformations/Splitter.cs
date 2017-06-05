using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdysTech.Influxer.Config
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SplitType
    {
        Delimited,
        FixedWidth
    }

    public class Splitter : ISplit
    {
        private Regex _splitPattern;

        public string Delimiter
        {
            get; set;
        }

        public List<ColumnConfig> SplitColumns
        {
            get; set;
        }

        public Regex SplitPattern
        {
            get
            {
                if (Type == SplitType.Delimited && _splitPattern == null && !String.IsNullOrWhiteSpace(Delimiter))
                {
                    _splitPattern = new Regex(Delimiter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
                return _splitPattern;
            }
        }

        [JsonIgnore]
        public IList<ColumnConfig> SubColumns
        {
            get
            {
                var ls = new List<ColumnConfig>();//(SubColumnsConfig.Count + SubColumnsConfig.Sum(t => t.SplitConfig?.SubColumnsConfig?.Count) ?? default(int));
                if (SplitColumns.Count > 0)
                    ls.AddRange(SplitColumns);
                if (SplitColumns.Any(t => t.SplitConfig != null))
                    ls.AddRange(SplitColumns.SelectMany(t => t.SplitConfig?.SubColumns));
                return ls;
            }
        }

        public SplitType Type
        {
            get; set;
        }

        public int Width
        {
            get; set;
        }

        public bool CanSplit(string content)
        {
            if (String.IsNullOrWhiteSpace(content)) return false;

            return (Type == SplitType.FixedWidth) ? content.Length > Width : SplitPattern.IsMatch(content);
        }

        public Dictionary<ColumnConfig, string> Split(string content)
        {
            IList<string> values = null;
            if (Type == SplitType.FixedWidth)
                values = content.SplitFixedWidth(Width).ToList();
            else
                values = SplitPattern.Split(content).ToList();
            var ret = new Dictionary<ColumnConfig, string>();
            for (int i = 0; i < SplitColumns.Count; i++)
            {
                if (SplitColumns[i].SplitConfig?.SubColumns?.Count > 0)
                {
                    var subColumns = SplitColumns[i].SplitConfig.Split(values[i]);
                    foreach (var c in subColumns)
                        ret.Add(c.Key, c.Value);
                }
                else
                {
                    ret.Add(SplitColumns[i], values[i]);
                }
            }
            return ret;
        }
    }
}