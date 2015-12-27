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
    class Program
    {

        private static InfluxerConfigSection settings;
        private static Regex pattern;


        enum ExitCode : int
        {
            Success = 0,
            InvalidArgument = 1,
            InvalidFilename = 2,
            UnableToProcess = 3,
            ProcessedWithErrors = 4,
            UnknownError = 10
        }

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
                help.AppendLine ("/config to print possible config section, pipe it to a file to edit and reuse the config");
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

            pattern = new Regex (settings.FileFormat == FileFormats.Perfmon ? settings.PerfmonFile.ColumnDelimiter : settings.GenericFile.ColumnDelimiter
                                + "(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", RegexOptions.Compiled);



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
                        result = ProcessPerfMonLog (settings.InputFileName, client).Result;
                        break;
                    case FileFormats.Generic:
                        if ( String.IsNullOrWhiteSpace (settings.GenericFile.TableName) )
                            throw new ArgumentException ("Generic format needs TableName input");
                        result = ProcessGenericFile (settings.InputFileName, settings.GenericFile.TableName, client).Result;
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


        private static async Task<ExitCode> ProcessPerfMonLog(string InputFileName, InfluxDBClient client)
        {

            try
            {
                int minOffset = 0;
                var lineCount = 0;
                var failedCount = 0;
                StringBuilder content = new StringBuilder ();
                var failureReasons = new Dictionary<Type, FailureTracker> ();

                Stopwatch stopwatch = new Stopwatch ();
                stopwatch.Start ();


                var firstLine = File.ReadLines (InputFileName).FirstOrDefault ();

                var firstCol = firstLine.Substring (0, firstLine.IndexOf (','));
                if ( !firstCol.Contains ("PDH-CSV") )
                    throw new Exception ("Input file is not a Standard Perfmon csv file");
                var x = Regex.Matches (firstCol, "([-0-9]+)");
                if ( x.Count > 0 )
                    minOffset = int.Parse (x[3].ToString ());

                //get the column headers
                List<PerfmonCounter> pecrfCounters;
                try
                {
                    pecrfCounters = ParsePerfMonFileHeader (firstLine);
                }
                catch ( Exception ex )
                {
                    throw new InvalidDataException ("Unable to parse file headers", ex);
                }



                Dictionary<string, List<string>> dbStructure;
                IEnumerable<IGrouping<string, PerfmonCounter>> perfGroup;
                if ( settings.PerfmonFile.Filter != Filters.None )
                {
                    var filterColumns = ParsePerfMonFileHeader (settings.PerfmonFile.ColumnsFilter.Columns.ToString (), false);
                    dbStructure = await client.GetInfluxDBStructureAsync (settings.InfluxDB.DatabaseName);
                    perfGroup = FilterPerfmonLogColumns (pecrfCounters, filterColumns, dbStructure).GroupBy (p => p.PerformanceObject);
                }
                else
                {
                    perfGroup = pecrfCounters.GroupBy (p => p.PerformanceObject);
                }

                //Parallel.ForEach (File.ReadLines (inputFileName).Skip (1), (string line) =>
                foreach ( var line in File.ReadLines (InputFileName).Skip (1) )
                {
                    lineCount++;
                    try
                    {
                        if ( !await ProcessPerfmonLogLine (line, perfGroup, minOffset, pattern, client) )
                            failedCount++;

                    }
                    catch ( Exception e )
                    {
                        failedCount++;
                        var type = e.GetType ();
                        if ( !failureReasons.ContainsKey (type) )
                            failureReasons.Add (type, new FailureTracker () { ExceptionType = type, Message = e.Message });
                        failureReasons[type].LineNumbers.Add (lineCount);
                    }

                    if ( failedCount > 0 )
                        Console.Write ("\r{0} Processed {1}, Failed - {2}                        ", stopwatch.Elapsed.ToString (@"hh\:mm\:ss"), lineCount, failedCount);
                    else
                        Console.Write ("\r{0} Processed {1}                          ", stopwatch.Elapsed.ToString (@"hh\:mm\:ss"), lineCount);

                }
                lineCount = 0;
                pecrfCounters.Clear ();
                stopwatch.Stop ();
                if ( failedCount > 0 )
                {
                    Console.WriteLine ("\n Done!! Processed {0}, failed to insert {1}", lineCount, failedCount);
                    foreach ( var f in failureReasons.Values )
                        Console.WriteLine ("{0}:{1} - {2} : {3}", f.ExceptionType, f.Message, f.Count, String.Join (",", f.LineNumbers));
                    if ( failedCount == lineCount )
                        return ExitCode.UnableToProcess;
                    else
                        return ExitCode.ProcessedWithErrors;
                }
            }
            catch ( Exception e )
            {
                Console.Error.WriteLine ("\r\nError!! {0}:{1} - {2}", e.GetType ().Name, e.Message, e.StackTrace);
                return ExitCode.UnknownError;
            }
            return ExitCode.Success;
        }

        private static List<PerfmonCounter> ParsePerfMonFileHeader(string headerLine, bool quoted = true)
        {
            List<PerfmonCounter> perfCounters = new List<PerfmonCounter> ();
            if ( String.IsNullOrWhiteSpace (headerLine) ) return perfCounters;
            var columns = pattern.Split (headerLine);
            var column = 1;

            perfCounters.AddRange (columns.Skip (quoted ? 1 : 0).Where (s => quoted ? s.StartsWith ("\"\\") : s.StartsWith ("\\")).Select (p =>
                    p.Replace (settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray (), settings.InfluxDB.InfluxReserved.ReplaceReservedWith).Split ('\\')).Select (p =>
                        new PerfmonCounter ()
                        {
                            ColumnIndex = column++,
                            Host = p[2].Trim (settings.InfluxDB.InfluxReserved.ReplaceReservedWith),
                            PerformanceObject = p[3].Trim (settings.InfluxDB.InfluxReserved.ReplaceReservedWith),
                            CounterName = p[4].Trim (settings.InfluxDB.InfluxReserved.ReplaceReservedWith)
                        }));
            return perfCounters;
        }

        private static List<PerfmonCounter> FilterPerfmonLogColumns(List<PerfmonCounter> columns, List<PerfmonCounter> filterColumns, Dictionary<string, List<string>> dbStructure)
        {
            switch ( settings.PerfmonFile.Filter )
            {
                case Filters.Measurement:
                    return columns.Where (p => dbStructure.ContainsKey (p.PerformanceObject)).ToList ();
                case Filters.Field:
                    return columns.Where (p => dbStructure.ContainsKey (p.PerformanceObject) && dbStructure[p.PerformanceObject].Contains (p.CounterName)).ToList ();
                case Filters.Columns:
                    return columns.Where (p => filterColumns.Any (f => p.PerformanceObject == f.PerformanceObject && p.CounterName == f.CounterName)).ToList ();
            }
            return columns;
        }

        private static async Task<bool> ProcessPerfmonLogLine(string line, IEnumerable<IGrouping<string, PerfmonCounter>> perfGroup, int minOffset, Regex pattern, InfluxDBClient client)
        {
            StringBuilder content = new StringBuilder ();
            DateTime timeStamp;

            var columns = pattern.Split (line.Replace ("\"", ""));
            var columnCount = columns.Count ();

            if ( !DateTime.TryParseExact (columns[0], settings.PerfmonFile.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeStamp) )
                throw new FormatException ("Couldn't parse " + columns[0] + " using format " + settings.PerfmonFile.TimeFormat + ", check -timeformat argument");
            var epoch = timeStamp.AddMinutes (minOffset).ToEpoch (settings.PerfmonFile.Precision);

            double value = 0.0;
            content.Clear ();
            var lineStartIndex = 0;

            foreach ( var group in perfGroup )
            {
                foreach ( var hostGrp in group.GroupBy (p => p.Host) )
                {
                    lineStartIndex = content.Length;
                    content.AppendFormat ("{0},Host={1}", group.Key, hostGrp.Key);

                    if ( settings.PerfmonFile.DefaultTags.Tags != null )
                        content.AppendFormat (",{0} ", settings.PerfmonFile.DefaultTags.Tags.ToString ());
                    else
                        content.Append (" ");

                    var useCounter = false;

                    foreach ( var counter in hostGrp )
                    {
                        if ( !String.IsNullOrWhiteSpace (columns[counter.ColumnIndex]) && Double.TryParse (columns[counter.ColumnIndex], out value) )
                        {
                            content.AppendFormat ("{0}={1:0.00},", counter.CounterName, value);
                            useCounter = true;

                        }
                    }

                    if ( useCounter )
                        content.AppendFormat (" {0}\n", epoch);
                    else
                    {
                        content.Length = lineStartIndex;
                    }
                }
            }

            //each group will have an ending comma which is not needed
            content.Replace (", ", " ");
            //remove last \n
            content.Remove (content.Length - 1, 1);
            //synchronous processing
            if ( await client.PostRawValueAsync (settings.InfluxDB.DatabaseName, TimePrecision.Seconds, content.ToString ()) )
                return true;
            else
            {
                return false;
            }

        }

        private static async Task<ExitCode> ProcessGenericFile(string InputFileName, string tableName, InfluxDBClient client)
        {
            try
            {
                StringBuilder content = new StringBuilder ();
                var lineCount = 0;
                var failedCount = 0;
                Stopwatch stopwatch = new Stopwatch ();
                stopwatch.Start ();

                List<GenericColumn> columnHeaders;

                var firstLine = File.ReadLines (InputFileName).FirstOrDefault ();
                columnHeaders = ParseGenericColumns (firstLine);

                Dictionary<string, List<string>> dbStructure;
                if ( settings.GenericFile.Filter != Filters.None )
                {
                    var filterColumns = ParseGenericColumns (settings.GenericFile.ColumnsFilter.Columns.ToString ());

                    dbStructure = await client.GetInfluxDBStructureAsync (settings.InfluxDB.DatabaseName);
                    columnHeaders = FilterGenericColumns (columnHeaders, filterColumns, dbStructure);

                }

                var failureReasons = new Dictionary<Type, FailureTracker> ();


                //Parallel.ForEach (File.ReadLines (inputFileName).Skip (1), (string line) =>
                foreach ( var line in File.ReadLines (InputFileName).Skip (1) )
                {
                    try
                    {
                        if ( !await ProcessGenericLine (line, columnHeaders, pattern, client) )
                            failedCount++;
                    }
                    catch ( Exception e )
                    {
                        failedCount++;
                        var type = e.GetType ();
                        if ( !failureReasons.ContainsKey (type) )
                            failureReasons.Add (type, new FailureTracker () { ExceptionType = type, Message = e.Message });
                        failureReasons[type].LineNumbers.Add (lineCount);
                    }

                    lineCount++;

                    if ( failedCount > 0 )
                        Console.Write ("\r{0} Processed {1}, Failed - {2}                        ", stopwatch.Elapsed.ToString (@"hh\:mm\:ss"), lineCount, failedCount);
                    else
                        Console.Write ("\r{0} Processed {1}                          ", stopwatch.Elapsed.ToString (@"hh\:mm\:ss"), lineCount);

                }
                lineCount = 0;

                stopwatch.Stop ();
                if ( failedCount > 0 )
                {
                    Console.WriteLine ("\n Done!! Processed {0}, failed to insert {1}", lineCount, failedCount);
                    foreach ( var f in failureReasons.Values )
                        Console.WriteLine ("{0}:{1} - {2} : {3}", f.ExceptionType, f.Message, f.Count, String.Join (",", f.LineNumbers));
                    if ( failedCount == lineCount )
                        return ExitCode.UnableToProcess;
                    else
                        return ExitCode.ProcessedWithErrors;
                }

            }
            catch ( Exception e )
            {
                Console.Error.WriteLine ("\r\nError!! {0}:{1} - {2}", e.GetType ().Name, e.Message, e.StackTrace);
                return ExitCode.UnknownError;
            }
            return ExitCode.Success;
        }

        private static List<GenericColumn> ParseGenericColumns(string headerLine)
        {
            var columns = new List<GenericColumn> ();
            int index = 0;
            columns.AddRange (pattern.Split (headerLine).Select (s => new GenericColumn () { ColumnIndex = index++, ColumnHeader = s.Replace (settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray (), settings.InfluxDB.InfluxReserved.ReplaceReservedWith) }));
            return columns;
        }

        private static List<GenericColumn> FilterGenericColumns(List<GenericColumn> columns, List<GenericColumn> filterColumns, Dictionary<string, List<string>> dbStructure)
        {
            switch ( settings.GenericFile.Filter )
            {
                case Filters.Measurement:
                    return columns.Where (p => dbStructure.ContainsKey (settings.GenericFile.TableName)).ToList ();
                case Filters.Field:
                    return columns.Where (p => dbStructure.ContainsKey (settings.GenericFile.TableName) && dbStructure[settings.GenericFile.TableName].Contains (p.ColumnHeader)).ToList ();
                case Filters.Columns:
                    return columns.Where (p => filterColumns.Any (f => f.ColumnHeader == p.ColumnHeader)).ToList ();
            }
            return columns;
        }

        private async static Task<bool> ProcessGenericLine(string line, List<GenericColumn> columnHeaders, Regex pattern, InfluxDBClient client)
        {
            Dictionary<string, double> values = new Dictionary<string, double> ();
            StringBuilder tagsCollection = new StringBuilder ();

            DateTime timeStamp;

            var columns = pattern.Split (line.Replace ("\"", ""));
            var columnCount = columns.Count ();

            if ( !DateTime.TryParseExact (columns[0], settings.GenericFile.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeStamp) )
                throw new FormatException ("Couldn't parse " + columns[0] + " using format " + settings.GenericFile.TimeFormat + ", check -timeformat argument");
            var epoch = timeStamp.AddMinutes (settings.GenericFile.UtcOffset).ToEpoch (settings.GenericFile.Precision);

            double value = 0.0;

            foreach ( var c in columnHeaders.Skip (1) )
            {
                if ( Double.TryParse (columns[c.ColumnIndex], out value) )
                {
                    values.Add (c.ColumnHeader, Math.Round (value, 2));
                    //break;
                }
                else
                    tagsCollection.AppendFormat ("{0}={1},", c.ColumnHeader, columns[c.ColumnIndex].Replace (settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray (), settings.InfluxDB.InfluxReserved.ReplaceReservedWith));
            }

            if ( settings.GenericFile.DefaultTags.Tags != null )
                tagsCollection.Append (settings.GenericFile.DefaultTags.Tags.ToString ());
            else
                tagsCollection.Remove (tagsCollection.Length - 1, 1);

            if ( await client.PostValuesAsync (settings.InfluxDB.DatabaseName, settings.GenericFile.TableName, epoch, settings.GenericFile.Precision, tagsCollection.ToString (), values) )
                return true;
            else
            {
                return false;
            }

        }
    }
}
