namespace AdysTech.Influxer.Config
{
    public class InfluxDBConfig : OverridableConfigElement
    {
        [CommandLineArg("-influx", Usage = "-influx <Url>", Description = "Influx DB Url including port")]
        [DefaultValue(Value = "http://localhost:8086")]
        public string InfluxUri
        {
            get; set;
        }

        [CommandLineArg("-dbname", Usage = "-dbName <name>", Description = "Influx database Name, will be created if not present")]
        [DefaultValue(Value = "InfluxerDB")]
        public string DatabaseName
        {
            get; set;
        }

        [CommandLineArg("-uname", Usage = "-uname <username>", Description = "User name for InfluxDB")]
        public string UserName
        {
            get; set;
        }

        [CommandLineArg("-pass", Usage = "-pass <password>", Description = "Password for InfluxDB")]
        public string Password
        {
            get; set;
        }

        [CommandLineArg("-batch", Usage = "-batch <number of points>", Description = "No of points to send to InfluxDB in one request")]
        [DefaultValue(Value = "128", Converter = Converters.IntParser)]
        public int PointsInSingleBatch
        {
            get; set;
        }

        public InfluxIdentifiers InfluxReserved
        {
            get; set;
        }

        [CommandLineArg("-retentionminutes", Usage = "-retentionDuration <number of minutes>", Description = "No of minutes that the data will be retained by InfluxDB, if noneof the plcies match, a new one will be created")]
        public int RetentionDuration
        {
            get; set;
        }

        [CommandLineArg("-retention", Usage = "-retention <policy name>", Description = "Name of the InfluxDB retention policy where the taget measurements will be created. RetentionDuration takes precedence over this")]
        public string RetentionPolicy
        {
            get; set;
        }

        [CommandLineArgAttribute("-table", Usage = "-table <table name>", Description = "Measurement name in InfluxDB")]
        [DefaultValue(Value = "InfluxerData")]
        public string Measurement
        {
            get; set;
        }

        public InfluxDBConfig() : base()
        {
            InfluxReserved = new InfluxIdentifiers();
        }
    }

    public class InfluxIdentifiers : OverridableConfigElement
    {
        [DefaultValue(Value = "\" ;_()%#./*[]{},")]
        public string ReservedCharecters
        {
            get; set;
        }

        [DefaultValue(Value = "_", Converter = Converters.CharParser)]
        public char ReplaceReservedWith
        {
            get; set;
        }
    }
}
