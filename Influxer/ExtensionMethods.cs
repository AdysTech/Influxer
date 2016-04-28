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
        public static string Replace (this string s, char[] separators, char newValue)
        {
            string[] temp;

            temp = s.Split (separators, StringSplitOptions.RemoveEmptyEntries);
            return String.Join (" ", temp).Trim ().Replace (' ', newValue);
        }

        public static IEnumerable<string> SplitFixedWidth (this string str, int width)
        {
            for (int i = 0; i < str.Length; i += width)
                yield return str.Substring (i, Math.Min (width, str.Length - i));
        }
    }

    public static class DictionaryExtensionMethods
    {
        /// <summary>
        /// Adds elements from one Dictionary to another, overwrites duplicate key values
        /// </summary>
        /// <param name="source">Dictionary which will accept elements</param>
        /// <param name="collection">Dictionary which will provide elements</param>
        public static void AddRange<T, S> (this Dictionary<T, S> source, Dictionary<T, S> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException ("Collection is null");
            }
            
            foreach (var item in collection)
            {
                if (!source.ContainsKey (item.Key))
                {
                    source.Add (item.Key, item.Value);
                }
                else
                {
                    source[item.Key] = item.Value;
                }
            }
        }
    }
}
