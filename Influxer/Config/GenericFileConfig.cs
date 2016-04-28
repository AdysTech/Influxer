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


        [CommandLineArgAttribute ("-validate", Usage = "-validate <No of Rows>", Description = "Validates n rows for consistent column data types")]
        [ConfigurationProperty ("ValidateRows", DefaultValue = 10)]
        public int ValidateRows
        {
            get { return (int) this["ValidateRows"]; }
            set { this["ValidateRows"] = value; }
        }

        [CommandLineArgAttribute ("-header", Usage = "-header <Row No>", Description = "Indicates which row to use to get column headers")]
        [ConfigurationProperty ("HeaderRow", DefaultValue = 1)]
        public int HeaderRow
        {
            get { return (int) this["HeaderRow"]; }
            set { this["HeaderRow"] = value; }
        }

        [CommandLineArgAttribute ("-skip", Usage = "-skip <Row No>", Description = "Indicates how may roaws should be skipped after header row to get data rows")]
        [ConfigurationProperty ("SkipRows", DefaultValue = 0)]
        public int SkipRows
        {
            get { return (int) this["SkipRows"]; }
            set { this["SkipRows"] = value; }
        }

        [CommandLineArgAttribute("-ignore", Usage = "-ignore <char>", Description = "Lines starting with <char> are considered as comments, and ignored")]
        [ConfigurationProperty("CommentMarker")]
        public string CommentMarker
        {
            get { return (string)this["CommentMarker"]; }
            set { this["CommentMarker"] = value; }
        }

        [ConfigurationProperty("TimeColumn", DefaultValue = 1)]
        public int TimeColumn
        {
            get { return (int)this["TimeColumn"]; }
            set { this["TimeColumn"] = value; }
        }

        [CommandLineArgAttribute("-noheader", Usage = "-noheader true", Description = "Input file does not have column headers, configuration file should provide a column header mapping")]
        [ConfigurationProperty("HeaderMissing")]
        public bool HeaderMissing
        {
            get { return (bool)this["HeaderMissing"]; }
            set { this["HeaderMissing"] = value; }
        }

        [ConfigurationProperty("ColumnLayout")]
        public ColumnLayoutConfig ColumnLayout
        {
            get { return (ColumnLayoutConfig)this["ColumnLayout"]; }
            set { this["ColumnLayout"] = value; }
        }

        [CommandLineArgAttribute ("-ignoreerrors", Usage = "-ignoreerrors true", Description = "Ignore too many errors due to invalid data or config file")]
        [ConfigurationProperty ("IgnoreErrors")]
        public bool IgnoreErrors
        {
            get { return (bool) this["IgnoreErrors"]; }
            set { this["IgnoreErrors"] = value; }
        }
    }
}
