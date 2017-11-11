using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

//ref: http://www.codeproject.com/Articles/32490/Custom-Configuration-Sections-for-Lazy-Coders
//ref: http://www.codeproject.com/Articles/16466/Unraveling-the-Mysteries-of-NET-Configuration

namespace AdysTech.Influxer.Config
{
    public enum FileFormats
    {
        Perfmon,
        Generic
    }

    public enum Filters
    {
        None,
        Measurement,
        Field,
        Columns
    }

    public enum TimeForamtType
    {
        String,
        Epoch,
        Binary
    }

    public class InfluxerConfigSection : OverridableConfigElement
    {
        private static InfluxerConfigSection _instance;

        [CommandLineArg("-format", Usage = "-format <format>", Description = "Input file format. Supported: Perfmon, Generic")]
        [DefaultValue(Value = "Generic", Converter = Converters.EnumParser)]
        [JsonConverter(typeof(StringEnumConverter))]
        public FileFormats FileFormat
        {
            get; set;
        }

        [CommandLineArg("-input", Usage = "-input <file name>", Description = "Input file name")]
        public string InputFileName
        {
            get; set;
        }

        public InfluxDBConfig InfluxDB
        {
            get; set;
        }

        public GenericFileConfig GenericFile
        {
            get; set;
        }

        public PerfmonFileConfig PerfmonFile
        {
            get; set;
        }

        public InfluxerConfigSection() : base()
        {
            PerfmonFile = new PerfmonFileConfig();
            GenericFile = new GenericFileConfig();
            InfluxDB = new InfluxDBConfig();
        }

        /// <summary>
        /// Copies configuration entries into the stream
        /// </summary>
        /// <param name="outStream">Stream which can be written by current actor</param>
        /// <param name="defaultValues">True to generate default settings, false to generate entries matching current values, which might be due to a commandline override</param>
        /// <returns>true: if successful, false otherwise</returns>
        public static bool Export(Stream outStream, bool defaultValues = false)
        {
            var section = defaultValues ? new InfluxerConfigSection() : _instance;
            if (defaultValues)
            {
                if (section.GenericFile.ColumnLayout.Count == 0)
                {
                    section.GenericFile.ColumnLayout.Add(new ColumnConfig() { NameInFile = "SampleColumn123, if this is missing column position is used", InfluxName = "Tag_ServerName", Skip = false, DataType = ColumnDataType.Tag });
                    section.GenericFile.ColumnLayout[0].ReplaceTransformations.Add(new ReplaceTransformation() { FindText = "Text to find", ReplaceWith = "will be replaced" });

                    section.GenericFile.ColumnLayout.Add(new ColumnConfig() { NameInFile = "SampleColumn123, if this is missing column position is used", InfluxName = "Tag_Region", Skip = false, DataType = ColumnDataType.Tag });
                    section.GenericFile.ColumnLayout[1].ExtractTransformations.Add(new ExtractTransformation() { Type = ExtractType.RegEx, RegEx = @"(\d+)x(\d+)" });

                    section.GenericFile.ColumnLayout.Add(new ColumnConfig() { NameInFile = "SampleColumn123, if this is missing column position is used", InfluxName = "Tag_Transaction", Skip = false, DataType = ColumnDataType.Tag });
                    section.GenericFile.ColumnLayout[2].ExtractTransformations.Add(new ExtractTransformation() { Type = ExtractType.SubString, StartIndex = 0, Length = 10 });

                    section.GenericFile.DefaultTags = new List<string>
                    {
                        "Server=ABCD",
                        "Region=North"
                    };
                }
            }
            var content = JsonConvert.SerializeObject(section, Formatting.Indented);

            //do not close the writer to keep the underlying stream open.
            StreamWriter writer = new StreamWriter(outStream);

            writer.Write(content);
            writer.Flush();
            outStream.Position = 0;

            return true;
        }

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
        /// Loads the configuration into application from the file passed.
        /// </summary>
        /// <param name="path">Path to the file which contains valid configuration entries. Without InfluxerConfiguration section will raise an exception</param>
        /// <returns>InfluxerConfigSection created based on entries in config file</returns>
        public static InfluxerConfigSection Load(string path, bool force=false)
        {
            if (force || _instance == null)
            {
                if (!File.Exists(path))
                    throw new InvalidOperationException("Configuration file was not found!!, Please check the path and retry.");
                try
                {
                    _instance = JsonConvert.DeserializeObject<InfluxerConfigSection>(File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Config file couldn't be loaded, Use Config switch to get a config file with default values");
                }
            }
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
        /// Sets the configuration, mainly aids in testing
        /// </summary>
        /// <returns></returns>
        public static void SetCurrentSettings(InfluxerConfigSection settings)
        {
            _instance = settings;
        }

        #region IOverridableConfig Members

        new public string PrintHelpText()
        {
            StringBuilder help = new StringBuilder();
            help.AppendLine("Required flags");
            foreach (var prop in this.GetType().GetProperties())
            {
                if (prop.GetCustomAttributes(typeof(CommandLineArgAttribute), true).FirstOrDefault() is CommandLineArgAttribute cmdAttribute)
                {
                    help.AppendFormat("{0,-40}\t\t{1,-100}", cmdAttribute.Usage, cmdAttribute.Description);
                    //if (!String.IsNullOrWhiteSpace (cmdAttribute.DefaultValue))
                    //    help.AppendFormat ("\t Default:{0,-40}\n", cmdAttribute.DefaultValue);
                    //else
                    //    help.Append ("\n");
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

        new public bool ProcessCommandLineArguments(Dictionary<string, string> CommandLine)
        {
            if (CommandLine == null || CommandLine.Count == 0) return true;

            bool ret1, ret2, ret3;
            ret1 = base.ProcessCommandLineArguments(CommandLine);
            
            ret2 = InfluxDB.ProcessCommandLineArguments(CommandLine);
            
            if (FileFormat == FileFormats.Perfmon)
                ret3 = PerfmonFile.ProcessCommandLineArguments(CommandLine);
            else
                ret3 = GenericFile.ProcessCommandLineArguments(CommandLine);

            return (ret1 || ret2 || ret3);
        }

        #endregion IOverridableConfig Members
    }
}