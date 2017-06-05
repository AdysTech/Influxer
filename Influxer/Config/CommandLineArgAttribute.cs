using System;

namespace AdysTech.Influxer.Config
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class CommandLineArgAttribute : Attribute
    {
        public string Argument { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }

        public CommandLineArgAttribute(string argument)
        {
            Argument = argument;
        }
    }
}