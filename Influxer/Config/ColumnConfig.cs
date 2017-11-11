using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Runtime.Serialization;

namespace AdysTech.Influxer.Config
{
    public enum ColumnDataType : int
    {
        Unknown = 0,
        Timestamp,
        Tag,
        NumericalField,
        StringField,
        BooleanField
    }

    public class ColumnConfig
    {
        [OnDeserialized]
        internal void PostDeserialize(StreamingContext context)
        {
            if (SplitConfig?.SplitColumns?.Count > 0 && (ExtractTransformations?.Count > 0 ||
                                                ReplaceTransformations?.Count > 0))
            {
                throw new ArgumentException("A Column can be split or transformed, but not both!!");
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public ColumnDataType DataType
        {
            get; set;
        }

        public ExtractTransformationCollection ExtractTransformations
        {
            get; set;
        }

        public FilterTransformationCollection FilterTransformations
        {
            get; set;
        }

        public string InfluxName
        {
            get; set;
        }

        public bool IsDefault
        {
            get; set;
        }

        public string NameInFile
        {
            get; set;
        }

        public ReplaceTransformationCollection ReplaceTransformations
        {
            get; set;
        }

        public bool Skip
        {
            get; set;
        }

        public Splitter SplitConfig
        {
            get; set;
        }
        public ColumnConfig()
        {
            ExtractTransformations = new ExtractTransformationCollection();
            ReplaceTransformations = new ReplaceTransformationCollection();
        }
    }
}
