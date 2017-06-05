using System.Collections.Generic;

namespace AdysTech.Influxer.Config
{
    public interface IOverridableConfig
    {
        string PrintHelpText();

        bool ProcessCommandLineArguments(Dictionary<string, string> CommandLine);
    }
}