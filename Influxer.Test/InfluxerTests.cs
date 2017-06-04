using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AdysTech.Influxer.Config;
using AdysTech.InfluxDB.Client.Net;
using System.Threading.Tasks;
using AdysTech.Influxer;
using System.IO;
using System.Diagnostics;
using AdysTech.Influxer.Logging;

namespace Influxer.Test
{
    [TestClass]
    public class InfluxerTests
    {
        string _testFilesPath;

        public string TestFilesPath
        {
            get
            {
                if (_testFilesPath != null)
                    return _testFilesPath;
                var pwd = Directory.GetCurrentDirectory();
                //ordered tests use a different folder structure
                if (pwd.EndsWith("Out"))
                {
                    var proj = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    _testFilesPath = Path.Combine(Directory.GetParent(pwd).Parent.Parent.FullName, proj, "TestFiles");

                }
                else
                {
                    _testFilesPath = Directory.GetParent(pwd).Parent.FullName;
                    _testFilesPath = Path.Combine(_testFilesPath, "TestFiles");
                }
                return _testFilesPath;

            }


        }

        private static async Task<InfluxDBClient> GetClientAsync(InfluxerConfigSection settings)
        {
            var client = new InfluxDBClient(settings.InfluxDB.InfluxUri, settings.InfluxDB.UserName, settings.InfluxDB.Password);
            var dbNames = await client.GetInfluxDBNamesAsync();
            if (dbNames.Contains(settings.InfluxDB.DatabaseName))
                return client;
            else
            {
                await client.CreateDatabaseAsync(settings.InfluxDB.DatabaseName);
                return client;
            }
        }

        [TestMethod]
        public async Task TestGenericHeaderless()
        {
            var settings = InfluxerConfigSection.Load(Path.Combine(TestFilesPath, "HeaderlessText.conf"));
            settings.InputFileName = Path.Combine(TestFilesPath, "HeaderlessText.txt");
            var client = await GetClientAsync(settings);
            var file = new GenericFile();
            var result = await file.ProcessGenericFile(settings.InputFileName, client);
            //Debug.WriteLine (result.ToString ());
            Assert.IsTrue(result.ExitCode == ExitCode.ProcessedWithErrors && result.PointsFound == 1001 && result.PointsFailed == 29, "Processing a generic text file failed");
        }

        [TestMethod]
        public async Task TestPerfmonFile()
        {
            var settings = InfluxerConfigSection.LoadDefault();
            settings.FileFormat = FileFormats.Perfmon;
            settings.InputFileName = Path.Combine(TestFilesPath, "Perfmon.csv");
            var client = await GetClientAsync(settings);
            var result = await new PerfmonFile().ProcessPerfMonLog(settings.InputFileName, client);
            //Debug.WriteLine (result.ToString ());
            Assert.IsTrue(result.ExitCode == ExitCode.ProcessedWithErrors && result.PointsFound == 144818 && result.PointsFailed == 0, "Processing Perfmon file failed");
        }

        [TestMethod]
        public async Task TestPerfmonFileMultiMeasurement()
        {
            var settings = InfluxerConfigSection.LoadDefault();
            settings.FileFormat = FileFormats.Perfmon;
            settings.InfluxDB.RetentionDuration = (int)TimeSpan.FromDays(365).TotalMinutes;
            settings.InfluxDB.RetentionPolicy = "autogen1";
            settings.PerfmonFile.MultiMeasurements = true;
            settings.InputFileName = Path.Combine(TestFilesPath, "Perfmon.csv");
            var client = await GetClientAsync(settings);
            var result = await new PerfmonFile().ProcessPerfMonLog(settings.InputFileName, client);
            //Debug.WriteLine (result.ToString ());
            Assert.IsTrue(result.ExitCode == ExitCode.ProcessedWithErrors && result.PointsFound == 5347 && result.PointsFailed == 0, "Processing Perfmon file failed");
        }

        [TestMethod]
        public async Task TestGenericSimple()
        {
            var settings = InfluxerConfigSection.LoadDefault();
            settings.FileFormat = FileFormats.Generic;
            settings.InputFileName = Path.Combine(TestFilesPath, "Simple.csv");
            settings.InfluxDB.RetentionPolicy = "autogen";
            settings.GenericFile.TimeFormat = "yyyy-MM-dd m:ss";
            var client = await GetClientAsync(settings);
            var file = new GenericFile();
            var result = await file.ProcessGenericFile(settings.InputFileName, client);
            //Debug.WriteLine (result.ToString ());
            Assert.IsTrue(result.ExitCode == ExitCode.ProcessedWithErrors && result.PointsFound == 4897 && result.PointsFailed == 1, "Processing a generic CSV file failed");
        }

        [TestMethod]
        public async Task TestGenericSimple2()
        {
            var settings = InfluxerConfigSection.LoadDefault();
            settings.FileFormat = FileFormats.Generic;
            settings.InputFileName = Path.Combine(TestFilesPath, "Simple-2.csv");
            settings.GenericFile.TimeFormat = "MM/dd/yyyy mm:ss";
            settings.InfluxDB.RetentionPolicy = "autogen";
            var client = await GetClientAsync(settings);
            var result = await new GenericFile().ProcessGenericFile(settings.InputFileName, client);
            //Debug.WriteLine (result.ToString ());
            Assert.IsTrue(result.ExitCode == ExitCode.Success && result.PointsFound == 226 && result.PointsFailed == 0, "Processing a generic CSV file failed");
        }

        [TestMethod]
        public async Task TestGenericMicroSecPrecision()
        {
            var args = new string[] { "-input", Path.Combine (TestFilesPath, "MicroSecSemicolonSeperated.txt"),
                "-format", "Generic",
                "-TimeFormat", "yyyy-MM-dd-hh.mm.ss.ffffff",
                "-Precision", "Microseconds",
                "-splitter", ";" };
            InfluxerConfigSection settings;
            CommandLineProcessor.ProcessArguments(args);
            settings = CommandLineProcessor.Settings;
            var client = await GetClientAsync(settings);
            var result = await new GenericFile().ProcessGenericFile(settings.InputFileName, client);
            //Debug.WriteLine (result.ToString ());
            Assert.IsTrue(result.ExitCode == ExitCode.Success && result.PointsFound == 4 && result.PointsFailed == 0, "Processing a generic MicroSecSemicolonSeperated file failed");
        }

        [TestMethod]
        public void TestHelpCommand()
        {
            var args = new string[] { "--help" };
            Assert.IsFalse(CommandLineProcessor.ProcessArguments(args), "Processing help command failed");
        }

        [TestMethod]
        public void TestAutoLayoutCommand()
        {
            var args = new string[] { "-input", Path.Combine (TestFilesPath, "MicroSecSemicolonSeperated.txt"),
                "-format", "Generic",
                "-TimeFormat", "yyyy-MM-dd-hh.mm.ss.ffffff",
                "-Precision", "Microseconds",
                "-splitter", ";",
                "-tags", "Server=abcd",
                "-ignoreerrors",
                "/export",
                "/autolayout"
            };
            Assert.IsFalse(CommandLineProcessor.ProcessArguments(args), "Processing AutoLayout Command failed");
        }
    }
}
