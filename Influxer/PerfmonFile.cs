using AdysTech.InfluxDB.Client.Net;
using AdysTech.Influxer.Config;
using AdysTech.Influxer.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    public class PerfmonFile
    {
        private Dictionary<string, string> defaultTags;

        private int minOffset;

        private Regex pattern;

        private IInfluxRetentionPolicy policy = null;

        private InfluxerConfigSection settings;

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

        private List<IInfluxDatapoint> ProcessPerfmonLogLine(string line, IEnumerable<IGrouping<string, PerfmonCounter>> perfGroup)
        {
            var columns = pattern.Split(line.Replace("\"", ""));
            var columnCount = columns.Count();

            if (!DateTime.TryParseExact(columns[0], settings.PerfmonFile.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timeStamp))
                throw new FormatException("Couldn't parse " + columns[0] + " using format " + settings.PerfmonFile.TimeFormat + ", check -timeformat argument");
            var utcTime = timeStamp.AddMinutes(minOffset);

            var points = new List<IInfluxDatapoint>();

            foreach (var performanceObject in perfGroup)
            {
                foreach (var hostGrp in performanceObject.GroupBy(p => p.Host))
                {
                    if (settings.PerfmonFile.MultiMeasurements)
                    {
                        var point = new InfluxDatapoint<double>
                        {
                            Precision = TimePrecision.Milliseconds,
                            Retention = policy,
                            MeasurementName = performanceObject.Key,
                            UtcTimestamp = utcTime
                        };
                       
                        if (defaultTags.Count > 0) point.InitializeTags(defaultTags);
                        point.Tags.Add("Host", hostGrp.Key);

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
                    else
                    {
                        foreach (var counter in hostGrp)
                        {
                            if (!String.IsNullOrWhiteSpace(columns[counter.ColumnIndex]) && Double.TryParse(columns[counter.ColumnIndex], out double value))
                            {
                                var point = new InfluxDatapoint<double>()
                                {
                                    Precision = TimePrecision.Milliseconds,
                                    Retention = policy,
                                    MeasurementName = settings.InfluxDB.Measurement,
                                    UtcTimestamp = utcTime
                                };

                                if (defaultTags.Count > 0) point.InitializeTags(defaultTags);
                                point.Tags.Add("Host", hostGrp.Key);

                                point.Tags.Add("PerformanceObject", counter.PerformanceObject);
                                point.Tags.Add("PerformanceCounter", counter.CounterName);
                                point.Fields.Add("CounterValue", value);
                                points.Add(point);
                            }
                        }
                    }
                }
            }
            return points;
        }

        public PerfmonFile()
        {
            settings = InfluxerConfigSection.GetCurrentOrDefault();
            pattern = new Regex(settings.PerfmonFile.ColumnSplitter, RegexOptions.Compiled);
            defaultTags = new Dictionary<string, string>();
            if (settings.PerfmonFile.DefaultTags != null && settings.PerfmonFile.DefaultTags.Count > 0)
            {
                foreach (var tag in settings.PerfmonFile.DefaultTags)
                {
                    var tags = tag.Split('=');
                    defaultTags.Add(tags[0], tags[1]);
                }
            }
        }

        public async Task<ProcessStatus> ProcessPerfMonLog(string InputFileName, InfluxDBClient client)
        {
            ProcessStatus result = new ProcessStatus();
            int linesProcessed = 0;
            int failedLines = 0;

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

                IInfluxDatabase dbStructure;
                IEnumerable<IGrouping<string, PerfmonCounter>> perfGroup;
                if (settings.PerfmonFile.Filter != Filters.None)
                {
                    var filterColumns = ParsePerfMonFileHeader(settings.PerfmonFile.ColumnsFilter.ToString(), false);
                    dbStructure = await client.GetInfluxDBStructureAsync(settings.InfluxDB.DatabaseName);
                    perfGroup = FilterPerfmonLogColumns(pecrfCounters, filterColumns, dbStructure as InfluxDatabase).GroupBy(p => p.PerformanceObject);
                }
                else
                {
                    perfGroup = pecrfCounters.GroupBy(p => p.PerformanceObject);
                }

                List<IInfluxDatapoint> points = null, retryQueue = new List<IInfluxDatapoint>();

                if (settings.InfluxDB.RetentionDuration != 0 || !String.IsNullOrWhiteSpace(settings.InfluxDB.RetentionPolicy))
                {
                    var policies = await client.GetRetentionPoliciesAsync(settings.InfluxDB.DatabaseName);
                    //if duraiton is specified that takes precidence
                    if (settings.InfluxDB.RetentionDuration != 0)
                    {
                        policy = policies.FirstOrDefault(p => p.Duration.TotalMinutes == settings.InfluxDB.RetentionDuration);

                        if (policy == null)
                        {
                            policy = new InfluxRetentionPolicy()
                            {
                                Name = String.IsNullOrWhiteSpace(settings.InfluxDB.RetentionPolicy) ? $"InfluxerRetention_{settings.InfluxDB.RetentionDuration}min" : settings.InfluxDB.RetentionPolicy,
                                DBName = settings.InfluxDB.DatabaseName,
                                Duration = TimeSpan.FromMinutes(settings.InfluxDB.RetentionDuration),
                                IsDefault = false,
                                ReplicaN = 1
                            };
                            if (!await client.CreateRetentionPolicyAsync(policy))
                                throw new InvalidOperationException("Unable to create retention policy");
                        }
                    }
                    else if (!String.IsNullOrWhiteSpace(settings.InfluxDB.RetentionPolicy))
                    {
                        policy = policies.FirstOrDefault(p => p.Name == settings.InfluxDB.RetentionPolicy);
                        if (policy == null)
                            throw new ArgumentException("No Retention policy with Name {0} was found, and duration is not specified to create a new one!!", settings.InfluxDB.RetentionPolicy);
                    }
                }
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
                            result.PointsFound += linePoints.Count;

                            if (points == null)
                                points = linePoints;
                            else
                                points.AddRange(linePoints);

                            if (points.Count >= settings.InfluxDB.PointsInSingleBatch)
                            {
                                bool postresult = false;
                                try
                                {
                                    postresult = await client.PostPointsAsync(settings.InfluxDB.DatabaseName, points);
                                }
                                catch (ServiceUnavailableException)
                                {
                                    postresult = false;
                                }
                                if (postresult)
                                {
                                    failedReqCount = 0;
                                    result.PointsProcessed += points.Count(p => p.Saved);
                                }
                                else
                                {
                                    //add failed to retry queue
                                    retryQueue.AddRange(points.Where(p => p.Saved != true));
                                    result.PointsProcessed += points.Count(p => p.Saved);
                                    //avoid failing on too many points
                                    if (++failedReqCount > 4)
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
                        Logger.Log(LogLevel.Verbose, "\r{0} Processed:- {1} lines, {2} points, Failed:- {3} lines, {4} points     ", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), linesProcessed, result.PointsFound, failedLines, retryQueue.Count);
                    else
                        Logger.Log(LogLevel.Verbose, "\r{0} Processed:- {1} lines, {2} points", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), linesProcessed, result.PointsFound);
                }

                //if we reached here due to repeated failures
                if (retryQueue.Count >= settings.InfluxDB.PointsInSingleBatch * 3 || failedReqCount > 3)
                    throw new InvalidOperationException("InfluxDB is not able to accept points!! Please check InfluxDB logs for error details!");

                //finally few points may be left out which were not processed (say 10 points left, but we check for 100 points in a batch)
                if (points != null && points.Count > 0)
                {
                    if (await client.PostPointsAsync(settings.InfluxDB.DatabaseName, points))
                    {
                        result.PointsProcessed += points.Count;
                        points.Clear();
                    }
                    else
                    {
                        failedReqCount++;
                        //add failed to retry queue
                        retryQueue.AddRange(points.Where(p => p.Saved != true));
                        result.PointsProcessed += points.Count(p => p.Saved);
                    }
                }

                if (retryQueue.Count > 0)
                {
                    Logger.LogLine(LogLevel.Info, "\n {0} Retrying {1} failed points", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), retryQueue.Count);
                    if (await client.PostPointsAsync(settings.InfluxDB.DatabaseName, retryQueue))
                    {
                        result.PointsProcessed += retryQueue.Count;
                        retryQueue.Clear();
                    }
                    else if (retryQueue.Count >= settings.InfluxDB.PointsInSingleBatch * 3 || ++failedReqCount > 4)
                        throw new InvalidOperationException("InfluxDB is not able to accept points!! Please check InfluxDB logs for error details!");
                    else
                    {
                        result.PointsFailed += retryQueue.Count;
                    }
                }

                pecrfCounters.Clear();
                stopwatch.Stop();
                if (failedLines > 0 || retryQueue.Count > 0)
                {
                    Logger.Log(LogLevel.Verbose, "\r{0} Done!! Processed:- {1} lines, {2} points, Failed:- {3} lines, {4} points", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), linesProcessed, result.PointsFound, failedLines, retryQueue.Count);
                    Logger.LogLine(LogLevel.Error, "Process Started {0}, Input {1}, Processed:- {2} lines, {3} points, Failed:- {4} lines, {5} points", (DateTime.Now - stopwatch.Elapsed), InputFileName, linesProcessed, result.PointsFound, failedLines, retryQueue.Count);
                    foreach (var f in failureReasons.Values)
                        Logger.LogLine(LogLevel.Error, "{0} lines ({1}) failed due to {2} ({3})", f.Count, String.Join(",", f.LineNumbers), f.ExceptionType, f.Message);
                    if (failedLines == linesProcessed || result.PointsFound == retryQueue.Count)
                        result.ExitCode = ExitCode.UnableToProcess;
                    else
                        result.ExitCode = ExitCode.ProcessedWithErrors;
                }
                else
                {
                    result.ExitCode = ExitCode.Success;
                    Logger.LogLine(LogLevel.Info, "\n Done!! Processed:- {0} lines, {1} points", linesProcessed, result.PointsFound);
                }
            }
            catch (Exception e)
            {
                Logger.LogLine(LogLevel.Error, "Failed to process {0}", InputFileName);
                Logger.LogLine(LogLevel.Error, "\r\nError!! {0}:{1} - {2}", e.GetType().Name, e.Message, e.StackTrace);
                result.ExitCode = ExitCode.UnknownError;
            }

            return result;
        }
    }
}