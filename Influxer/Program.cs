//Copyright -  Adarsha@AdysTech
//https://github.com/AdysTech/Influxer/blob/master/Influxer/Program.cs
using AdysTech.InfluxDB.Client.Net;
using AdysTech.Influxer.Config;
using AdysTech.Influxer.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    internal class Program
    {
        private static InfluxerConfigSection settings;

        private static int Main(string[] args)
        {
            try
            {
                if (!CommandLineProcessor.ProcessArguments(args))
                    return (int)ExitCode.Success;
                settings = CommandLineProcessor.Settings;
            }
            catch (ArgumentException e)
            {
                Logger.LogLine(LogLevel.Error, e.Message);
                return (int)ExitCode.InvalidArgument;
            }
            catch (FileLoadException e)
            {
                Logger.LogLine(LogLevel.Error, e.Message);
                Logger.LogLine(LogLevel.Info, "Problem loading config file, regenerate it with /config option");
                return (int)ExitCode.InvalidFilename;
            }
            catch (Exception e)
            {
                Logger.LogLine(LogLevel.Error, "Error processing arguments {0}: {1}", e.GetType().Name, e.Message);
                return (int)ExitCode.InvalidArgument;
            }

            #region Validate inputs

            if (String.IsNullOrWhiteSpace(settings.InputFileName))
            {
                Logger.LogLine(LogLevel.Error, "Input File Name is not specified!! Can't continue");
                return (int)ExitCode.InvalidArgument;
            }

            try
            {
                settings.InputFileName = Path.GetFullPath(settings.InputFileName);
            }
            catch (Exception e)
            {
                Logger.LogLine(LogLevel.Error, "Error with input file:{0},{1}", e.GetType().Name, e.Message);
                Logger.LogLine(LogLevel.Info, "Problem with inputfile name, check path");
                return (int)ExitCode.InvalidFilename;
            }

            if (String.IsNullOrWhiteSpace(settings.InfluxDB.InfluxUri))
            {
                Logger.LogLine(LogLevel.Error, "Influx DB Uri is not configured!!");
                return (int)ExitCode.InvalidArgument;
            }

            if (String.IsNullOrWhiteSpace(settings.InfluxDB.DatabaseName))
            {
                Logger.LogLine(LogLevel.Error, "Influx DB name is not configured!!");
                return (int)ExitCode.InvalidArgument;
            }

            #endregion Validate inputs

            ProcessStatus result = new ProcessStatus() { ExitCode = ExitCode.UnknownError };
            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                var client = new InfluxDBClient(settings.InfluxDB.InfluxUri, settings.InfluxDB.UserName, settings.InfluxDB.Password);

                if (!VerifyDatabaseAsync(client, settings.InfluxDB.DatabaseName).Result)
                {
                    Logger.LogLine(LogLevel.Info, "Unable to create DB {0}", settings.InfluxDB.DatabaseName);
                    return (int)ExitCode.UnableToProcess;
                }
                switch (settings.FileFormat)
                {
                    case FileFormats.Perfmon:
                        result = new PerfmonFile().ProcessPerfMonLog(settings.InputFileName, client).Result;
                        break;

                    case FileFormats.Generic:
                        if (String.IsNullOrWhiteSpace(settings.InfluxDB.Measurement))
                            throw new ArgumentException("Generic format needs TableName input");
                        result = new GenericFile().ProcessGenericFile(settings.InputFileName, client).Result;
                        break;
                }

                stopwatch.Stop();
                Logger.LogLine(LogLevel.Info, "\n Finished!! Processed {0} points (Success: {1}, Failed:{2}) in {3}", result.PointsFound, result.PointsProcessed, result.PointsFailed, stopwatch.Elapsed.ToString());
            }
            catch (AggregateException e)
            {
                Logger.LogLine(LogLevel.Error, "Error!! {0}:{1} - {2}", e.InnerException.GetType().Name, e.InnerException.Message, e.InnerException.StackTrace);
            }
            catch (Exception e)
            {
                Logger.LogLine(LogLevel.Error, "Error!! {0}:{1} - {2}", e.GetType().Name, e.Message, e.StackTrace);
            }
            return (int)result.ExitCode;
        }

        private static async Task<bool> VerifyDatabaseAsync(InfluxDBClient client, string DBName)
        {
            try
            {
                //verify DB exists, create if not
                var dbNames = await client.GetInfluxDBNamesAsync();
                if (dbNames.Contains(DBName))
                    return true;
                else
                {
                    var filter = settings.FileFormat == FileFormats.Perfmon ? settings.PerfmonFile.Filter : settings.GenericFile.Filter;
                    if (filter == Filters.Measurement || filter == Filters.Field)
                    {
                        Logger.LogLine(LogLevel.Info, "Measurement/Field filtering is not applicable for new database!!");
                        filter = Filters.None;
                    }
                    return await client.CreateDatabaseAsync(DBName);
                }
            }
            catch (Exception e)
            {
                Logger.LogLine(LogLevel.Info, "Unexpected exception of type {0} caught: {1}",
                            e.GetType(), e.Message);
            }
            return false;
        }
    }

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
}