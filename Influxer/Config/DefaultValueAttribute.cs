using System;

namespace AdysTech.Influxer.Config
{
    public enum Converters
    {
        None,
        BooleanParser,
        CharParser,
        IntParser,
        DoubleParser,
        CommaSeperatedListParser,
        EnumParser
    }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class DefaultValueAttribute : Attribute
    {

        public string Value { get; set; }

        public Converters Converter { get; set; }



    }
}
