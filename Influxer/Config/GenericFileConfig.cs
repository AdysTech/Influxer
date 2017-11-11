using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace AdysTech.Influxer.Config
{
    public class GenericFileConfig : PerfmonFileConfig
    {
        public List<ColumnConfig> ColumnLayout
        {
            get; set;
        }

        [CommandLineArg("-ignore", Usage = "-ignore <char>", Description = "Lines starting with <char> are considered as comments, and ignored")]
        public string CommentMarker
        {
            get; set;
        }

        [CommandLineArg("-noheader", Usage = "-noheader", Description = "Input file does not have column headers, configuration file should provide a column header mapping")]
        [DefaultValue(Converter = Converters.BooleanParser)]
        public bool HeaderMissing
        {
            get; set;
        }

        [CommandLineArgAttribute("-header", Usage = "-header <Row No>", Description = "Indicates which row to use to get column headers")]
        [DefaultValue(Value = "1", Converter = Converters.IntParser)]
        public int HeaderRow
        {
            get; set;
        }

        [CommandLineArgAttribute("-ignoreerrors", Usage = "-ignoreerrors", Description = "Ignore too many errors due to invalid data or config file")]
        [DefaultValue(Converter = Converters.BooleanParser)]
        public bool IgnoreErrors
        {
            get; set;
        }

        [CommandLineArgAttribute("-skip", Usage = "-skip <Row No>", Description = "Indicates how may roaws should be skipped after header row to get data rows")]
        [DefaultValue(Converter = Converters.IntParser)]
        public int SkipRows
        {
            get; set;
        }

        [CommandLineArg("-timetype", Usage = "-timetype <type>", Description = "Type of Time format used in input files, String, Epoch or Binary")]
        [DefaultValue(Value = "String", Converter = Converters.EnumParser)]
        [JsonConverter(typeof(StringEnumConverter))]
        public TimeForamtType TimeFormatType
        {
            get; set;
        }

        [DefaultValue(Value = "1", Converter = Converters.IntParser)]
        public int TimeColumn
        {
            get; set;
        }

        [CommandLineArgAttribute("-utcoffset", Usage = "-utcoffset <No of Minutes>", Description = "Offset in minutes to UTC, each line in input will be adjusted to arrive time in UTC")]
        [DefaultValue(Converter = Converters.IntParser)]
        public int UtcOffset
        {
            get; set;
        }

        [CommandLineArgAttribute("-validate", Usage = "-validate <No of Rows>", Description = "Validates n rows for consistent column data types")]
        [DefaultValue(Value = "10", Converter = Converters.IntParser)]
        public int ValidateRows
        {
            get; set;
        }

        public GenericFileConfig() : base()
        {
            ColumnLayout = new List<ColumnConfig>();
        }
    }
}