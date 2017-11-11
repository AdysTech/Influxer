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
    public class GenericFile
    {
        private Dictionary<string, string> defaultTags;
        private Regex pattern;
        private InfluxerConfigSection settings;

        private List<GenericColumn> FilterGenericColumns(List<GenericColumn> columns, List<GenericColumn> filterColumns, InfluxDatabase dbStructure)
        {
            switch (settings.GenericFile.Filter)
            {
                case Filters.Measurement:
                    return columns.Where(p => dbStructure.Measurements.Any(m => m.Name == settings.InfluxDB.Measurement)).ToList();

                case Filters.Field:
                    return columns.Where(p => dbStructure.Measurements.Any(m => m.Name == settings.InfluxDB.Measurement) &&
                    (dbStructure.Measurements.FirstOrDefault(m => m.Name == settings.InfluxDB.Measurement).Tags.Contains(p.ColumnHeader)
                    || dbStructure.Measurements.FirstOrDefault(m => m.Name == settings.InfluxDB.Measurement).Fields.Contains(p.ColumnHeader))).ToList();

                case Filters.Columns:
                    return columns.Where(p => filterColumns.Any(f => f.ColumnHeader == p.ColumnHeader)).ToList();
            }
            return columns;
        }

        private List<GenericColumn> ParseGenericColumns(string headerLine)
        {
            var columns = new List<GenericColumn>();
            columns.AddRange(pattern.Split(headerLine).Select((s, i) => new GenericColumn() { ColumnIndex = i, ColumnHeader = s.Replace(settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray(), settings.InfluxDB.InfluxReserved.ReplaceReservedWith) }));
            return columns;
        }

        private InfluxDatapoint<InfluxValueField> ProcessGenericLine(string line, List<GenericColumn> columnHeaders)
        {
            var columns = pattern.Split(line);
            var columnCount = columns.Count();
            var content = columns[settings.GenericFile.TimeColumn - 1].Replace("\"", "");

            InfluxDatapoint<InfluxValueField> point = new InfluxDatapoint<InfluxValueField>()
            {
                Precision = settings.GenericFile.Precision,
                MeasurementName = settings.InfluxDB.Measurement
            };
            point.InitializeTags(defaultTags);

            var pointData = new Dictionary<GenericColumn, string>();

            foreach (var c in columnHeaders)
            {
                content = columns[c.ColumnIndex].Replace("\"", "");

                if (c.HasAutoGenColumns)
                {
                    pointData.AddRange(c.SplitData(content));
                }
                else
                {
                    pointData.Add(c, content);
                }
            }

            foreach (var d in pointData)
            {
                content = d.Value;
                if (d.Key.HasTransformations && d.Key.CanTransform(content))
                    content = d.Key.Transform(d.Value);

                if (String.IsNullOrWhiteSpace(content)) continue;

                if (d.Key.ColumnIndex == settings.GenericFile.TimeColumn - 1)
                {
                    point.UtcTimestamp = ParseTimestamp(content);
                }
                else
                {
                    double value = double.NaN; bool boolVal = false;
                    if (d.Key.Type == ColumnDataType.NumericalField)
                    {
                        if (!Double.TryParse(content, out value) || double.IsNaN(value))
                            throw new InvalidDataException(d.Key.ColumnHeader + " has inconsistent data, Unable to parse \"" + content + "\" as number");
                        point.Fields.Add(d.Key.ColumnHeader, new InfluxValueField(Math.Round(value, 2)));
                    }
                    else if (d.Key.Type == ColumnDataType.StringField)
                    {
                        point.Fields.Add(d.Key.ColumnHeader, new InfluxValueField(content));
                    }
                    else if (d.Key.Type == ColumnDataType.BooleanField)
                    {
                        if (!Boolean.TryParse(content, out boolVal))
                            throw new InvalidDataException(d.Key.ColumnHeader + " has inconsistent data, Unable to parse \"" + content + "\" as Boolean");
                        point.Fields.Add(d.Key.ColumnHeader, new InfluxValueField(boolVal));
                    }
                    else if (d.Key.Type == ColumnDataType.Tag)
                        point.Tags.Add(d.Key.ColumnHeader, content.Replace(settings.InfluxDB.InfluxReserved.ReservedCharecters.ToCharArray(), settings.InfluxDB.InfluxReserved.ReplaceReservedWith));
                }
            }

            if (point.Fields.Count == 0)
                throw new InvalidDataException("No values found on the row to post to Influx");

            return point;
        }

        private DateTime ParseTimestamp(string content)
        {

            switch (settings.GenericFile.TimeFormatType)
            {
                case TimeForamtType.String:
                    if (!DateTime.TryParseExact(content, settings.GenericFile.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timeStamp))
                        throw new FormatException("Couldn't parse " + content + " using format " + settings.GenericFile.TimeFormat + ", check -timeformat argument");
                    return timeStamp.AddMinutes(settings.GenericFile.UtcOffset);
                case TimeForamtType.Binary:
                    if (long.TryParse(content, out long ts))
                        return DateTime.FromBinary(ts);
                    else
                        throw new FormatException("Couldn't parse " + content + " as a Binary timestamp, please check the data or -timetype/TimeformatType arguments");
                case TimeForamtType.Epoch:
                    if (long.TryParse(content, out long ep))
                        return ep.FromEpoch(settings.GenericFile.Precision);
                    else
                        throw new FormatException("Couldn't parse " + content + " as a epoch timestamp, please check the data or -timetype/TimeformatType arguments");
            }
            return DateTime.MinValue;
        }

        public List<GenericColumn> ColumnHeaders { get; private set; }

        public GenericFile()
        {
            settings = InfluxerConfigSection.GetCurrentOrDefault();
            pattern = new Regex(settings.GenericFile.ColumnSplitter, RegexOptions.Compiled);
            defaultTags = new Dictionary<string, string>();
            if (settings.GenericFile.DefaultTags != null && settings.GenericFile.DefaultTags.Count > 0)
            {
                foreach (var tag in settings.GenericFile.DefaultTags)
                {
                    var tags = tag.Split('=');
                    defaultTags.Add(tags[0], tags[1]);
                }
            }
        }

        public ProcessStatus GetFileLayout(string InputFileName)
        {
            ColumnHeaders = new List<GenericColumn>();
            if (settings.GenericFile.HeaderMissing && settings.GenericFile.ColumnLayout.Count == 0)
            {
                Logger.LogLine(LogLevel.Info, "Header missing, but no columns defined in configuration. Cannot proceed!!");
                Logger.LogLine(LogLevel.Error, "Header missing, but no columns defined in configuration. Cannot proceed!!");
                return new ProcessStatus() { ExitCode = ExitCode.InvalidArgument };
            }
            else if (!settings.GenericFile.HeaderMissing)
            {
                var firstLine = File.ReadLines(InputFileName).Skip(settings.GenericFile.HeaderRow - 1).FirstOrDefault();
                var columns = ParseGenericColumns(firstLine);
                if (settings.GenericFile.ColumnLayout.Count > 0)
                {
                    foreach (var c in columns)
                    {
                        if (!String.IsNullOrWhiteSpace(settings.GenericFile.ColumnLayout[c.ColumnIndex].NameInFile) && settings.GenericFile.ColumnLayout[c.ColumnIndex].NameInFile != c.ColumnHeader)
                        {
                            Logger.LogLine(LogLevel.Info, $"Column Mismatch: Column[{c.ColumnIndex}] defined in configuration {settings.GenericFile.ColumnLayout[c.ColumnIndex].NameInFile}, found {c.ColumnHeader}, Cannot proceed!!");
                            Logger.LogLine(LogLevel.Info, $"Column Mismatch: Column[{c.ColumnIndex}] defined in configuration {settings.GenericFile.ColumnLayout[c.ColumnIndex].NameInFile}, found {c.ColumnHeader}, Cannot proceed!!");

                            return new ProcessStatus() { ExitCode = ExitCode.InvalidArgument };
                        }

                        if (!settings.GenericFile.ColumnLayout[c.ColumnIndex].Skip)
                        {
                            c.ColumnHeader = settings.GenericFile.ColumnLayout[c.ColumnIndex].InfluxName;
                            c.Type = settings.GenericFile.ColumnLayout[c.ColumnIndex].DataType;
                            c.Config = settings.GenericFile.ColumnLayout[c.ColumnIndex];
                            ColumnHeaders.Add(c);
                        }
                    }
                }
                else
                {
                    foreach (var c in columns)
                    {
                        c.Config = new ColumnConfig() { NameInFile = c.ColumnHeader, InfluxName = c.ColumnHeader, DataType = c.Type };
                        settings.GenericFile.ColumnLayout.Add(c.Config);
                        ColumnHeaders.Add(c);
                    }
                }
            }
            else
            {
                var index = 0;
                foreach (ColumnConfig c in settings.GenericFile.ColumnLayout)
                {
                    if (!c.Skip)
                        ColumnHeaders.Add(new GenericColumn() { ColumnHeader = c.InfluxName, ColumnIndex = index, Type = c.DataType, Config = c });
                    index++;
                }
            }
            return new ProcessStatus() { ExitCode = ExitCode.Success };
        }

        public async Task<ProcessStatus> ProcessGenericFile(string InputFileName, InfluxDBClient client)
        {
            ProcessStatus result = new ProcessStatus();

            int failedReqCount = 0;

            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                var r = GetFileLayout(InputFileName);
                if (r.ExitCode != ExitCode.Success)
                    return r;

                IInfluxDatabase dbStructure;
                if (settings.GenericFile.Filter != Filters.None)
                {
                    var filterColumns = new List<GenericColumn>();
                    if (settings.GenericFile.Filter == Filters.Columns)
                    {
                        if (settings.GenericFile.ColumnLayout != null && settings.GenericFile.ColumnLayout.Count > 0)
                            Logger.LogLine(LogLevel.Info, "Column Filtering is not applicable when columns are defined in Config file. Use the Skip attribute on each column to filter them");
                        else
                            filterColumns = ParseGenericColumns(settings.GenericFile.ColumnsFilter.ToString());
                    }

                    dbStructure = await client.GetInfluxDBStructureAsync(settings.InfluxDB.DatabaseName);
                    ColumnHeaders = FilterGenericColumns(ColumnHeaders, filterColumns, dbStructure as InfluxDatabase);
                }

                var validity = ValidateData(InputFileName);

                var failureReasons = new Dictionary<Type, FailureTracker>();

                List<IInfluxDatapoint> points = new List<IInfluxDatapoint>(), retryQueue = new List<IInfluxDatapoint>();
                IInfluxRetentionPolicy policy = null;

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

                foreach (var line in File.ReadLines(InputFileName).Skip(settings.GenericFile.HeaderRow + settings.GenericFile.SkipRows))
                {
                    if (String.IsNullOrWhiteSpace(line) || (!String.IsNullOrEmpty(settings.GenericFile.CommentMarker) && line.StartsWith(settings.GenericFile.CommentMarker)))
                        continue;
                    try
                    {
                        result.PointsFound++;
                        var point = ProcessGenericLine(line, ColumnHeaders);
                        if (point == null)
                            result.PointsFailed++;
                        else
                        {
                            point.Retention = policy;
                            points.Add(point);
                        }

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
                                if (++failedReqCount > 3)
                                    break;
                            }
                            //a point will be either posted to Influx or in retry queue
                            points.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        result.PointsFailed++;
                        var type = e.GetType();
                        if (e is InfluxDBException)
                        {
                            if ((e as InfluxDBException).Reason == "Partial Write")
                            {
                                retryQueue.AddRange(points.Where(p => p.Saved != true));
                                result.PointsProcessed += points.Count(p => p.Saved);
                                points.Clear();
                            }
                        }
                        if (!failureReasons.ContainsKey(type))
                            failureReasons.Add(type, new FailureTracker() { ExceptionType = type, Message = e.Message });
                        failureReasons[type].LineNumbers.Add(result.PointsFound + settings.GenericFile.HeaderRow + settings.GenericFile.SkipRows + 1);

                        //avoid too many failures, may be config is wrong
                        if (!settings.GenericFile.IgnoreErrors && result.PointsFailed > settings.InfluxDB.PointsInSingleBatch * 3)
                        {
                            Logger.LogLine(LogLevel.Info, "\n Too many failed points, refer to error info. Aborting!!");
                            Logger.LogLine(LogLevel.Error, "\n Too many failed points, refer to error info. Aborting!!");
                            break;
                        }
                    }

                    if (result.PointsFailed > 0 || retryQueue.Count > 0)
                        Logger.Log(LogLevel.Verbose, "\r{0} Processed {1}, Failed {2}, Queued {3}                        ", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), result.PointsFound, result.PointsFailed, retryQueue.Count);
                    else
                        Logger.Log(LogLevel.Verbose, "\r{0} Processed {1}                          ", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), result.PointsFound);
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

                //retry all previously failed points
                if (retryQueue.Count > 0)
                {
                    Logger.LogLine(LogLevel.Verbose, "\n {0} Retrying {1} failed points", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), retryQueue.Count);
                    try
                    {
                        if (await client.PostPointsAsync(settings.InfluxDB.DatabaseName, retryQueue))
                        {
                            result.PointsProcessed += retryQueue.Count;
                            retryQueue.Clear();
                        }
                        else
                        {
                            result.PointsFailed += retryQueue.Count;
                            if (retryQueue.Count >= settings.InfluxDB.PointsInSingleBatch * 3 || ++failedReqCount > 4)
                                throw new InvalidOperationException("InfluxDB is not able to accept points!! Please check InfluxDB logs for error details!");
                        }
                    }
                    catch (InfluxDBException e)
                    {
                        if (e.Reason == "Partial Write")
                        {
                        }
                    }
                }

                stopwatch.Stop();
                if (result.PointsFailed > 0)
                {
                    Logger.LogLine(LogLevel.Error, "Process Started {0}, Input {1}, Processed{2}, Failed:{3}", (DateTime.Now - stopwatch.Elapsed), InputFileName, result.PointsFound, result.PointsFailed);
                    foreach (var f in failureReasons.Values)
                        Logger.LogLine(LogLevel.Error, "{0} lines (e.g. {1}) failed due to {2} ({3})", f.Count, String.Join(",", f.LineNumbers.Take(5)), f.ExceptionType, f.Message);
                    if (result.PointsFailed == result.PointsFound)
                        result.ExitCode = ExitCode.UnableToProcess;
                    else
                        result.ExitCode = ExitCode.ProcessedWithErrors;
                }
                else
                {
                    result.ExitCode = ExitCode.Success;
                    Logger.LogLine(LogLevel.Info, "\n Done!! Processed:- {0} points", result.PointsFound);
                }
            }
            catch (Exception e)
            {
                Logger.LogLine(LogLevel.Error, "Failed to process {0}", InputFileName);
                Logger.LogLine(LogLevel.Error, "\r\nError!! {0}:{1} - {2}", e.GetType().Name, e.Message, e.StackTrace);
                result.ExitCode = ExitCode.UnableToProcess;
            }
            return result;
        }

        public bool ValidateData(string InputFileName)
        {
            var lineNo = 0;
            if (settings.GenericFile.ValidateRows == 0)
                settings.GenericFile.ValidateRows = 1;

            foreach (var line in File.ReadLines(InputFileName).Skip(settings.GenericFile.HeaderRow + settings.GenericFile.SkipRows))
            {
                if (String.IsNullOrWhiteSpace(line) || (!String.IsNullOrEmpty(settings.GenericFile.CommentMarker) && line.StartsWith(settings.GenericFile.CommentMarker)))
                    continue;

                var columns = pattern.Split(line);
                double value = 0.0; bool boolVal = false;

                var pointData = new Dictionary<GenericColumn, string>();

                foreach (var c in ColumnHeaders)
                {
                    var content = columns[c.ColumnIndex].Replace("\"", "");
                    if (c.HasAutoGenColumns)
                    {
                        pointData.AddRange(c.SplitData(content));
                    }
                    else
                    {
                        pointData.Add(c, content);
                    }
                }

                foreach (var d in pointData)
                {
                    var content = d.Value;

                    try
                    {
                        if (d.Key.HasTransformations && d.Key.CanTransform(content))
                            content = d.Key.Transform(content);
                    }
                    //Filter transformation will throw exceptions, which can be ignored as they are row specific
                    catch (InvalidDataException e)
                    {
                        continue;
                    }

                    if (d.Key.ColumnIndex == settings.GenericFile.TimeColumn - 1)
                    {
                        ParseTimestamp(content);
                    }

                    if (String.IsNullOrWhiteSpace(content))
                        continue;

                    if (d.Key.Type == ColumnDataType.Unknown)
                    {
                        if (Double.TryParse(content, out value))
                            d.Key.Type = d.Key.Config.DataType = ColumnDataType.NumericalField;
                        else if (Boolean.TryParse(content, out boolVal))
                            d.Key.Config.DataType = d.Key.Type = ColumnDataType.BooleanField;
                        else
                            d.Key.Config.DataType = d.Key.Type = ColumnDataType.Tag;
                    }
                    else
                    {
                        if (d.Key.Type == ColumnDataType.NumericalField && (!Double.TryParse(content, out value) || double.IsNaN(value)))
                            throw new InvalidDataException($"{d.Key.ColumnHeader} has inconsistent data, Can't parse {content} as Number");
                        else if (d.Key.Type == ColumnDataType.BooleanField && (!Boolean.TryParse(content, out boolVal)))
                            throw new InvalidDataException($"{d.Key.ColumnHeader} has inconsistent data, Can't parse {content} as Boolean");
                    }
                }
                if (++lineNo == settings.GenericFile.ValidateRows)
                    break;
            }
            return !ColumnHeaders.Any(t => t.Type == ColumnDataType.Unknown);
        }
    }
}