using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AdysTech.Influxer.Config;
using AdysTech.InfluxDB.Client.Net;
using System.Threading.Tasks;
using AdysTech.Influxer;
using System.IO;

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
                var pwd = Directory.GetCurrentDirectory ();
                //ordered tests use a different folder structure
                if (pwd.EndsWith ("Out"))
                {
                    var proj = Path.GetFileNameWithoutExtension (System.Reflection.Assembly.GetExecutingAssembly ().Location);
                    _testFilesPath = Path.Combine (Directory.GetParent (pwd).Parent.Parent.FullName, proj, "TestFiles");

                }
                else
                {
                    _testFilesPath = Directory.GetParent (pwd).Parent.FullName;
                    _testFilesPath = Path.Combine (_testFilesPath, "TestFiles");
                }
                return _testFilesPath;

            }


        }

        [TestMethod]
        public async Task TestGenericHeaderless ()
        {
            var settings = InfluxerConfigSection.Load (Path.Combine (TestFilesPath, "HeaderlessText.conf"));
            settings.InputFileName = Path.Combine (TestFilesPath, "HeaderlessText.txt");
            var client = await GetClientAsync (settings);
            var result = await new GenericFile ().ProcessGenericFile (settings.InputFileName, client);
            Assert.IsTrue (result.ExitCode == ExitCode.Success || result.ExitCode == ExitCode.ProcessedWithErrors, "Processing a generic text file failed");
        }

        [TestMethod]
        public async Task TestPerfmonFile ()
        {
            var settings = InfluxerConfigSection.LoadDefault ();
            settings.FileFormat = FileFormats.Perfmon;
            settings.InputFileName = Path.Combine (TestFilesPath, "Perfmon.csv");
            var client = await GetClientAsync (settings);
            var result = await new PerfmonFile ().ProcessPerfMonLog (settings.InputFileName, client);
            Assert.IsTrue (result.ExitCode == ExitCode.Success || result.ExitCode == ExitCode.ProcessedWithErrors, "Processing Perfmon file failed");
        }

        [TestMethod]
        public async Task TestPerfmonFileMultiMeasurement ()
        {
            var settings = InfluxerConfigSection.LoadDefault ();
            settings.FileFormat = FileFormats.Perfmon;
            settings.InfluxDB.RetentionDuration = 2400;
            settings.InfluxDB.RetentionPolicy = "autogen1";
            settings.PerfmonFile.MultiMeasurements = true;
            settings.InputFileName = Path.Combine (TestFilesPath, "Perfmon.csv");
            var client = await GetClientAsync (settings);
            var result = await new PerfmonFile ().ProcessPerfMonLog (settings.InputFileName, client);
            Assert.IsTrue (result.ExitCode == ExitCode.Success || result.ExitCode == ExitCode.ProcessedWithErrors, "Processing Perfmon file failed");
        }

        [TestMethod]
        public async Task TestGenericSimple ()
        {
            var settings = InfluxerConfigSection.LoadDefault ();
            settings.FileFormat = FileFormats.Generic;
            settings.InputFileName = Path.Combine (TestFilesPath, "Simple.csv");
            settings.InfluxDB.RetentionPolicy = "autogen";
            settings.GenericFile.TimeFormat = "yyyy-MM-dd m:ss";
            var client = await GetClientAsync (settings);
            var result = await new GenericFile ().ProcessGenericFile (settings.InputFileName, client);
            Assert.IsTrue (result.ExitCode == ExitCode.Success || result.ExitCode == ExitCode.ProcessedWithErrors, "Processing a generic CSV file failed");
        }

        [TestMethod]
        public async Task TestGenericSimple2 ()
        {
            var settings = InfluxerConfigSection.LoadDefault ();
            settings.FileFormat = FileFormats.Generic;
            settings.InputFileName = Path.Combine (TestFilesPath, "Simple-2.csv");
            settings.GenericFile.TimeFormat = "MM/dd/yyyy mm:ss";
            settings.InfluxDB.RetentionPolicy = "autogen";
            var client = await GetClientAsync (settings);
            var result = await new GenericFile ().ProcessGenericFile (settings.InputFileName, client);
            Assert.IsTrue (result.ExitCode == ExitCode.Success || result.ExitCode == ExitCode.ProcessedWithErrors, "Processing a generic CSV file failed");
        }


        private static async Task<InfluxDBClient> GetClientAsync (InfluxerConfigSection settings)
        {
            var client = new InfluxDBClient (settings.InfluxDB.InfluxUri, settings.InfluxDB.UserName, settings.InfluxDB.Password);
            var dbNames = await client.GetInfluxDBNamesAsync ();
            if (dbNames.Contains (settings.InfluxDB.DatabaseName))
                return client;
            else
            {
                await client.CreateDatabaseAsync (settings.InfluxDB.DatabaseName);
                return client;
            }
        }
    }
}
