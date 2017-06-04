using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdysTech.Influxer.Config
{
    public class OverridableConfigElement : IOverridableConfig
    {
        public OverridableConfigElement()
        {
            foreach (var prop in this.GetType().GetProperties())
            {
                var valAttribute = prop.GetCustomAttributes(typeof(DefaultValueAttribute), true).FirstOrDefault() as DefaultValueAttribute;
                if (!string.IsNullOrEmpty(valAttribute?.Value))
                {
                    try
                    {
                        var val = Converter(valAttribute.Value, prop.PropertyType, valAttribute?.Converter);
                        prop.SetValue(this, val);
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException($"Invalid Default Argument {prop.Name} - {valAttribute.Value}", e);
                    }
                }
            }
        }


        #region IOverridableConfig Members

        public bool ProcessCommandLineArguments(Dictionary<string, string> CommandLine)
        {
            bool found = false, ret = false;
            foreach (var prop in this.GetType().GetProperties())
            {
                if (CommandLine.Count == 0) break;

                var cmdAttribute = prop.GetCustomAttributes(typeof(CommandLineArgAttribute), true).FirstOrDefault() as CommandLineArgAttribute;
                var valAttribute = prop.GetCustomAttributes(typeof(DefaultValueAttribute), true).FirstOrDefault() as DefaultValueAttribute;
                if (cmdAttribute != null)
                {
                    if (CommandLine.ContainsKey(cmdAttribute.Argument.ToLower()))
                    {
                        found = true;
                        try
                        {
                            if (valAttribute != null)
                            {
                                var val = Converter(CommandLine[cmdAttribute.Argument], prop.PropertyType, valAttribute?.Converter);
                                prop.SetValue(this, val);
                            }
                            else
                                prop.SetValue(this, CommandLine[cmdAttribute.Argument]);
                        }
                        catch (Exception e)
                        {
                            throw new ArgumentException($"Invalid Argument {cmdAttribute.Argument}", e);
                        }
                        CommandLine.Remove(cmdAttribute.Argument);
                    }
                }
                if (CommandLine.Count > 0 && prop.GetType().GetInterfaces().Contains(typeof(IOverridableConfig)))
                {
                    ret = (prop as IOverridableConfig).ProcessCommandLineArguments(CommandLine);
                    found = !found ? ret : found;
                }

            }
            return found;
        }



        public string PrintHelpText()
        {
            StringBuilder help = new StringBuilder();
            foreach (var prop in this.GetType().GetProperties())
            {
                var cmdAttribute = prop.GetCustomAttributes(typeof(CommandLineArgAttribute), true).FirstOrDefault() as CommandLineArgAttribute;
                var valAttribute = prop.GetCustomAttributes(typeof(DefaultValueAttribute), true).FirstOrDefault() as DefaultValueAttribute;
                if (cmdAttribute != null)
                {
                    help.AppendFormat("{0,-40}\t\t{1,-100}", cmdAttribute.Usage, cmdAttribute.Description);
                    if (valAttribute != null)
                        help.AppendFormat("\t Default:{0,-40}\n", valAttribute.Value);
                    else
                        help.Append("\n");
                }
                if (prop.GetType().GetInterfaces().Contains(typeof(IOverridableConfig)))
                {
                    help.Append((prop as IOverridableConfig).PrintHelpText());
                }
            }
            return help.ToString();
        }

        #endregion
        private object Converter(string value, Type propertyType, Converters? converter)
        {

            switch (converter)
            {
                case Converters.None: return value;
                case Converters.BooleanParser: return bool.Parse(value);
                case Converters.DoubleParser: return double.Parse(value);
                case Converters.IntParser: return int.Parse(value);
                case Converters.CommaSeperatedListParser: return new List<string>(value.Split(','));
                case Converters.EnumParser: return Enum.Parse(propertyType, value, true);
                case Converters.CharParser: return value.First();
                default: return value;
            }
        }
    }
}
