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
        [CommandLineArgAttribute("-input", Usage = "-input <file name>", Description = "Input file name")]
        [ConfigurationProperty("InputFileName")]
        public string InputFileName
        {
            get { return (string)this["InputFileName"]; }
            set { this["InputFileName"] = value; }
        }

        [CommandLineArgAttribute("-format", Usage = "-format <format>", Description = "Input file format. Supported: Perfmon, Generic", DefaultValue = "Perfmon")]
        [ConfigurationProperty("FileFormat", DefaultValue = FileFormats.Perfmon)]
        public FileFormats FileFormat
        {
            get { return (FileFormats)this["FileFormat"]; }
            set { this["FileFormat"] = value; }
        }

        [ConfigurationProperty("InfluxDBConfig")]
        public InfluxDBConfig InfluxDB
        {
            get { return this["InfluxDBConfig"] as InfluxDBConfig; }
            set { this["InfluxDBConfig"] = value; }
        }

        [ConfigurationProperty("PerfmonFileConfig")]
        public PerfmonFileConfig PerfmonFile
        {
            get { return this["PerfmonFileConfig"] as PerfmonFileConfig; }
            set { this["PerfmonFileConfig"] = value; }
        }

        [ConfigurationProperty("GenericFileConfig")]
        public GenericFileConfig GenericFile
        {
            get { return this["GenericFileConfig"] as GenericFileConfig; }
            set { this["GenericFileConfig"] = value; }
        }

        #region IOverridableConfig Members

        public bool ProcessCommandLineArguments(Dictionary<string, string> CommandLine)
        {
            if (CommandLine == null || CommandLine.Count == 0) return true;

            bool found = false;
            foreach (var prop in this.GetType().GetProperties())
            {
                if (CommandLine.Count == 0)
                    break;

                var cmdAttribute = prop.GetCustomAttributes(typeof(CommandLineArgAttribute), true).FirstOrDefault() as CommandLineArgAttribute;
                if (cmdAttribute != null)
                {
                    if (CommandLine.ContainsKey(cmdAttribute.Argument))
                    {
                        found = true;
                        try
                        {
                            this[prop.Name] = this.Properties[prop.Name].Converter.ConvertFromString(CommandLine[cmdAttribute.Argument]);
                        }
                        catch (Exception e)
                        {
                            throw new ArgumentException("Invalid Argument " + cmdAttribute.Argument, e);
                        }
                        CommandLine.Remove(cmdAttribute.Argument);
                    }
                }
            }

            if (CommandLine.Count == 0)
                return found;
            bool ret;
            ret = InfluxDB.ProcessCommandLineArguments(CommandLine);
            if (CommandLine.Count == 0)
                return !found ? ret : found;

            found = !found ? ret : found;

            if (FileFormat == FileFormats.Perfmon)
                ret = PerfmonFile.ProcessCommandLineArguments(CommandLine);
            else
                ret = GenericFile.ProcessCommandLineArguments(CommandLine);

            return !found ? ret : found;
        }

        public string PrintHelpText()
        {
            StringBuilder help = new StringBuilder();
            help.AppendLine("Required flags");
            foreach (var prop in this.GetType().GetProperties())
            {
                var cmdAttribute = prop.GetCustomAttributes(typeof(CommandLineArgAttribute), true).FirstOrDefault() as CommandLineArgAttribute;
                if (cmdAttribute != null)
                {
                    help.AppendFormat("{0,-40}\t\t{1,-100}", cmdAttribute.Usage, cmdAttribute.Description);
                    if (!String.IsNullOrWhiteSpace(cmdAttribute.DefaultValue))
                        help.AppendFormat("\t Default:{0,-40}\n", cmdAttribute.DefaultValue);
                    else
                        help.Append("\n");
                }
            }
            help.AppendLine(new String('-', 180));
            help.AppendLine("InfluxDB related flags");
            help.Append(InfluxDB.PrintHelpText());
            help.AppendLine(new String('-', 180));
            help.AppendLine("Perfmon file format related flags");
            help.Append(PerfmonFile.PrintHelpText());
            help.AppendLine(new String('-', 180));
            help.AppendLine("Generic delimited file format related flags");
            help.Append(GenericFile.PrintHelpText());

            return help.ToString();
        }

        #endregion

        private static InfluxerConfigSection _instance;

        /// <summary>
        /// Returns currently loaded configuration or default one if nothing is loaded
        /// </summary>
        /// <returns></returns>
        public static InfluxerConfigSection GetCurrentOrDefault()
        {
            if (_instance == null)
                return LoadDefault();
            else
                return _instance;
        }

        /// <summary>
        /// Returns default configuration settings for the application
        /// </summary>
        /// <returns>cref:InfluxerConfigSection</returns>
        public static InfluxerConfigSection LoadDefault()
        {
            _instance = new InfluxerConfigSection();
            return _instance;
        }


        /// <summary>
        /// Loads the configuration into application from the file passed.
        /// </summary>
        /// <param name="path">Path to the file which contains valid configuration entries. Without InfluxerConfiguration section will raise an exception</param>
        /// <returns>InfluxerConfigSection created based on entries in config file</returns>
        public static InfluxerConfigSection Load(string path)
        {
            if (_instance == null)
            {
                //if ( path.EndsWith (".config",
                //        StringComparison.InvariantCultureIgnoreCase) )
                //    path = path.Remove (path.Length - 7);
                //Configuration config =
                //        ConfigurationManager.OpenExeConfiguration (path);

                ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
                configMap.ExeConfigFilename = path;
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);

                if (config.Sections["InfluxerConfiguration"] != null)
                {
                    _instance = (InfluxerConfigSection)config.Sections["InfluxerConfiguration"];
                }
                else
                {
                    throw new InvalidOperationException("InfluxerConfiguration section was not found!!, Use Config switch to get a config file with default values");
                }
            }
            return _instance;
        }

        /// <summary>
        /// Copies configuration entries into the stream
        /// </summary>
        /// <param name="outStream">Stream which can be written by current actor</param>
        /// <param name="defaultValues">True to generate default settings, false to generate entries matching current values, which might be due to a commandline override</param>
        /// <returns>true: if successful, false otherwise</returns>
        public static bool Export(Stream outStream, bool defaultValues = false)
        {
            var tmpFile = System.IO.Path.GetTempFileName();
            try
            {
                var section = defaultValues ? new InfluxerConfigSection() : _instance;
                Configuration config;
                if (section.CurrentConfiguration == null)
                {
                    config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    config.Sections.Add("InfluxerConfiguration", section);
                }
                else
                {
                    config = section.CurrentConfiguration;
                }

                #region Add any sample values, but not added by default
                if (defaultValues)
                {
                    if (section.GenericFile.ColumnLayout.Count == 0)
                    {
                        section.GenericFile.ColumnLayout[0] = new ColumnConfig() { NameInFile = "SampleColumn123, if this is missing column position is used", InfluxName = "Tag_ServerName", Skip = false, DataType = ColumnDataType.Tag };
                        section.GenericFile.ColumnLayout[0].ReplaceTransformations[0] = new ReplaceTransformation() { FindText = "Text to find", ReplaceWith = "will be replaced" };

                        section.GenericFile.ColumnLayout[1] = new ColumnConfig() { NameInFile = "SampleColumn123, if this is missing column position is used", InfluxName = "Tag_Region", Skip = false, DataType = ColumnDataType.Tag };
                        section.GenericFile.ColumnLayout[1].ExtractTransformations[0] = new ExtractTransformation() { Type = ExtractType.RegEx, RegEx= @"(\d+)x(\d+)"};


                        section.GenericFile.ColumnLayout[2] = new ColumnConfig() { NameInFile = "SampleColumn123, if this is missing column position is used", InfluxName = "Tag_Transaction", Skip = false, DataType = ColumnDataType.Tag };
                        section.GenericFile.ColumnLayout[2].ExtractTransformations[0] = new ExtractTransformation() { Type = ExtractType.SubString, StartIndex = 0, Length = 10 };

                    }
                }
                #endregion

                config.Sections["InfluxerConfiguration"].SectionInformation.ForceSave = true;
                config.SaveAs(tmpFile, ConfigurationSaveMode.Full);
                config = null;
                using (var content = File.OpenRead(tmpFile))
                {
                    content.CopyTo(outStream);
                    outStream.Flush();
                    content.Close();
                }
            }
            finally
            {
                File.Delete(tmpFile);
            }
            return true;
        }

    }
}
