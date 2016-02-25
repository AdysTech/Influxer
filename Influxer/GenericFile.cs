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
            settings = InfluxerConfigSection.GetCurrentOrDefault();
            pattern = new Regex(settings.GenericFile.ColumnSplitter, RegexOptions.Compiled);
            defaultTags = new Dictionary<string, string>();
            if (settings.GenericFile.DefaultTags.Tags != null && settings.GenericFile.DefaultTags.Tags.Count > 0)
            {
                foreach (var tag in settings.GenericFile.DefaultTags.Tags)
                {
                    var tags = tag.Split('=');
                    defaultTags.Add(tags[0], tags[1]);
                }
            }
        }

        public async Task<ExitCode> ProcessGenericFile(string InputFileName, string tableName, InfluxDBClient client)
        {
            var linesProcessed = 0;
            var failedLines = 0;
            int failedReqCount = 0;

            try
            {

                if (settings.GenericFile.HeaderMissing && settings.GenericFile.ColumnLayout.Count == 0)
                {
                    Console.WriteLine("Header missing, but no columns defined in configuration. Cannot proceed!!");
                    Console.Error.WriteLine("Header missing, but no columns defined in configuration. Cannot proceed!!");
                    return ExitCode.InvalidArgument;
                }

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();


                List<GenericColumn> columnHeaders = new List<GenericColumn>();

                if (!settings.GenericFile.HeaderMissing)
                {
                    var firstLine = File.ReadLines(InputFileName).Skip(settings.GenericFile.HeaderRow - 1).FirstOrDefault();
                    var columns = ParseGenericColumns(firstLine);
                    foreach (var c in columns)
                    {
                        if (!String.IsNullOrWhiteSpace(settings.GenericFile.ColumnLayout[c.ColumnIndex].NameInFile) && settings.GenericFile.ColumnLayout[c.ColumnIndex].NameInFile != c.ColumnHeader)
                        {
                            Console.WriteLine("Column Mismatch: Column[%0] defined in configuration %1, found %2. Cannot proceed!!", c.ColumnIndex, settings.GenericFile.ColumnLayout[c.ColumnIndex].NameInFile, c.ColumnHeader);
                            Console.Error.WriteLine("Column Mismatch: Column[%0] defined in configuration %1, found %2. Cannot proceed!!", c.ColumnIndex, settings.GenericFile.ColumnLayout[c.ColumnIndex].NameInFile, c.ColumnHeader);
                            return ExitCode.InvalidArgument;
                        }

                        if (!settings.GenericFile.ColumnLayout[c.ColumnIndex].Skip)
                        {
                            c.ColumnHeader = settings.GenericFile.ColumnLayout[c.ColumnIndex].InfluxName;
                            columnHeaders.Add(c);
                        }
                    }
                }
                else
                {
                    var index = 0;
                    foreach (ColumnConfig c in settings.GenericFile.ColumnLayout)
                    {
                        if (!c.Skip)
                            columnHeaders.Add(new GenericColumn() { ColumnHeader = c.InfluxName, ColumnIndex = index, Type = c.DataType });
                        index++;
                    }
                }

                Dictionary<string, List<string>> dbStructure;
                if (settings.GenericFile.Filter != Filters.None)
                {
                    if (settings.GenericFile.Filter == Filters.Columns && settings.GenericFile.ColumnLayout != null && settings.GenericFile.ColumnLayout.Count > 0)
                        Console.WriteLine("Column Filtering is not applicable when columns are defined in Config file. Use the Skip attribute on each column to filter them");

                    var filterColumns = ParseGenericColumns(settings.GenericFile.ColumnsFilter.Columns.ToString());

                    dbStructure = await client.GetInfluxDBStructureAsync(settings.InfluxDB.DatabaseName);
                    columnHeaders = FilterGenericColumns(columnHeaders, filterColumns, dbStructure);

                }

                var validity = ValidateData(InputFileName, columnHeaders);

                var failureReasons = new Dictionary<Type, FailureTracker>();

                List<IInfluxDatapoint> points = new List<IInfluxDatapoint>(), retryQueue = new List<IInfluxDatapoint>();

                foreach (var line in File.ReadLines(InputFileName).Skip(settings.GenericFile.HeaderRow + settings.GenericFile.SkipRows))
                {
                    if (String.IsNullOrWhiteSpace(line) || (!String.IsNullOrEmpty(settings.GenericFile.CommentMarker) && line.StartsWith(settings.GenericFile.CommentMarker)))
                        continue;

                    try
                    {
                        var point = ProcessGenericLine(line, columnHeaders);
                        if (point == null)
                            failedLines++;
                        else
                            points.Add(point);

                        if (points.Count >= settings.InfluxDB.PointsInSingleBatch)
                        {
                            bool result = false;
                            try
                            {
                                result = await client.PostPointsAsync(settings.InfluxDB.DatabaseName, points);
                            }
                            catch (ServiceUnavailableException)
                            {
                                result = false;
                            }

                            if (result)
                            {
                                failedReqCount = 0;
                            }
                            else
                            {
                                //add failed to retry queue
                                retryQueue.AddRange(points.Where(p => p.Saved != true));

                                //avoid failing on too many points
                                if (++failedReqCount > 3)
                                    break;
                            }
                            //a point will be either posted to Influx or in retry queue
                            points.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        failedLines++;
                        var type = e.GetType();
                        if (!failureReasons.ContainsKey(type))
                            failureReasons.Add(type, new FailureTracker() { ExceptionType = type, Message = e.Message });
                        failureReasons[type].LineNumbers.Add(linesProcessed + settings.GenericFile.HeaderRow + settings.GenericFile.SkipRows + 1);
                    }

                    linesProcessed++;

                    if (failedLines > 0 || retryQueue.Count > 0)
                        Console.Write("\r{0} Processed {1}, Failed {2}, Queued {3}                        ", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), linesProcessed, failedLines, retryQueue.Count);
                    else
                        Console.Write("\r{0} Processed {1}                          ", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), linesProcessed);

                }

                //if we reached here due to repeated failures
                if (retryQueue.Count >= settings.InfluxDB.PointsInSingleBatch * 3 || failedReqCount > 3)
                    throw new InvalidOperationException("InfluxDB is not able to accept points!! Please check InfluxDB logs for error details!");


                //finally few points may be left out which were not processed (say 10 points left, but we check for 100 points in a batch)
                if (points != null && points.Count > 0)
                {

                    if (await client.PostPointsAsync(settings.InfluxDB.DatabaseName, points))
                        points.Clear();
                    else
                    {
                        failedReqCount++;
                        //add failed to retry queue
                        retryQueue.AddRange(points.Where(p => p.Saved != true));
                    }
                }

                //retry all previously failed points
                if (retryQueue.Count > 0)
                {
                    Console.WriteLine("\n {0} Retrying {1} failed points", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), retryQueue.Count);
                    if (await client.PostPointsAsync(settings.InfluxDB.DatabaseName, retryQueue))
                        retryQueue.Clear();
                    else
                    {
                        failedLines += retryQueue.Count;
                        if (retryQueue.Count >= settings.InfluxDB.PointsInSingleBatch * 3 || ++failedReqCount > 4)
                            throw new InvalidOperationException("InfluxDB is not able to accept points!! Please check InfluxDB logs for error details!");
                    }
                }

                stopwatch.Stop();
                if (failedLines > 0)
                {
                    Console.WriteLine("\n Done!! Processed {0}, failed to insert {1}", linesProcessed, failedLines);
                    Console.Error.WriteLine("Process Started {0}, Input {1}, Processed{2}, Failed:{3}", (DateTime.Now - stopwatch.Elapsed), InputFileName, linesProcessed, failedLines);
                    foreach (var f in failureReasons.Values)
                        Console.Error.WriteLine("{0} lines ({1}) failed due to {2} ({3})", f.Count, String.Join(",", f.LineNumbers), f.ExceptionType, f.Message);
                    if (failedLines == linesProcessed)
                        return ExitCode.UnableToProcess;
                    else
                        return ExitCode.ProcessedWithErrors;
                }

            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Failed to process {0}", InputFileName);
                Console.Error.WriteLine("\r\nError!! {0}:{1} - {2}", e.GetType().Name, e.Message, e.StackTrace);
                return ExitCode.UnknownError;
            }
            return ExitCode.Success;
        }

        private bool ValidateData(string InputFileName, List<GenericColumn> columnHeaders)
        {
            var lineNo = 0;
            if (settings.GenericFile.ValidateRows == 0)
                settings.GenericFile.ValidateRows = 1;

            foreach (var line in File.ReadLines(InputFileName).Skip(settings.GenericFile.HeaderRow + settings.GenericFile.SkipRows))
            {
                if (String.IsNullOrWhiteSpace(line) || (!String.IsNullOrEmpty(settings.GenericFile.CommentMarker) && line.StartsWith(settings.GenericFile.CommentMarker)))
                    continue;

                var columns = pattern.Split(line.Replace("\"", ""));
                double value = 0.0;

                foreach (var c in columnHeaders)
                {
                    if (c.ColumnIndex == settings.GenericFile.TimeColumn - 1)
                    {
                        DateTime timeStamp;
                        if (!DateTime.TryParseExact(columns[settings.GenericFile.TimeColumn - 1], settings.GenericFile.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeStamp))
                            throw new FormatException("Couldn't parse " + columns[0] + " using format " + settings.GenericFile.TimeFormat + ", check -timeformat argument");
                    }
                    else
                    {
                        if (c.Type == ColumnDataType.Unknown)
                        {
                            if (Double.TryParse(columns[c.ColumnIndex], out value))
                                c.Type = ColumnDataType.Field;
                            else
                                c.Type = ColumnDataType.Tag;
                        }
                        else
                        {
                            if ((Double.TryParse(columns[c.ColumnIndex], out value) && c.Type == ColumnDataType.Tag) || (c.Type == ColumnDataType.Field && double.IsNaN(value)))
                                throw new InvalidDataException(c.ColumnHeader + " has inconsistent data");
                        }
                    }
                }
                if (++lineNo == settings.GenericFile.ValidateRows)
                    break;
            }
            return true;
        }

        private List<GenericColumn> ParseGenericColumns(string headerLine)
        {
            var columns = new List<GenericColumn>();
            columns.AddRange(pattern.Split(headerLine).Select((s, i) => new GenericColumn() { ColumnIndex = i, ColumnHeader = s.Replace(settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray(), settings.InfluxDB.InfluxReserved.ReplaceReservedWith) }));
            return columns;
        }

        private List<GenericColumn> FilterGenericColumns(List<GenericColumn> columns, List<GenericColumn> filterColumns, Dictionary<string, List<string>> dbStructure)
        {
            switch (settings.GenericFile.Filter)
            {
                case Filters.Measurement:
                    return columns.Where(p => dbStructure.ContainsKey(settings.GenericFile.TableName)).ToList();
                case Filters.Field:
                    return columns.Where(p => dbStructure.ContainsKey(settings.GenericFile.TableName) && dbStructure[settings.GenericFile.TableName].Contains(p.ColumnHeader)).ToList();
                case Filters.Columns:
                    return columns.Where(p => filterColumns.Any(f => f.ColumnHeader == p.ColumnHeader)).ToList();
            }
            return columns;
        }

        private InfluxDatapoint<double> ProcessGenericLine(string line, List<GenericColumn> columnHeaders)
        {

            var columns = pattern.Split(line.Replace("\"", ""));
            var columnCount = columns.Count();

            InfluxDatapoint<double> point = new InfluxDatapoint<double>();
            point.Precision = settings.GenericFile.Precision;
            point.MeasurementName = settings.GenericFile.TableName;

            DateTime timeStamp;
            if (!DateTime.TryParseExact(columns[settings.GenericFile.TimeColumn - 1], settings.GenericFile.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeStamp))
                throw new FormatException("Couldn't parse " + columns[0] + " using format " + settings.GenericFile.TimeFormat + ", check -timeformat argument");
            point.UtcTimestamp = timeStamp.AddMinutes(settings.GenericFile.UtcOffset);

            point.InitializeTags(defaultTags);

            foreach (var c in columnHeaders)
            {
                if (c.ColumnIndex == settings.GenericFile.TimeColumn - 1) continue;

                double value = double.NaN;
                if (c.Type == ColumnDataType.Field)
                {
                    if (!Double.TryParse(columns[c.ColumnIndex], out value))
                        throw new InvalidDataException(c.ColumnHeader + " has inconsistent data, Unable to parse \"" + columns[c.ColumnIndex] + "\" as number");
                    point.Fields.Add(c.ColumnHeader, Math.Round(value, 2));
                }
                else if (c.Type == ColumnDataType.Tag)
                    point.Tags.Add(c.ColumnHeader, columns[c.ColumnIndex].Replace(settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray(), settings.InfluxDB.InfluxReserved.ReplaceReservedWith));
            }


            if (point.Fields.Count == 0)
                throw new InvalidDataException("No values found on the row to post to Influx");
            return point;
        }
    }
}
