//Copyright -  Adarsha@AdysTech
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
  
    public static class StringExtensionMethods
    {
       public static string Replace(this string s, char[] separators, string newValue)
       {
           string[] temp;
    
           temp = s.Split(separators, StringSplitOptions.RemoveEmptyEntries);
           return String.Join( " ", temp ).Trim().Replace(" ",newValue);
       }
    }
}
