using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public class FilterTransformation : ConfigurationElement, ITransform, IConfigurationElementCollectionElement
    {

        [ConfigurationProperty ("RegEx")]
        public string RegEx
        {
            get { return (string) this["RegEx"]; }
            set { this["RegEx"] = value; }
        }

        [ConfigurationProperty ("IsDefault")]
        public bool IsDefault
        {
            get { return (bool) this["IsDefault"]; }
            set { this["IsDefault"] = value; }
        }

        [ConfigurationProperty ("DefaultValue")]
        public string DefaultValue
        {
            get { return (string) this["DefaultValue"]; }
            set { this["DefaultValue"] = value; }
        }

        Regex _extractPattern;
        public Regex ExtractPattern
        {
            get
            {
                if (_extractPattern == null && !String.IsNullOrWhiteSpace (RegEx))
                {
                    _extractPattern = new Regex (RegEx, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
                return _extractPattern;
            }
        }



        public bool CanTransform (string content)
        {
            if (IsDefault) return true;

            return !String.IsNullOrWhiteSpace (content) ? ExtractPattern.IsMatch (content) : false;

        }

        public string Transform (string content)
        {
            if (CanTransform (content))
            {
                throw new InvalidDataException ($"{content} filtered out as per rule {RegEx}");
            }
            return string.Empty;
        }

        public string GetKey ()
        {
            return this.GetHashCode ().ToString ();
        }
    }
}
