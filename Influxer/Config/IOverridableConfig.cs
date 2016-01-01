using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public interface IOverridableConfig
    {
        bool ProcessCommandLineArguments(Dictionary<string,string> CommandLine);
        string PrintHelpText();
    }
}
