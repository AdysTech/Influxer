using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public enum ExtractType
    {
        RegEx,
        SubString
    }

    public class ExtractTransformation :ConfigurationElement, ITransform,IConfigurationElementCollectionElement
    {

        [ConfigurationProperty("Type")]
        public ExtractType Type
        {
            get { return (ExtractType)this["Type"]; }
            set { this["Type"] = value; }
        }

        [ConfigurationProperty("StartIndex")]
        public int StartIndex
        {
            get { return (int)this["StartIndex"]; }
            set { this["StartIndex"] = value; }
        }


        [ConfigurationProperty("Length")]
        public int Length
        {
            get { return (int)this["Length"]; }
            set { this["Length"] = value; }
        }


        [ConfigurationProperty("RegEx")]
        public string RegEx
        {
            get { return (string)this["RegEx"]; }
            set { this["RegEx"] = value; }
        }

        Regex _extractPattern;
        public Regex ExtractPattern
        {
            get
            {
                if (Type == ExtractType.RegEx && _extractPattern == null && !String.IsNullOrWhiteSpace(RegEx))
                {
                    _extractPattern = new Regex(RegEx, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
                return _extractPattern;
            }
        }

 

        public  bool CanTransform(string content)
        {
            if (Type == ExtractType.SubString)
                return !String.IsNullOrWhiteSpace(content) ? content.Length > StartIndex && content.Length > (StartIndex + Length) : false;
            else
                return !String.IsNullOrWhiteSpace(content) ? _extractPattern.IsMatch(content) : false;

        }

        public  string Transform(string content)
        {
            if (Type == ExtractType.SubString)
                return content.Substring(StartIndex, Length);
            else
            {
                var m = _extractPattern.Match(content);
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
                return null;
            }
        }

        public string GetKey()
        {
            return this.GetHashCode().ToString();
        }
    }
}
