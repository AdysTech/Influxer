using AdysTech.InfluxDB.Client.Net;
using AdysTech.Influxer.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    class PerfmonFile
    {
        private InfluxerConfigSection settings;
        private Regex pattern;
        private Dictionary<string, string> defaultTags;
        private int minOffset;

        public PerfmonFile()
        {
            settings = InfluxerConfigSection.GetCurrentOrDefault ();
            pattern = new Regex (settings.PerfmonFile.ColumnSplitter, RegexOptions.Compiled);
            defaultTags = new Dictionary<string, string> ();
            if ( settings.PerfmonFile.DefaultTags.Tags != null && settings.PerfmonFile.DefaultTags.Tags.Count > 0 )
            {
                foreach ( var tag in settings.PerfmonFile.DefaultTags.Tags )
                {
                    var tags = tag.Split ('=');
                    defaultTags.Add (tags[0], tags[1]);
                }
            }
        }

        public async Task<ExitCode> ProcessPerfMonLog(string InputFileName, InfluxDBClient client)
        {

            try
            {
                var lineCount = 0;
                var failedCount = 0;
                var pointCount = 0;
                StringBuilder content = new StringBuilder ();
                var failureReasons = new Dictionary<Type, FailureTracker> ();

                Stopwatch stopwatch = new Stopwatch ();
                stopwatch.Start ();


                var firstLine = File.ReadLines (InputFileName).FirstOrDefault ();

                var firstCol = firstLine.Substring (0, firstLine.IndexOf (','));
                if ( !firstCol.Contains ("PDH-CSV") )
                    throw new InvalidDataException ("Input file is not a Standard Perfmon csv file");
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

                List<IInfluxDatapoint> points = null;

                //Parallel.ForEach (File.ReadLines (inputFileName).Skip (1), (string line) =>
                foreach ( var line in File.ReadLines (InputFileName).Skip (1) )
                {
                    lineCount++;
                    try
                    {
                        var linePoints = ProcessPerfmonLogLine (line, perfGroup);
                        if ( linePoints == null || linePoints.Count == 0 )
                            failedCount++;
                        else
                        {
                            pointCount += linePoints.Count;
                            if ( points == null )
                                points = linePoints;
                            else
                                points.AddRange (linePoints);

                            if ( points.Count >= settings.InfluxDB.PointsInSingleBatch )
                            {
                                var result = await client.PostPointsAsync (settings.InfluxDB.DatabaseName, points);
                                if ( result )
                                    points = null;
                                else if ( points.Count >= settings.InfluxDB.PointsInSingleBatch * 2 )
                                    throw new InvalidOperationException ("InfluxDB is not able to accept points!! Please check InfluxDB logs for error details!");
                            }
                        }
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

                if ( await client.PostPointsAsync (settings.InfluxDB.DatabaseName, points) )
                    points.Clear ();

                pecrfCounters.Clear ();
                stopwatch.Stop ();
                if ( failedCount > 0 )
                {
                    Console.WriteLine ("\n Done!! Processed {0}, failed to insert {1} lines, Total Points: {2}", lineCount, failedCount, pointCount);
                    foreach ( var f in failureReasons.Values )
                        Console.WriteLine ("{0}:{1} - {2} : {3}", f.ExceptionType, f.Message, f.Count, String.Join (",", f.LineNumbers));
                    if ( failedCount == lineCount )
                        return ExitCode.UnableToProcess;
                    else
                        return ExitCode.ProcessedWithErrors;
                }
                else
                    Console.WriteLine ("\n Done!! Processed {0} lines with {1} points", lineCount, pointCount);

            }
            catch ( Exception e )
            {
                Console.Error.WriteLine ("\r\nError!! {0}:{1} - {2}", e.GetType ().Name, e.Message, e.StackTrace);
                return ExitCode.UnknownError;
            }
            return ExitCode.Success;
        }

        private List<PerfmonCounter> ParsePerfMonFileHeader(string headerLine, bool quoted = true)
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

        private List<PerfmonCounter> FilterPerfmonLogColumns(List<PerfmonCounter> columns, List<PerfmonCounter> filterColumns, Dictionary<string, List<string>> dbStructure)
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

        private List<IInfluxDatapoint> ProcessPerfmonLogLine(string line, IEnumerable<IGrouping<string, PerfmonCounter>> perfGroup)
        {
            var columns = pattern.Split (line.Replace ("\"", ""));
            var columnCount = columns.Count ();

            DateTime timeStamp;
            if ( !DateTime.TryParseExact (columns[0], settings.PerfmonFile.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeStamp) )
                throw new FormatException ("Couldn't parse " + columns[0] + " using format " + settings.PerfmonFile.TimeFormat + ", check -timeformat argument");
            var utcTime = timeStamp.AddMinutes (minOffset);

            var points = new List<IInfluxDatapoint> ();

            foreach ( var group in perfGroup )
            {
                foreach ( var hostGrp in group.GroupBy (p => p.Host) )
                {
                    var point = new InfluxDatapoint<double> ();
                    if ( defaultTags.Count > 0 ) point.InitializeTags (defaultTags);
                    point.Tags.Add ("Host", hostGrp.Key);
                    point.MeasurementName = group.Key;
                    point.UtcTimestamp = utcTime;

                    double value = 0.0;

                    foreach ( var counter in hostGrp )
                    {
                        if ( !String.IsNullOrWhiteSpace (columns[counter.ColumnIndex]) && Double.TryParse (columns[counter.ColumnIndex], out value) )
                            point.Fields.Add (counter.CounterName, value);
                    }
                    if ( point.Fields.Count > 0 )
                        points.Add (point);
                }
            }

            return points;

        }

    }
}
