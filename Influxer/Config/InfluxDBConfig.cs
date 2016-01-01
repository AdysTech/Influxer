using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public class InfluxDBConfig : OverridableConfigElement
    {
        [CommandLineArgAttribute ("-influx", Usage = "-influx <Url>", Description = "Influx DB Url including port", DefaultValue = "localhost:8083")]
        [ConfigurationProperty ("InfluxUri", DefaultValue = "http://localhost:8086")]
        public string InfluxUri
        {
            get { return (string) this["InfluxUri"]; }
            set { this["InfluxUri"] = value; }
        }

        [CommandLineArgAttribute ("-dbname", Usage = "-dbName <name>", Description = "Influx database Name, will be created if not present", DefaultValue = "InfluxerDB")]
        [ConfigurationProperty ("DatabaseName", DefaultValue = "InfluxerDB")]
        public string DatabaseName
        {
            get { return (string) this["DatabaseName"]; }
            set { this["DatabaseName"] = value; }
        }

        [CommandLineArgAttribute ("-uname", Usage = "-uname <username>", Description = "User name for InfluxDB")]
        [ConfigurationProperty ("UserName")]
        public string UserName
        {
            get { return (string) this["UserName"]; }
            set { this["UserName"] = value; }
        }

        [CommandLineArgAttribute ("-pass", Usage = "-pass <password>", Description = "Password for InfluxDB")]
        [ConfigurationProperty ("Password")]
        public string Password
        {
            get { return (string) this["Password"]; }
            set { this["Password"] = value; }
        }

        [CommandLineArgAttribute ("-batch", Usage = "-batch <number of points>", Description = "No of points to send to InfluxDB in one request", DefaultValue = "128")]
        [ConfigurationProperty ("PointsInSingleBatch", DefaultValue = 128)]
        public int PointsInSingleBatch
        {
            get { return (int) this["PointsInSingleBatch"]; }
            set { this["PointsInSingleBatch"] = value; }
        }

        [ConfigurationProperty ("InfluxReserved")]
        public InfluxIdentifiers InfluxReserved
        {
            get { return (InfluxIdentifiers) this["InfluxReserved"]; }
            set { this["InfluxReserved"] = value; }
        }
    }

    public class InfluxIdentifiers : OverridableConfigElement
    {
        [ConfigurationProperty ("ReservedCharecters", DefaultValue = "\" ;_()%#./*[]{},")]
        public string ReservedCharecters
        {
            get { return (string) this["ReservedCharecters"]; }
            set { this["ReservedCharecters"] = value; }
        }

        [ConfigurationProperty ("ReplaceReservedWith", DefaultValue = '_')]
        public char ReplaceReservedWith
        {
            get { return (char) this["ReplaceReservedWith"]; }
            set { this["ReplaceReservedWith"] = value; }
        }
    }
}
