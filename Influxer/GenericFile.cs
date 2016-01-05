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
    class GenericFile
    {
        private InfluxerConfigSection settings;
        private Regex pattern;
        private Dictionary<string, string> defaultTags;

        public GenericFile()
        {
            settings = InfluxerConfigSection.GetCurrentOrDefault ();
            pattern = new Regex (settings.GenericFile.ColumnSplitter, RegexOptions.Compiled);
            defaultTags = new Dictionary<string, string> ();
            if ( settings.GenericFile.DefaultTags.Tags != null && settings.GenericFile.DefaultTags.Tags.Count > 0 )
            {
                foreach ( var tag in settings.GenericFile.DefaultTags.Tags )
                {
                    var tags = tag.Split ('=');
                    defaultTags.Add (tags[0], tags[1]);
                }
            }
        }

        public async Task<ExitCode> ProcessGenericFile(string InputFileName, string tableName, InfluxDBClient client)
        {
            try
            {
                var lineCount = 0;
                var failedCount = 0;
                Stopwatch stopwatch = new Stopwatch ();
                stopwatch.Start ();

                List<GenericColumn> columnHeaders;

                var firstLine = File.ReadLines (InputFileName).Skip (settings.GenericFile.HeaderRow - 1).FirstOrDefault ();
                columnHeaders = ParseGenericColumns (firstLine);

                Dictionary<string, List<string>> dbStructure;
                if ( settings.GenericFile.Filter != Filters.None )
                {
                    var filterColumns = ParseGenericColumns (settings.GenericFile.ColumnsFilter.Columns.ToString ());

                    dbStructure = await client.GetInfluxDBStructureAsync (settings.InfluxDB.DatabaseName);
                    columnHeaders = FilterGenericColumns (columnHeaders, filterColumns, dbStructure);

                }

                var validity = ValidateData (InputFileName, columnHeaders);

                var failureReasons = new Dictionary<Type, FailureTracker> ();

                var points = new List<IInfluxDatapoint> ();

                //Parallel.ForEach (File.ReadLines (inputFileName).Skip (1), (string line) =>
                foreach ( var line in File.ReadLines (InputFileName).Skip (settings.GenericFile.HeaderRow + settings.GenericFile.SkipRows) )
                {
                    try
                    {
                        var point = ProcessGenericLine (line, columnHeaders);
                        if ( point == null )
                            failedCount++;
                        else
                            points.Add (point);

                        if ( points.Count >= settings.InfluxDB.PointsInSingleBatch )
                        {
                            var result = await client.PostPointsAsync (settings.InfluxDB.DatabaseName, points);
                            if ( result )
                                points.Clear ();
                            else
                            {
                                //keep only failed points in the list
                                points.RemoveAll (p => p.Saved == true);

                                //points that fail on first try will remain and sent again on second request to InfluxDBClient
                                Console.Error.WriteLine ("{0} points failed to write to Influx", points.Count);

                                //avoid retrying forever
                                if ( points.Count >= settings.InfluxDB.PointsInSingleBatch * 3 )
                                    break;
                            }
                        }
                    }
                    catch ( Exception e )
                    {
                        failedCount++;
                        var type = e.GetType ();
                        if ( !failureReasons.ContainsKey (type) )
                            failureReasons.Add (type, new FailureTracker () { ExceptionType = type, Message = e.Message });
                        failureReasons[type].LineNumbers.Add (lineCount + settings.GenericFile.HeaderRow + settings.GenericFile.SkipRows + 1);
                    }

                    lineCount++;

                    if ( failedCount > 0 )
                        Console.Write ("\r{0} Processed {1}, Failed - {2}                        ", stopwatch.Elapsed.ToString (@"hh\:mm\:ss"), lineCount, failedCount);
                    else
                        Console.Write ("\r{0} Processed {1}                          ", stopwatch.Elapsed.ToString (@"hh\:mm\:ss"), lineCount);

                }
                //finally few points may be left out which were not processed (say 10 points left, but we check for 100 points in a batch)
                if ( points != null )
                {
                    if ( await client.PostPointsAsync (settings.InfluxDB.DatabaseName, points) )
                        points.Clear ();
                    else
                    {
                        //any previously failed points will remain here, and we need to indicate this in return status
                        points.RemoveAll (p => p.Saved == true);

                        Console.Error.WriteLine ("{0} points failed to write to Influx", points.Count);
                        failedCount += points.Count;
                        if ( points.Count >= settings.InfluxDB.PointsInSingleBatch * 3 )
                            throw new InvalidOperationException ("InfluxDB is not able to accept points!! Please check InfluxDB logs for error details!");
                    }
                }

                stopwatch.Stop ();
                if ( failedCount > 0 )
                {
                    Console.WriteLine ("\n Done!! Processed {0}, failed to insert {1}", lineCount, failedCount);
                    Console.Error.WriteLine ("Process Started {0}, Input {1}, Processed{2}, Failed:{3}", ( DateTime.Now - stopwatch.Elapsed ), InputFileName, lineCount, failedCount);
                    foreach ( var f in failureReasons.Values )
                        Console.Error.WriteLine ("{0} lines ({1}) failed due to {2} ({3})", f.Count, String.Join (",", f.LineNumbers), f.ExceptionType, f.Message);
                    if ( failedCount == lineCount )
                        return ExitCode.UnableToProcess;
                    else
                        return ExitCode.ProcessedWithErrors;
                }

            }
            catch ( Exception e )
            {
                Console.Error.WriteLine ("Failed to process {0}", InputFileName);
                Console.Error.WriteLine ("\r\nError!! {0}:{1} - {2}", e.GetType ().Name, e.Message, e.StackTrace);
                return ExitCode.UnknownError;
            }
            return ExitCode.Success;
        }

        private bool ValidateData(string InputFileName, List<GenericColumn> columnHeaders)
        {
            var lineNo = 0;
            if ( settings.GenericFile.ValidateRows == 0 )
                settings.GenericFile.ValidateRows = 1;

            foreach ( var line in File.ReadLines (InputFileName).Skip (settings.GenericFile.HeaderRow + settings.GenericFile.SkipRows) )
            {
                var columns = pattern.Split (line.Replace ("\"", ""));
                double value = 0.0;

                foreach ( var c in columnHeaders.Skip (1) )
                {
                    if ( c.Type == GenericColumn.ColumnDataType.Unknown )
                    {
                        if ( Double.TryParse (columns[c.ColumnIndex], out value) )
                            c.Type = GenericColumn.ColumnDataType.Field;
                        else
                            c.Type = GenericColumn.ColumnDataType.Tag;
                    }
                    else
                    {
                        if ( ( Double.TryParse (columns[c.ColumnIndex], out value) && c.Type == GenericColumn.ColumnDataType.Tag ) || ( c.Type == GenericColumn.ColumnDataType.Field && double.IsNaN (value) ) )
                            throw new InvalidDataException (c.ColumnHeader + " has inconsistent data");
                    }
                }
                if ( ++lineNo == settings.GenericFile.ValidateRows )
                    break;
            }
            return true;
        }

        private List<GenericColumn> ParseGenericColumns(string headerLine)
        {
            var columns = new List<GenericColumn> ();
            columns.AddRange (pattern.Split (headerLine).Select ((s, i) => new GenericColumn () { ColumnIndex = i, ColumnHeader = s.Replace (settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray (), settings.InfluxDB.InfluxReserved.ReplaceReservedWith) }));
            return columns;
        }

        private List<GenericColumn> FilterGenericColumns(List<GenericColumn> columns, List<GenericColumn> filterColumns, Dictionary<string, List<string>> dbStructure)
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

        private InfluxDatapoint<double> ProcessGenericLine(string line, List<GenericColumn> columnHeaders)
        {

            var columns = pattern.Split (line.Replace ("\"", ""));
            var columnCount = columns.Count ();

            InfluxDatapoint<double> point = new InfluxDatapoint<double> ();
            point.Precision = settings.GenericFile.Precision;
            point.MeasurementName = settings.GenericFile.TableName;

            DateTime timeStamp;
            if ( !DateTime.TryParseExact (columns[0], settings.GenericFile.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeStamp) )
                throw new FormatException ("Couldn't parse " + columns[0] + " using format " + settings.GenericFile.TimeFormat + ", check -timeformat argument");
            point.UtcTimestamp = timeStamp.AddMinutes (settings.GenericFile.UtcOffset);

            point.InitializeTags (defaultTags);

            foreach ( var c in columnHeaders.Skip (1) )
            {
                double value = double.NaN;
                if ( c.Type == GenericColumn.ColumnDataType.Field )
                {
                    if ( !Double.TryParse (columns[c.ColumnIndex], out value) )
                        throw new InvalidDataException (c.ColumnHeader + " has inconsistent data, Unable to parse " + columns[c.ColumnIndex] + " as number");
                    point.Fields.Add (c.ColumnHeader, Math.Round (value, 2));
                }
                else if ( c.Type == GenericColumn.ColumnDataType.Tag )
                    point.Tags.Add (c.ColumnHeader, columns[c.ColumnIndex].Replace (settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray (), settings.InfluxDB.InfluxReserved.ReplaceReservedWith));
            }


            if ( point.Fields.Count == 0 )
                throw new InvalidDataException ("No values found on the row to post to Influx");
            return point;
        }
    }
}
