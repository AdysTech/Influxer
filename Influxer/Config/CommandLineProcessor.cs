using AdysTech.Influxer.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AdysTech.Influxer.Config
{
    public static class CommandLineProcessor
    {
        private static InfluxerConfigSection _settings;

        public static InfluxerConfigSection Settings
        {
            get
            {
                return _settings;
            }
        }

        public static bool ProcessArguments(string[] args)
        {
            if (args.Length == 0)
            {
                var help = new StringBuilder();
                help.AppendLine($"Influxer Version: {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}");
                help.AppendLine($"Build: {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion}");
                Logger.Log(LogLevel.Info, help.ToString());
                throw new ArgumentException("Command line arguments not valid, try --help to see valid ones!");
            }

            #region Parse command line arguments

            Dictionary<string, string> cmdArgs = new Dictionary<string, string>();
            Regex commandSwitch = new Regex("^-[-a-zA-Z+]|^/[?a-zA-Z+]", RegexOptions.Compiled);
            for (int i = 0; i < args.Length; i++)
            {
                if (commandSwitch.IsMatch(args[i]))
                {
                    var key = args[i].ToLower();
                    if (i + 1 < args.Length && !commandSwitch.IsMatch(args[i + 1]))
                    {
                        cmdArgs.Add(key.ToLower(), args[i + 1]);
                        i++;
                    }
                    else
                        cmdArgs.Add(key.ToLower(), "true");
                }
            }

            var totalArguments = cmdArgs.Count;

            if (cmdArgs.ContainsKey("--help") || cmdArgs.ContainsKey("/help") || cmdArgs.ContainsKey("/?"))
            {
                var help = new StringBuilder();
                help.AppendLine($"Influxer Version: {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}");
                help.AppendLine($"Build: {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion}");
                help.AppendLine("Influxer is an application to parse log files, push data to Influx for later visualization.");
                help.AppendLine("It currently supports Windows Perfmon and any generic delimited file formats");
                help.AppendLine("It uses InfluxDB.Client.Net to interact with Influx.");
                help.AppendLine(new String('-', 180));
                help.AppendLine("Supported command line arguments");
                help.AppendLine("--help /? or /help  shows this help text\n");
                help.AppendLine();
                help.AppendLine("/export to print possible config section, pipe it to a file to edit and reuse the config");
                help.AppendLine();
                help.AppendLine("-config <configuration file path> to load the config file.");
                help.AppendLine();
                help.AppendLine("Any configuration entries can be overridden by command line switches shown below\n");
                help.AppendLine(new String('-', 180));
                help.Append(InfluxerConfigSection.LoadDefault().PrintHelpText());
                Logger.Log(LogLevel.Info, help.ToString());
                return false;
            }

            if (cmdArgs.ContainsKey("-config"))
            {
                try
                {
                    var configFile = Path.GetFullPath(cmdArgs["-config"]);
                    _settings = InfluxerConfigSection.Load(configFile);
                    cmdArgs.Remove("-config");
                    totalArguments -= 1;
                }
                catch (Exception e)
                {
                    throw new FileLoadException($"Error Loading config file:{e.GetType().Name},{e.Message}", e);
                }
            }
            else
            {
                _settings = InfluxerConfigSection.LoadDefault();
            }

            #endregion Parse command line arguments

            if (totalArguments >= 1)
            {
                if (!(cmdArgs.Count == 1 && cmdArgs.ContainsKey("/export")))
                {
                    try
                    {
                        if (!_settings.ProcessCommandLineArguments(cmdArgs))
                        {
                            throw new ArgumentException("Invalid commandline arguments!! Use /help to see valid ones");
                        }
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException($"Error processing arguments :{e.GetType().Name}, {e.Message}", e);
                    }
                }
            }

            if (cmdArgs.ContainsKey("/export"))
            {
                if (cmdArgs.ContainsKey("/autolayout"))
                {
                    if (string.IsNullOrWhiteSpace(_settings.InputFileName))
                        throw new ArgumentException("No Input file name mentioned!!");

                    var g = new GenericFile();
                    g.GetFileLayout(_settings.InputFileName);
                    g.ValidateData(_settings.InputFileName);
                }
                //just to be able to write to console or test/debug window. Console.OpenStandardOutput () will do just fine
                using (MemoryStream stream = new MemoryStream())
                {
                    InfluxerConfigSection.Export(stream, totalArguments > 1 ? false : true);
                    StreamReader reader = new StreamReader(stream);
                    stream.Position = 0;
                    Logger.Log(LogLevel.Info, reader.ReadToEnd());
                }
                return false;
            }

            if (cmdArgs.Count > 0)
            {
                throw new ArgumentException($"Unknown command line arguments: {String.Join(", ", cmdArgs.Select(c => c.Key))}");
            }
            return true;
        }
    }
}