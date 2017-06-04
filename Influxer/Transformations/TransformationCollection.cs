using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{

    public class ReplaceTransformationCollection : List<ReplaceTransformation>
    {
        [OnDeserialized]
        internal void PostDeserialize(StreamingContext context)
        {
            if (this.Count(t => t.IsDefault) > 1)
                throw new ArgumentException("Only one instance can be marked as Default");
        }
    }

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
}
