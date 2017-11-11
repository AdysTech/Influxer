using AdysTech.InfluxDB.Client.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace AdysTech.Influxer.Config
{
    public class PerfmonFileConfig : OverridableConfigElement
    {
        [CommandLineArg("-columns", Usage = "-columns <column list>", Description = "Comma seperated list of Columns to import")]
        [DefaultValue(Converter = Converters.CommaSeperatedListParser)]
        public List<string> ColumnsFilter
        {
            get; set;
        }

        [CommandLineArg("-splitter", Usage = "-splitter <regex>", Description = "RegEx used for splitting rows into columns")]
        [DefaultValue(Value = ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")]
        public string ColumnSplitter
        {
            get; set;
        }

        [CommandLineArg("-tags", Usage = "-tags <tag=value,tag2=value>", Description = "Tags to be passed with every value,Comma seperated key value pairs")]
        [DefaultValue(Converter = Converters.CommaSeperatedListParser)]
        public List<string> DefaultTags
        {
            get; set;
        }

        [CommandLineArg("-filter", Usage = "-filter <filter>", Description = "Filter input data file, Supported:Measurement (import preexisting measurements), Field (import preexisting fields), Columns (import specified columns)")]
        [DefaultValue(Converter = Converters.EnumParser)]
        [JsonConverter(typeof(StringEnumConverter))]
        public Filters Filter
        {
            get; set;
        }

        [CommandLineArg("-MultiMeasurements", Usage = "-MultiMeasurements", Description = "Push each Performance counter into their own Measurements")]
        [DefaultValue(Converter = Converters.BooleanParser)]
        public bool MultiMeasurements
        {
            get; set;
        }

        [CommandLineArg("-precision", Usage = "-Precision <precision>", Description = "Supported:Hours<1>,Minutes<2>,Seconds<3>,MilliSeconds<4>,MicroSeconds<5>,NanoSeconds<6>")]
        [DefaultValue(Value = "Seconds", Converter = Converters.EnumParser)]
        [JsonConverter(typeof(StringEnumConverter))]
        public TimePrecision Precision
        {
            get; set;
        }

        
        [CommandLineArg("-timeformat", Usage = "-TimeFormat <format>", Description = "Time format used in input files")]
        [DefaultValue(Value = "MM/dd/yyyy HH:mm:ss.fff")]
        public string TimeFormat
        {
            get; set;
        }

        public PerfmonFileConfig() : base()
        {
            DefaultTags = new List<string>();
            ColumnsFilter = new List<string>();
        }
    }
}