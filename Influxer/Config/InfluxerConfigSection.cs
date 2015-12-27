using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//ref: http://www.codeproject.com/Articles/32490/Custom-Configuration-Sections-for-Lazy-Coders
//ref: http://www.codeproject.com/Articles/16466/Unraveling-the-Mysteries-of-NET-Configuration

namespace AdysTech.Influxer.Config
{
    public enum Filters
    {
        None,
        Measurement,
        Field,
        Columns
    }

    public enum FileFormats
    {
        Perfmon,
        Generic
    }

    public class InfluxerConfigSection : ConfigurationSection, IOverridableConfig
    {
        [CommandLineArgAttribute ("-input", Usage = "-input <file name>", Description = "Input file name")]
        [ConfigurationProperty ("InputFileName")]
        public string InputFileName
        {
            get { return (string) this["InputFileName"]; }
            set { this["InputFileName"] = value; }
        }

        [CommandLineArgAttribute ("-format", Usage = "-format <format>", Description = "Input file format. Supported: Perfmon, Generic", DefaultValue = "Perfmon")]
        [ConfigurationProperty ("FileFormat", DefaultValue = FileFormats.Perfmon)]
        public FileFormats FileFormat
        {
            get { return (FileFormats) this["FileFormat"]; }
            set { this["FileFormat"] = value; }
        }

        [ConfigurationProperty ("InfluxDBConfig")]
        public InfluxDBConfig InfluxDB
        {
            get { return this["InfluxDBConfig"] as InfluxDBConfig; }
            set { this["InfluxDBConfig"] = value; }
        }

        [ConfigurationProperty ("PerfmonFileConfig")]
        public PerfmonFileConfig PerfmonFile
        {
            get { return this["PerfmonFileConfig"] as PerfmonFileConfig; }
            set { this["PerfmonFileConfig"] = value; }
        }

        [ConfigurationProperty ("GenericFileConfig")]
        public GenericFileConfig GenericFile
        {
            get { return this["GenericFileConfig"] as GenericFileConfig; }
            set { this["GenericFileConfig"] = value; }
        }

        #region IOverridableConfig Members

        public bool ProcessCommandLineArguments(Dictionary<string, string> CommandLine)
        {
            bool found = false;
            foreach ( var prop in this.GetType ().GetProperties () )
            {
                var cmdAttribute = prop.GetCustomAttributes (typeof (CommandLineArgAttribute), true).FirstOrDefault () as CommandLineArgAttribute;
                if ( cmdAttribute != null )
                {
                    if ( CommandLine.ContainsKey (cmdAttribute.Argument.ToLower ()) )
                    {
                        found = true;
                        try
                        {
                            this[prop.Name] = this.Properties[prop.Name].Converter.ConvertFromString (CommandLine[cmdAttribute.Argument]);
                        }
                        catch ( Exception e )
                        {
                            throw new ArgumentException ("Invalid Argument " + cmdAttribute.Argument, e);
                        }
                        CommandLine.Remove (cmdAttribute.Argument);
                    }
                }
            }

            found = InfluxDB.ProcessCommandLineArguments (CommandLine);
            if ( FileFormat == FileFormats.Perfmon )
                found = PerfmonFile.ProcessCommandLineArguments (CommandLine);
            else
                found = GenericFile.ProcessCommandLineArguments (CommandLine);

            return found;
        }

        public string PrintHelpText()
        {
            StringBuilder help = new StringBuilder ();
            help.AppendLine ("Required flags");
            foreach ( var prop in this.GetType ().GetProperties () )
            {
                var cmdAttribute = prop.GetCustomAttributes (typeof (CommandLineArgAttribute), true).FirstOrDefault () as CommandLineArgAttribute;
                if ( cmdAttribute != null )
                {
                    help.AppendFormat ("{0,-40}\t\t{1,-100}", cmdAttribute.Usage, cmdAttribute.Description);
                    if ( !String.IsNullOrWhiteSpace (cmdAttribute.DefaultValue) )
                        help.AppendFormat ("\t Default:{0,-40}\n", cmdAttribute.DefaultValue);
                    else
                        help.Append ("\n");
                }
            }
            help.AppendLine (new String ('-', 180));
            help.AppendLine ("InfluxDB related flags");
            help.Append (InfluxDB.PrintHelpText ());
            help.AppendLine (new String ('-', 180));
            help.AppendLine ("Perfmon file format related flags");
            help.Append (PerfmonFile.PrintHelpText ());
            help.AppendLine (new String ('-', 180));
            help.AppendLine ("Generic delimited file format related flags");
            help.Append (GenericFile.PrintHelpText ());

            return help.ToString ();
        }

        #endregion

        private bool _loaded = false;
        private static InfluxerConfigSection _instance;

        /// <summary>
        /// Returns default configuration settings for the application
        /// </summary>
        /// <returns>cref:InfluxerConfigSection</returns>
        public static InfluxerConfigSection LoadDefault()
        {
            _instance = new InfluxerConfigSection ();
            return _instance;
        }

        public static InfluxerConfigSection Load(string path)
        {
            if ( _instance == null )
            {
                if ( path.EndsWith (".config",
                        StringComparison.InvariantCultureIgnoreCase) )
                    path = path.Remove (path.Length - 7);
                Configuration config =
                        ConfigurationManager.OpenExeConfiguration (path);
                if ( config.Sections["InfluxerConfiguration"] != null )
                {
                    _instance = (InfluxerConfigSection) config.Sections["InfluxerConfiguration"];
                    _instance._loaded = true;
                }
                else
                {
                    throw new InvalidOperationException ("InfluxerConfiguration section was not found!!, Use Config switch to get a config file with default values");
                }
            }
            return _instance;
        }

        public static bool Export(Stream outStream, bool defaultValues = false)
        {
            var tmpFile = System.IO.Path.GetTempFileName ();
            try
            {
                //ExeConfigurationFileMap configMap = new ExeConfigurationFileMap ();
                //configMap.ExeConfigFilename = tmpFile;
                //Configuration config = ConfigurationManager.OpenMappedExeConfiguration (configMap, ConfigurationUserLevel.None);
                var config = ConfigurationManager.OpenExeConfiguration (ConfigurationUserLevel.None);
                config.Sections.Add ("InfluxerConfiguration", defaultValues ? new InfluxerConfigSection () : _instance);
                config.Sections["InfluxerConfiguration"].SectionInformation.ForceSave = true;
                config.SaveAs (tmpFile, ConfigurationSaveMode.Full);
                config = null;
                using ( var content = File.OpenRead (tmpFile) )
                {
                    content.CopyTo (outStream);
                    outStream.Flush ();
                    content.Close ();
                }
            }
            finally
            {
                File.Delete (tmpFile);
            }
            return true;
        }

    }
}
