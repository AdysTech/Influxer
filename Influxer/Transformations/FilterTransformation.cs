using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AdysTech.Influxer.Config
{
    public class FilterTransformation :  ITransform
    {

        public string RegEx
        {
            get; set;
        }

        public bool IsDefault
        {
            get; set;
        }

       public string DefaultValue
        {
            get; set;
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
