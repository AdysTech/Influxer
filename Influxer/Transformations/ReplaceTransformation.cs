using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public class ReplaceTransformation : ConfigurationElement, ITransform, IConfigurationElementCollectionElement
    {
        
        [ConfigurationProperty("FindText")]
        public string FindText
        {
            get { return (string)this["FindText"]; }
            set { this["FindText"] = value; }
        }

        [ConfigurationProperty("ReplaceWith")]
        public string ReplaceWith
        {
            get { return (string)this["ReplaceWith"]; }
            set { this["ReplaceWith"] = value; }
        }

        [ConfigurationProperty("IsDefault")]
        public bool IsDefault
        {
            get { return (bool)this["IsDefault"]; }
            set { this["IsDefault"] = value; }
        }

        [ConfigurationProperty("DefaultValue")]
        public string DefaultValue
        {
            get { return (string)this["DefaultValue"]; }
            set { this["DefaultValue"] = value; }
        }



        public bool CanTransform(string content)
        {
            if (IsDefault) return true;
            return !String.IsNullOrWhiteSpace(content) ? content.Contains(FindText) : false;
        }

        public string GetKey()
        {
            return this.GetHashCode().ToString();
        }

        public  string Transform(string content)
        {
            if (IsDefault) return DefaultValue;
            return content.Replace(FindText, ReplaceWith);
        }
    }
}
