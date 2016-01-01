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

namespace AdysTech.Influxer
{
    enum ExitCode : int
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

        static int Main(string[] args)
        {

            #region Command Line argument processing
            if ( args.Length == 0 )
            {
                Console.WriteLine ("Command line arguments not valid, try --help to see valid ones!");
                return (int) ExitCode.InvalidArgument;
            }
            #region Parse command line arguments
            Dictionary<string, string> cmdArgs = new Dictionary<string, string> ();
            for ( int i = 0; i < args.Length; i++ )
            {
                if ( args[i].StartsWith ("-") || args[i].StartsWith ("/") )
                {
                    var key = args[i].ToLower ();
                    if ( i == args.Length - 1 )
                    {
                        cmdArgs.Add (key, "true");
                        break;
                    }
                    i++;
                    if ( !( args[i].StartsWith ("-") || args[i].StartsWith ("/") ) )
                        cmdArgs.Add (key.ToLower (), args[i]);
                    else
                        cmdArgs.Add (key.ToLower (), "true");
                }
            }

            var totalArguments = cmdArgs.Count;

            if ( cmdArgs.ContainsKey ("--help") || cmdArgs.ContainsKey ("/help") || cmdArgs.ContainsKey ("/?") )
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
                Console.Write (help.ToString ());
                return (int) ExitCode.Success;
            }


            if ( cmdArgs.ContainsKey ("-config") )
            {
                try
                {
                    var configFile = Path.GetFullPath (cmdArgs["-config"]);
                    settings = InfluxerConfigSection.Load (configFile);
                    cmdArgs.Remove ("-config");
                }
                catch ( Exception e )
                {
                    Console.Error.WriteLine ("Error with config file:{0},{1}", e.GetType ().Name, e.Message);
                    Console.WriteLine ("Problem loading config file, regenerate it with /config option");
                    return (int) ExitCode.InvalidFilename;
                }
            }
            else
            {
                settings = InfluxerConfigSection.LoadDefault ();
            }
            #endregion

            try
            {
                if ( !settings.ProcessCommandLineArguments (cmdArgs) )
                {
                    Console.Error.WriteLine ("Invalid commandline arguments!! Use /help to see valid ones");
                    return (int) ExitCode.InvalidArgument;
                }
            }
            catch ( Exception e )
            {
                Console.Error.WriteLine ("Error processing arguments", e.GetType ().Name, e.Message);
                return (int) ExitCode.InvalidArgument;
            }

            if ( cmdArgs.ContainsKey ("/export") )
            {
                InfluxerConfigSection.Export (Console.OpenStandardOutput (), totalArguments > 1 ? false : true);
                return (int) ExitCode.Success;
            }

            if ( cmdArgs.Count > 0 )
            {
                Console.Error.WriteLine ("Unknown command line arguments: {0}", String.Join (", ", cmdArgs.Select (c => c.Key)));
                return (int) ExitCode.InvalidArgument;
            }

            #endregion



            #region Validate inputs

            if ( String.IsNullOrWhiteSpace (settings.InputFileName) )
            {
                Console.Error.WriteLine ("Input File Name is not specified!! Can't continue");
                return (int) ExitCode.InvalidArgument;
            }

            try
            {
                settings.InputFileName = Path.GetFullPath (settings.InputFileName);
            }
            catch ( Exception e )
            {
                Console.Error.WriteLine ("Error with input file:{0},{1}", e.GetType ().Name, e.Message);
                Console.WriteLine ("Problem with inputfile name, check path");
                return (int) ExitCode.InvalidFilename;
            }


            if ( String.IsNullOrWhiteSpace (settings.InfluxDB.InfluxUri) )
            {
                Console.Error.WriteLine ("Influx DB Uri is not configured!!");
                return (int) ExitCode.InvalidArgument;
            }

            if ( String.IsNullOrWhiteSpace (settings.InfluxDB.DatabaseName) )
            {
                Console.Error.WriteLine ("Influx DB name is not configured!!");
                return (int) ExitCode.InvalidArgument;
            }

            #endregion

            ExitCode result = ExitCode.UnknownError;
            try
            {
                Stopwatch stopwatch = new Stopwatch ();
                stopwatch.Start ();

                var client = new InfluxDBClient (settings.InfluxDB.InfluxUri, settings.InfluxDB.UserName, settings.InfluxDB.Password);

                if ( !VerifyDatabaseAsync (client, settings.InfluxDB.DatabaseName).Result )
                {
                    Console.WriteLine ("Unable to create DB {0}", settings.InfluxDB.DatabaseName);
                    return (int) ExitCode.UnableToProcess;
                }
                switch ( settings.FileFormat )
                {
                    case FileFormats.Perfmon:
                        result = new PerfmonFile ().ProcessPerfMonLog (settings.InputFileName, client).Result;
                        break;
                    case FileFormats.Generic:
                        if ( String.IsNullOrWhiteSpace (settings.GenericFile.TableName) )
                            throw new ArgumentException ("Generic format needs TableName input");
                        result = new GenericFile ().ProcessGenericFile (settings.InputFileName, settings.GenericFile.TableName, client).Result;
                        break;
                }

                stopwatch.Stop ();
                Console.WriteLine ("\n Finished!! Processed in {0}", stopwatch.Elapsed.ToString ());

            }

            catch ( AggregateException e )
            {
                Console.Error.WriteLine ("Error!! {0}:{1} - {2}", e.InnerException.GetType ().Name, e.InnerException.Message, e.InnerException.StackTrace);
            }

            catch ( Exception e )
            {
                Console.Error.WriteLine ("Error!! {0}:{1} - {2}", e.GetType ().Name, e.Message, e.StackTrace);
            }
            return (int) result;
        }

        private static async Task<bool> VerifyDatabaseAsync(InfluxDBClient client, string DBName)
        {
            try
            {
                //verify DB exists, create if not
                var dbNames = await client.GetInfluxDBNamesAsync ();
                if ( dbNames.Contains (DBName) )
                    return true;
                else
                {
                    var filter = settings.FileFormat == FileFormats.Perfmon ? settings.PerfmonFile.Filter : settings.GenericFile.Filter;
                    if ( filter == Filters.Measurement || filter == Filters.Field )
                    {
                        Console.WriteLine ("Measurement/Field filtering is not applicable for new database!!");
                        filter = Filters.None;
                    }
                    return await client.CreateDatabaseAsync (DBName);
                }
            }
            catch ( Exception e )
            {
                Console.WriteLine ("Unexpected exception of type {0} caught: {1}",
                            e.GetType (), e.Message);
            }
            return false;
        }
    }
}
