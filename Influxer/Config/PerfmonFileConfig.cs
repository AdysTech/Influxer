using AdysTech.InfluxDB.Client.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public class PerfmonFileConfig : OverridableConfigElement
    {
        [CommandLineArgAttribute ("-delimiter", Usage = "-seperator <char>", Description = "Charecter seperating Columns", DefaultValue = ",")]
        [ConfigurationProperty ("ColumnDelimiter", DefaultValue = ",")]
        public string ColumnDelimiter
        {
            get { return (string) this["ColumnDelimiter"]; }
            set { this["ColumnDelimiter"] = value; }
        }

        [CommandLineArgAttribute ("-splitter", Usage = "-splitter <regex>", Description = "RegEx used for splitting rows into columns", DefaultValue = ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")]
        [ConfigurationProperty ("ColumnSplitter", DefaultValue = ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")]
        public string ColumnSplitter
        {
            get { return (string) this["ColumnSplitter"]; }
            set { this["ColumnSplitter"] = value; }
        }

        [CommandLineArgAttribute ("-timeformat", Usage = "-TimeFormat <format>", Description = "Time format used in input files", DefaultValue = "MM/dd/yyyy HH:mm:ss.fff")]
        [ConfigurationProperty ("TimeFormat", DefaultValue = "MM/dd/yyyy HH:mm:ss.fff")]
        public string TimeFormat
        {
            get { return (string) this["TimeFormat"]; }
            set { this["TimeFormat"] = value; }
        }

        [CommandLineArgAttribute ("-precision", Usage = "-Precision <precision>", Description = "Supported:Hours<1>,Minutes<2>,Seconds<3>,MilliSeconds<4>,MicroSeconds<5>,NanoSeconds<6>", DefaultValue = "Seconds")]
        [ConfigurationProperty ("Precision", DefaultValue = TimePrecision.Seconds)]
        public TimePrecision Precision
        {
            get { return (TimePrecision) this["Precision"]; }
            set { this["Precision"] = value; }
        }

        [ConfigurationProperty ("DefaultTags")]
        public TagsCollection DefaultTags
        {
            get { return (TagsCollection) this["DefaultTags"]; }
            set { this["DefaultTags"] = value; }
        }

        [CommandLineArgAttribute ("-filter", Usage = "-filter <filter>", Description = "Filter input data file, Supported:Measurement (import preexisting measurements), Field (import preexisting fields), Columns (import specified columns)")]
        [ConfigurationProperty ("Filter")]
        public Filters Filter
        {
            get { return (Filters) this["Filter"]; }
            set { this["Filter"] = value; }
        }

        [ConfigurationProperty ("ColumnsFilter")]
        public ColumnsCollection ColumnsFilter
        {
            get { return (ColumnsCollection) this["ColumnsFilter"]; }
            set { this["ColumnsFilter"] = value; }
        }
    }

    public class TagsCollection : OverridableConfigElement
    {
        [CommandLineArgAttribute ("-tags", Usage = "-tags <tag=value,tag2=value>", Description = "Tags to be passed with every value,Comma seperated key value pairs")]
        [ConfigurationProperty ("Tags")]
        [TypeConverter (typeof (CommaDelimitedStringCollectionConverter))]
        public CommaDelimitedStringCollection Tags
        {
            get { return (CommaDelimitedStringCollection) this["Tags"]; }
            set { this["Tags"] = value; }
        }
    }

    public class ColumnsCollection : OverridableConfigElement
    {
        [CommandLineArgAttribute ("-columns", Usage = "-columns <column list>", Description = "Comma seperated list of Columns to import")]
        [ConfigurationProperty ("Columns")]
        [TypeConverter (typeof (CommaDelimitedStringCollectionConverter))]
        public CommaDelimitedStringCollection Columns
        {
            get { return (CommaDelimitedStringCollection) this["Columns"]; }
            set { this["Columns"] = value; }
        }
    }
}
