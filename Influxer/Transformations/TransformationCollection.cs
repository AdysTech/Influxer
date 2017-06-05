using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace AdysTech.Influxer.Config
{
    public class ExtractTransformationCollection : List<ExtractTransformation>
    {
        [OnDeserialized]
        internal void PostDeserialize(StreamingContext context)
        {
            if (this.Count(t => t.IsDefault) > 1)
                throw new ArgumentException("Only one instance can be marked as Default");
        }
    }

    public class FilterTransformationCollection : List<FilterTransformation>
    {
    }

    public class ReplaceTransformationCollection : List<ReplaceTransformation>
    {
        [OnDeserialized]
        internal void PostDeserialize(StreamingContext context)
        {
            if (this.Count(t => t.IsDefault) > 1)
                throw new ArgumentException("Only one instance can be marked as Default");
        }
    }
}