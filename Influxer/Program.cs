//Copyright -  Adarsha@AdysTech
//https://github.com/AdysTech/Influxer/blob/master/Influxer/Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using AdysTech.InfluxDB.Client.Net;
using System.Configuration;
using AdysTech.Influxer.Config;
using AdysTech.Influxer.Logging;

namespace AdysTech.Influxer
{
    public enum ExitCode : int
    {
        Success = 0,
        InvalidArgument = 1,
        InvalidFilename = 2,
        UnableToProcess = 3,
        ProcessedWithErrors = 4,
        InvalidData = 5,
        UnknownError = 10
    }


    class Program
    {

        private static InfluxerConfigSection settings;

        static int Main (string[] args)
        {

            #region Command Line argument processing
            if (args.Length == 0)
            {
                Logger.LogLine (LogLevel.Info, "Command line arguments not valid, try --help to see valid ones!");
                return (int) ExitCode.InvalidArgument;
            }
            #region Parse command line arguments
            Dictionary<string, string> cmdArgs = new Dictionary<string, string> ();
            Regex commandSwitch = new Regex ("^-[a-zA-Z+]|^/[a-zA-Z+]", RegexOptions.Compiled);
            for (int i = 0; i < args.Length; i++)
            {
                if (commandSwitch.IsMatch (args[i]))
                {
                    var key = args[i].ToLower ();
                    if (i + 1 < args.Length && !commandSwitch.IsMatch (args[i + 1]))
                    {
                        cmdArgs.Add (key.ToLower (), args[i + 1]);
                        i++;
                    }
                    else
                        cmdArgs.Add (key.ToLower (), "true");
                }
            }

            var totalArguments = cmdArgs.Count;

            if (cmdArgs.ContainsKey ("--help") || cmdArgs.ContainsKey ("/help") || cmdArgs.ContainsKey ("/?"))
            {
                var help = new StringBuilder ();
                help.AppendLine ("Influxer is an application to parse log files, push data to Influx for later visualization.");
                help.AppendLine ("It currently supports Windows Perfmon and any generic delimited file formats");
                help.AppendLine ("It uses InfluxDB.Client.Net to interact with Influx.");
                help.AppendLine (new String ('-', 180));
                help.AppendLine ("Supported command line arguments");
                help.AppendLine ("--help /? or /help  shows this help text\n");
                help.AppendLine ();
                help.AppendLine ("/export to print possible config section, pipe it to a file to edit and reuse the config");
                help.AppendLine ();
                help.AppendLine ("-config <configuration file path> to load the config file.");
                help.AppendLine ();
                help.AppendLine ("Any configuration entries can be overridden by command line switches shown below\n");
                help.AppendLine (new String ('-', 180));
                help.Append (InfluxerConfigSection.LoadDefault ().PrintHelpText ());
                Logger.Log (LogLevel.Info, help.ToString ());
                return (int) ExitCode.Success;
            }


            if (cmdArgs.ContainsKey ("-config"))
            {
                try
                {
                    var configFile = Path.GetFullPath (cmdArgs["-config"]);
                    settings = InfluxerConfigSection.Load (configFile);
                    cmdArgs.Remove ("-config");
                    totalArguments -= 1;
                }
                catch (Exception e)
                {
                    Logger.LogLine (LogLevel.Error, "Error with config file:{0},{1}", e.GetType ().Name, e.Message);
                    Logger.LogLine (LogLevel.Info, "Problem loading config file, regenerate it with /config option");
                    return (int) ExitCode.InvalidFilename;
                }
            }
            else
            {
                settings = InfluxerConfigSection.LoadDefault ();
            }
            #endregion

            if (totalArguments >= 1)
            {
                if (!(cmdArgs.Count == 1 && cmdArgs.ContainsKey ("/export")))
                {
                    try
                    {
                        if (!settings.ProcessCommandLineArguments (cmdArgs))
                        {
                            Logger.LogLine (LogLevel.Error, "Invalid commandline arguments!! Use /help to see valid ones");
                            return (int) ExitCode.InvalidArgument;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogLine (LogLevel.Error, "Error processing arguments {0}: {1}", e.GetType ().Name, e.Message);
                        return (int) ExitCode.InvalidArgument;
                    }
                }
            }

            if (cmdArgs.ContainsKey ("/export"))
            {
                if (cmdArgs.ContainsKey ("/autolayout"))
                {
                    if (string.IsNullOrWhiteSpace (settings.InputFileName))
                        throw new ArgumentException ("No Input file name mentioned!!");

                    var g = new GenericFile ();
                    g.GetFileLayout (settings.InputFileName);
                    g.ValidateData (settings.InputFileName);
                }
                InfluxerConfigSection.Export (Console.OpenStandardOutput (), totalArguments > 1 ? false : true);
                return (int) ExitCode.Success;
            }


            if (cmdArgs.Count > 0)
            {
                Logger.LogLine (LogLevel.Error, "Unknown command line arguments: {0}", String.Join (", ", cmdArgs.Select (c => c.Key)));
                return (int) ExitCode.InvalidArgument;
            }

            #endregion

            #region Validate inputs

            if (String.IsNullOrWhiteSpace (settings.InputFileName))
            {
                Logger.LogLine (LogLevel.Error, "Input File Name is not specified!! Can't continue");
                return (int) ExitCode.InvalidArgument;
            }

            try
            {
                settings.InputFileName = Path.GetFullPath (settings.InputFileName);
            }
            catch (Exception e)
            {
                Logger.LogLine (LogLevel.Error, "Error with input file:{0},{1}", e.GetType ().Name, e.Message);
                Logger.LogLine (LogLevel.Info, "Problem with inputfile name, check path");
                return (int) ExitCode.InvalidFilename;
            }



            if (String.IsNullOrWhiteSpace (settings.InfluxDB.InfluxUri))
            {
                Logger.LogLine (LogLevel.Error, "Influx DB Uri is not configured!!");
                return (int) ExitCode.InvalidArgument;
            }

            if (String.IsNullOrWhiteSpace (settings.InfluxDB.DatabaseName))
            {
                Logger.LogLine (LogLevel.Error, "Influx DB name is not configured!!");
                return (int) ExitCode.InvalidArgument;
            }

            #endregion

            ProcessStatus result = new ProcessStatus () { ExitCode = ExitCode.UnknownError };
            try
            {
                Stopwatch stopwatch = new Stopwatch ();
                stopwatch.Start ();

                var client = new InfluxDBClient (settings.InfluxDB.InfluxUri, settings.InfluxDB.UserName, settings.InfluxDB.Password);

                if (!VerifyDatabaseAsync (client, settings.InfluxDB.DatabaseName).Result)
                {
                    Logger.LogLine (LogLevel.Info, "Unable to create DB {0}", settings.InfluxDB.DatabaseName);
                    return (int) ExitCode.UnableToProcess;
                }
                switch (settings.FileFormat)
                {
                    case FileFormats.Perfmon:
                        result = new PerfmonFile ().ProcessPerfMonLog (settings.InputFileName, client).Result;
                        break;
                    case FileFormats.Generic:
                        if (String.IsNullOrWhiteSpace (settings.InfluxDB.Measurement))
                            throw new ArgumentException ("Generic format needs TableName input");
                        result = new GenericFile ().ProcessGenericFile (settings.InputFileName, client).Result;
                        break;
                }

                stopwatch.Stop ();
                Logger.LogLine (LogLevel.Info, "\n Finished!! Processed {0} points (Success: {1}, Failed:{2}) in {3}", result.PointsFound, result.PointsProcessed, result.PointsFailed, stopwatch.Elapsed.ToString ());

            }

            catch (AggregateException e)
            {
                Logger.LogLine (LogLevel.Error, "Error!! {0}:{1} - {2}", e.InnerException.GetType ().Name, e.InnerException.Message, e.InnerException.StackTrace);
            }

            catch (Exception e)
            {
                Logger.LogLine (LogLevel.Error, "Error!! {0}:{1} - {2}", e.GetType ().Name, e.Message, e.StackTrace);
            }
            return (int) result.ExitCode;
        }

        private static async Task<bool> VerifyDatabaseAsync (InfluxDBClient client, string DBName)
        {
            try
            {
                //verify DB exists, create if not
                var dbNames = await client.GetInfluxDBNamesAsync ();
                if (dbNames.Contains (DBName))
                    return true;
                else
                {
                    var filter = settings.FileFormat == FileFormats.Perfmon ? settings.PerfmonFile.Filter : settings.GenericFile.Filter;
                    if (filter == Filters.Measurement || filter == Filters.Field)
                    {
                        Logger.LogLine (LogLevel.Info, "Measurement/Field filtering is not applicable for new database!!");
                        filter = Filters.None;
                    }
                    return await client.CreateDatabaseAsync (DBName);
                }
            }
            catch (Exception e)
            {
                Logger.LogLine (LogLevel.Info, "Unexpected exception of type {0} caught: {1}",
                            e.GetType (), e.Message);
            }
            return false;
        }
    }
}
