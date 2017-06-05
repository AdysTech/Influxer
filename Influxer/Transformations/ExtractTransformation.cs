using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdysTech.Influxer.Config
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ExtractType
    {
        RegEx,
        SubString
    }

    public class ExtractTransformation : ITransform
    {
        private Regex _extractPattern;

        public string DefaultValue
        {
            get; set;
        }

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

        public bool IsDefault
        {
            get; set;
        }

        public int Length
        {
            get; set;
        }

        public string RegEx
        {
            get; set;
        }

        public string ResultPattern
        {
            get; set;
        }

        public int StartIndex
        {
            get; set;
        }

        public ExtractType Type
        {
            get; set;
        }

        public bool CanTransform(string content)
        {
            if (IsDefault) return true;
            if (Type == ExtractType.SubString)
                return !String.IsNullOrWhiteSpace(content) ? content.Length > StartIndex && content.Length > (StartIndex + Length) : false;
            else
                return !String.IsNullOrWhiteSpace(content) ? ExtractPattern.IsMatch(content) : false;
        }

        public string Transform(string content)
        {
            if (IsDefault) return DefaultValue;
            if (Type == ExtractType.SubString)
                return content.Substring(StartIndex, Length);
            else
            {
                var m = ExtractPattern.Match(content);
                if (m.Success)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(ResultPattern))
                            return m.Groups[0].Value;
                        else
                            return string.Format(ResultPattern, m.Groups.Cast<Group>().Skip(1).Select(g => g.Value as object).ToArray());
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException($"Could not extract {content} using {ResultPattern} due to {e.Message}");
                    }
                }
            }
            return null;
        }
    }
}