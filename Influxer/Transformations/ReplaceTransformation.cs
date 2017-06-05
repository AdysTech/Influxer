using System;

namespace AdysTech.Influxer.Config
{
    public class ReplaceTransformation : ITransform
    {
        public string DefaultValue
        {
            get; set;
        }

        public string FindText
        {
            get; set;
        }

        public bool IsDefault
        {
            get; set;
        }

        public string ReplaceWith
        {
            get; set;
        }

        public bool CanTransform(string content)
        {
            if (IsDefault) return true;
            return !String.IsNullOrWhiteSpace(content) ? content.Contains(FindText) : false;
        }

        public string Transform(string content)
        {
            if (IsDefault) return DefaultValue;
            return content.Replace(FindText, ReplaceWith);
        }
    }
}