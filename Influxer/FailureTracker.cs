using System;
using System.Collections.Generic;

namespace AdysTech.Influxer
{
    internal class FailureTracker
    {
        public int Count { get { return LineNumbers.Count; } }
        public Type ExceptionType { get; set; }
        public List<int> LineNumbers { get; private set; }
        public string Message { get; set; }

        public FailureTracker()
        {
            LineNumbers = new List<int>();
        }
    }
}