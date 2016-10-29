using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{

    [ConfigurationCollection(typeof(ReplaceTransformation),AddItemName ="Replace", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class ReplaceTransformationCollection : ConfigurationElementCollection<ReplaceTransformation>
    {
        protected override void PostDeserialize()
        {
            base.PostDeserialize();
            if (this.Count(t => t.IsDefault) > 1)
                throw new ArgumentException("Only one instance can be marked as Default");
        }
    }

    [ConfigurationCollection(typeof(ExtractTransformation),AddItemName ="Extract", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class ExtractTransformationCollection : ConfigurationElementCollection<ExtractTransformation>
    {
        protected override void PostDeserialize()
        {
            base.PostDeserialize();
            if (this.Count(t => t.IsDefault) > 1)
                throw new ArgumentException("Only one instance can be marked as Default");
        }
    }

    [ConfigurationCollection (typeof (FilterTransformation), AddItemName = "Filter", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class FilterTransformationCollection : ConfigurationElementCollection<FilterTransformation>
    {
        
    }
}

