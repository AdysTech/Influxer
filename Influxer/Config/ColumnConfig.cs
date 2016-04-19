using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public enum ColumnDataType : int
    {
        Unknown = 0,
        Timestamp,
        Tag,
        NumericalField,
        StringField
    }

    public class ColumnConfig : ConfigurationElement, IConfigurationElementCollectionElement
    {


        [ConfigurationProperty("NameInFile")]
        public string NameInFile
        {
            get { return (string)this["NameInFile"]; }
            set { this["NameInFile"] = value; }
        }

        [ConfigurationProperty("InfluxName", IsRequired = true)]
        public string InfluxName
        {
            get { return (string)this["InfluxName"]; }
            set { this["InfluxName"] = value; }
        }

        [ConfigurationProperty("Skip", DefaultValue = false)]
        public bool Skip
        {
            get { return (bool)this["Skip"]; }
            set { this["Skip"] = value; }
        }

        [ConfigurationProperty("DataType")]
        public ColumnDataType DataType
        {
            get { return (ColumnDataType)this["DataType"]; }
            set { this["DataType"] = value; }
        }

        [ConfigurationProperty("ReplaceTransformations")]
        public ReplaceTransformationCollection ReplaceTransformations
        {
            get { return (ReplaceTransformationCollection)this["ReplaceTransformations"]; }
            set { this["ReplaceTransformations"] = value; }
        }

        [ConfigurationProperty("ExtractTransformations")]
        public ExtractTransformationCollection ExtractTransformations
        {
            get { return (ExtractTransformationCollection)this["ExtractTransformations"]; }
            set { this["ExtractTransformations"] = value; }
        }

        public string GetKey()
        {
            return InfluxName;
        }


      
    }
}
