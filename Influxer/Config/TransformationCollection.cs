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
        
    }

    [ConfigurationCollection(typeof(ExtractTransformation),AddItemName ="Extract", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class ExtractTransformationCollection : ConfigurationElementCollection<ExtractTransformation>
    {

    }
}

