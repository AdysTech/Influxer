//Copyright -  Adarsha@AdysTech
using AdysTech.InfluxDB.Client.Net;
using System;
using System.Collections.Generic;

namespace AdysTech.Influxer
{
    public static class DictionaryExtensionMethods
    {
        /// <summary>
        /// Adds elements from one Dictionary to another, overwrites duplicate key values
        /// </summary>
        /// <param name="source">Dictionary which will accept elements</param>
        /// <param name="collection">Dictionary which will provide elements</param>
        public static void AddRange<T, S>(this Dictionary<T, S> source, Dictionary<T, S> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("Collection is null");
            }

            foreach (var item in collection)
            {
                if (!source.ContainsKey(item.Key))
                {
                    source.Add(item.Key, item.Value);
                }
                else
                {
                    source[item.Key] = item.Value;
                }
            }
        }
    }

    public static class StringExtensionMethods
    {
        public static string Replace(this string s, char[] separators, char newValue)
        {
            string[] temp;

            temp = s.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            return String.Join(" ", temp).Trim().Replace(' ', newValue);
        }

        public static IEnumerable<string> SplitFixedWidth(this string str, int width)
        {
            for (int i = 0; i < str.Length; i += width)
                yield return str.Substring(i, Math.Min(width, str.Length - i));
        }
    }

    public static class LongExtensionMethods
    {
        private static readonly DateTime Origin = new DateTime(new DateTime(1970, 1, 1).Ticks, DateTimeKind.Utc);

        public static DateTime FromEpoch(this long epoch, TimePrecision precision)
        {
            switch (precision)
            {
                case TimePrecision.Hours: return Origin.AddHours(epoch);
                case TimePrecision.Minutes: return Origin.AddMinutes(epoch);
                case TimePrecision.Seconds: return Origin.AddSeconds(epoch);
                case TimePrecision.Milliseconds: return Origin.AddMilliseconds(epoch);
                case TimePrecision.Microseconds: return Origin.AddTicks(epoch / 1000 * TimeSpan.TicksPerMillisecond);
                case TimePrecision.Nanoseconds: return Origin.AddTicks(epoch / 100); //1 tick = 100 nano sec
            }
            return Origin;
        }

    }
}