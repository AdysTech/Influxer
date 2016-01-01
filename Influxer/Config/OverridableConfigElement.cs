using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public class OverridableConfigElement : ConfigurationElement, IOverridableConfig
    {

        #region IOverridableConfig Members

        public bool ProcessCommandLineArguments(Dictionary<string, string> CommandLine)
        {
            bool found = false, ret = false;
            foreach ( var prop in this.GetType ().GetProperties () )
            {
                if ( CommandLine.Count == 0 ) break;

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
                if ( CommandLine.Count > 0 && prop.GetType ().GetInterfaces ().Contains (typeof (IOverridableConfig)) )
                {
                    ret = ( prop as IOverridableConfig ).ProcessCommandLineArguments (CommandLine);
                    found = !found ? ret : found;
                }

            }
            return found;
        }

        public string PrintHelpText()
        {
            StringBuilder help = new StringBuilder ();
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
                if ( prop.GetType ().GetInterfaces ().Contains (typeof (IOverridableConfig)) )
                {
                    help.Append (( prop as IOverridableConfig ).PrintHelpText ());
                }
            }
            return help.ToString ();
        }

        #endregion
    }
}
