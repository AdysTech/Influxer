using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public enum SplitType
    {
        Delimited,
        FixedWidth
    }

    public class Splitter : ConfigurationElement, ISplit
    {

        [ConfigurationProperty ("Type")]
        public SplitType Type
        {
            get { return (SplitType) this["Type"]; }
            set { this["Type"] = value; }
        }

        [ConfigurationProperty ("Width")]
        public int Width
        {
            get { return (int) this["Width"]; }
            set { this["Width"] = value; }
        }


        [ConfigurationProperty ("Delimiter")]
        public string Delimiter
        {
            get { return (string) this["Delimiter"]; }
            set { this["Delimiter"] = value; }
        }

        [ConfigurationProperty ("SplitColumns")]
        public ColumnLayoutConfig SubColumnsConfig
        {
            get { return (ColumnLayoutConfig) this["SplitColumns"]; }
            set { this["SplitColumns"] = value; }
        }



        Regex _splitPattern;
        public Regex SplitPattern
        {
            get
            {
                if (Type == SplitType.Delimited && _splitPattern == null && !String.IsNullOrWhiteSpace (Delimiter))
                {
                    _splitPattern = new Regex (Delimiter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
                return _splitPattern;
            }
        }

        public IList<ColumnConfig> SubColumns
        {
            get
            {
                var ls = new List<ColumnConfig> (SubColumnsConfig.Count + SubColumnsConfig.Select (t => t.SplitConfig?.SubColumnsConfig?.Count).Sum ().Value);
                ls.AddRange (SubColumnsConfig);
                ls.AddRange (SubColumnsConfig.SelectMany (t => t.SplitConfig?.SubColumns));
                return ls;
            }
        }

        public bool CanSplit (string content)
        {
            if (String.IsNullOrWhiteSpace (content)) return false;
            
            return (Type == SplitType.FixedWidth) ? content.Length > Width : SplitPattern.IsMatch (content);

        }

        public Dictionary<ColumnConfig, string> Split (string content)
        {
            IList<string> values = null;
            if (Type == SplitType.FixedWidth)
                values = content.SplitFixedWidth (Width).ToList ();
            else
                values = SplitPattern.Split (content).ToList ();
            var ret = new Dictionary<ColumnConfig, string> ();
            for (int i = 0; i < SubColumnsConfig.Count; i++)
            {
                if (SubColumnsConfig[i].SplitConfig?.SubColumns?.Count > 0)
                {
                    var subColumns = SubColumnsConfig[i].SplitConfig.Split (values[i]);
                    foreach (var c in subColumns)
                        ret.Add (c.Key, c.Value);
                }
                else
                {
                    ret.Add (SubColumnsConfig[i], values[i]);
                }
            }
            return ret;
        }
    }
}
