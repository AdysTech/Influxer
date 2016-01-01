using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    [AttributeUsage (AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class CommandLineArgAttribute : Attribute
    {
        public string Argument { get; set; }
        public string Usage { get; set; }
        public string Description { get; set; }
        public string DefaultValue { get; set; }

        public CommandLineArgAttribute(string argument)
        {
            Argument = argument;
        }
    }
}
