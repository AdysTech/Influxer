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
            settings = InfluxerConfigSection.GetCurrentOrDefault();
            pattern = new Regex(settings.PerfmonFile.ColumnSplitter, RegexOptions.Compiled);
            defaultTags = new Dictionary<string, string>();
            if (settings.PerfmonFile.DefaultTags.Tags != null && settings.PerfmonFile.DefaultTags.Tags.Count > 0)
            {
                foreach (var tag in settings.PerfmonFile.DefaultTags.Tags)
                {
                    var tags = tag.Split('=');
                    defaultTags.Add(tags[0], tags[1]);
                }
            }
        }

        public async Task<ExitCode> ProcessPerfMonLog(string InputFileName, InfluxDBClient client)
        {
            int linesProcessed = 0;
            int failedLines = 0;
            int pointsFound = 0;
            int failedReqCount = 0;
            try
            {

                var failureReasons = new Dictionary<Type, FailureTracker>();

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();


                var firstLine = File.ReadLines(InputFileName).FirstOrDefault();

                var firstCol = firstLine.Substring(0, firstLine.IndexOf(','));
                if (!firstCol.Contains("PDH-CSV"))
                    throw new InvalidDataException("Input file is not a Standard Perfmon csv file");
                var x = Regex.Matches(firstCol, "([-0-9]+)");
                if (x.Count > 0)
                    minOffset = int.Parse(x[3].ToString());

                //get the column headers
                List<PerfmonCounter> pecrfCounters;
                try
                {
                    pecrfCounters = ParsePerfMonFileHeader(firstLine);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException("Unable to parse file headers", ex);
                }



                InfluxDatabase dbStructure;
                IEnumerable<IGrouping<string, PerfmonCounter>> perfGroup;
                if (settings.PerfmonFile.Filter != Filters.None)
                {
                    var filterColumns = ParsePerfMonFileHeader(settings.PerfmonFile.ColumnsFilter.Columns.ToString(), false);
                    dbStructure = await client.GetInfluxDBStructureAsync(settings.InfluxDB.DatabaseName);
                    perfGroup = FilterPerfmonLogColumns(pecrfCounters, filterColumns, dbStructure).GroupBy(p => p.PerformanceObject);
                }
                else
                {
                    perfGroup = pecrfCounters.GroupBy(p => p.PerformanceObject);
                }

                List<IInfluxDatapoint> points = null, retryQueue = new List<IInfluxDatapoint>();

                //Parallel.ForEach (File.ReadLines (inputFileName).Skip (1), (string line) =>
                foreach (var line in File.ReadLines(InputFileName).Skip(1))
                {
                    try
                    {
                        var linePoints = ProcessPerfmonLogLine(line, perfGroup);
                        linesProcessed++;

                        if (linePoints == null || linePoints.Count == 0)
                            failedLines++;
                        else
                        {
                            pointsFound += linePoints.Count;

                            if (points == null)
                                points = linePoints;
                            else
                                points.AddRange(linePoints);

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
                                    failedReqCount++;
                                    //add failed to retry queue
                                    retryQueue.AddRange(points.Where(p => p.Saved != true));

                                    //avoid failing on too many points
                                    if (failedReqCount > 4)
                                        break;
                                }
                                //a point will be either posted to Influx or in retry queue
                                points = null;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        failedLines++;
                        var type = e.GetType();
                        if (!failureReasons.ContainsKey(type))
                            failureReasons.Add(type, new FailureTracker() { ExceptionType = type, Message = e.Message });
                        failureReasons[type].LineNumbers.Add(linesProcessed);
                    }

                    if (failedLines > 0 || retryQueue.Count > 0)
                        Console.Write("\r{0} Processed:- {1} lines, {2} points, Failed:- {3} lines, {4} points     ", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), linesProcessed, pointsFound, failedLines, retryQueue.Count);
                    else
                        Console.Write("\r{0} Processed:- {1} lines, {2} points", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), linesProcessed, pointsFound);

                }

                //if we reached here due to repeated failures
                if (retryQueue.Count >= settings.InfluxDB.PointsInSingleBatch * 3 || failedReqCount > 3)
                    throw new InvalidOperationException("InfluxDB is not able to accept points!! Please check InfluxDB logs for error details!");

                //finally few points may be left out which were not processed (say 10 points left, but we check for 100 points in a batch)
                if (points != null)
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

                if (retryQueue.Count > 0)
                {
                    Console.WriteLine("\n {0} Retrying {1} failed points", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), retryQueue.Count);
                    if (await client.PostPointsAsync(settings.InfluxDB.DatabaseName, retryQueue))
                        retryQueue.Clear();
                    else if (retryQueue.Count >= settings.InfluxDB.PointsInSingleBatch * 3 || ++failedReqCount > 4)
                        throw new InvalidOperationException("InfluxDB is not able to accept points!! Please check InfluxDB logs for error details!");
                }

                pecrfCounters.Clear();
                stopwatch.Stop();
                if (failedLines > 0 || retryQueue.Count > 0)
                {
                    Console.Write("\r{0} Done!! Processed:- {1} lines, {2} points, Failed:- {3} lines, {4} points", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), linesProcessed, pointsFound, failedLines, retryQueue.Count);
                    Console.Error.WriteLine("Process Started {0}, Input {1}, Processed:- {2} lines, {3} points, Failed:- {4} lines, {5} points", (DateTime.Now - stopwatch.Elapsed), InputFileName, linesProcessed, pointsFound, failedLines, retryQueue.Count);
                    foreach (var f in failureReasons.Values)
                        Console.Error.WriteLine("{0} lines ({1}) failed due to {2} ({3})", f.Count, String.Join(",", f.LineNumbers), f.ExceptionType, f.Message);
                    if (failedLines == linesProcessed || pointsFound == retryQueue.Count)
                        return ExitCode.UnableToProcess;
                    else
                        return ExitCode.ProcessedWithErrors;
                }
                else
                    Console.WriteLine("\n Done!! Processed:- {0} lines, {1} points", linesProcessed, pointsFound);
            }

            catch (Exception e)
            {
                Console.Error.WriteLine("Failed to process {0}", InputFileName);
                Console.Error.WriteLine("\r\nError!! {0}:{1} - {2}", e.GetType().Name, e.Message, e.StackTrace);
                return ExitCode.UnknownError;
            }
            return ExitCode.Success;
        }

        private List<PerfmonCounter> ParsePerfMonFileHeader(string headerLine, bool quoted = true)
        {
            List<PerfmonCounter> perfCounters = new List<PerfmonCounter>();
            if (String.IsNullOrWhiteSpace(headerLine)) return perfCounters;
            var columns = pattern.Split(headerLine);
            var column = 1;

            perfCounters.AddRange(columns.Skip(quoted ? 1 : 0).Where(s => quoted ? s.StartsWith("\"\\") : s.StartsWith("\\")).Select(p =>
              p.Replace(settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray(), settings.InfluxDB.InfluxReserved.ReplaceReservedWith).Split('\\')).Select(p =>
              new PerfmonCounter()
                  {
                      ColumnIndex = column++,
                      Host = p[2].Trim(settings.InfluxDB.InfluxReserved.ReplaceReservedWith),
                      PerformanceObject = p[3].Trim(settings.InfluxDB.InfluxReserved.ReplaceReservedWith),
                      CounterName = p[4].Trim(settings.InfluxDB.InfluxReserved.ReplaceReservedWith)
                  }));
            return perfCounters;
        }

        private List<PerfmonCounter> FilterPerfmonLogColumns(List<PerfmonCounter> columns, List<PerfmonCounter> filterColumns, InfluxDatabase dbStructure)
        {
            switch (settings.PerfmonFile.Filter)
            {
                case Filters.Measurement:
                    return columns.Where(p => dbStructure.Measurements.Any(m => m.Name == p.PerformanceObject)).ToList();
                case Filters.Field:
                    return columns.Where(p => dbStructure.Measurements.Any(m => m.Name == p.PerformanceObject) && dbStructure.Measurements.FirstOrDefault(m => m.Name == p.PerformanceObject).Fields.Any(f => f == p.CounterName)).ToList();
                case Filters.Columns:
                    return columns.Where(p => filterColumns.Any(f => p.PerformanceObject == f.PerformanceObject && p.CounterName == f.CounterName)).ToList();
            }
            return columns;
        }

        private List<IInfluxDatapoint> ProcessPerfmonLogLine(string line, IEnumerable<IGrouping<string, PerfmonCounter>> perfGroup)
        {
            var columns = pattern.Split(line.Replace("\"", ""));
            var columnCount = columns.Count();

            DateTime timeStamp;
            if (!DateTime.TryParseExact(columns[0], settings.PerfmonFile.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeStamp))
                throw new FormatException("Couldn't parse " + columns[0] + " using format " + settings.PerfmonFile.TimeFormat + ", check -timeformat argument");
            var utcTime = timeStamp.AddMinutes(minOffset);

            var points = new List<IInfluxDatapoint>();

            foreach (var group in perfGroup)
            {
                foreach (var hostGrp in group.GroupBy(p => p.Host))
                {
                    var point = new InfluxDatapoint<double>();
                    if (defaultTags.Count > 0) point.InitializeTags(defaultTags);
                    point.Tags.Add("Host", hostGrp.Key);
                    point.MeasurementName = group.Key;
                    point.UtcTimestamp = utcTime;

                    double value = 0.0;

                    foreach (var counter in hostGrp)
                    {
                        if (!String.IsNullOrWhiteSpace(columns[counter.ColumnIndex]) && Double.TryParse(columns[counter.ColumnIndex], out value))
                        {
                            //Perfmon file can have duplicate columns!!
                            if (point.Fields.ContainsKey(counter.CounterName))
                                point.Fields[counter.CounterName] = value;
                            else
                                point.Fields.Add(counter.CounterName, value);

                        }
                    }
                    if (point.Fields.Count > 0)
                        points.Add(point);
                }
            }

            return points;

        }

    }
}
