//Copyright -  Adarsha@AdysTech
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    internal static class EpochHelper
    {
        private static readonly DateTime Origin = new DateTime (1970, 1, 1);

        public static long ToEpochMs(this DateTime time)
        {
            TimeSpan t = time - Origin;
            return (long) ( t.TotalSeconds * 1000 );
        }

        public static long ToEpoch(this DateTime time)
        {
            TimeSpan t = time - Origin;
            return (long) ( t.TotalSeconds );
        }
    }
    
    public static class StringExtensionMethods
    {
       public static string Replace(this string s, char[] separators, string newValue)
       {
           string[] temp;
    
           temp = s.Split(separators, StringSplitOptions.RemoveEmptyEntries);
           return String.Join( " ", temp ).Time().Replace(" ",newValue);
       }
    }
}
