using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public class GenericFileConfig : PerfmonFileConfig
    {
        [CommandLineArgAttribute ("-table", Usage = "-table <table name>", Description = "Measurement name in InfluxDB", DefaultValue = "InfluxerData")]
        [ConfigurationProperty ("TableName", DefaultValue = "InfluxerData")]
        public string TableName
        {
            get { return (string) this["TableName"]; }
            set { this["TableName"] = value; }
        }

        [CommandLineArgAttribute ("-utcoffset", Usage = "-utcoffset <No of Minutes>", Description = "Offset in minutes to UTC, each line in input will be adjusted to arrive time in UTC")]
        [ConfigurationProperty ("UtcOffset", DefaultValue = 0)]
        public int UtcOffset
        {
            get { return (int) this["UtcOffset"]; }
            set { this["UtcOffset"] = value; }
        }
    }
}
