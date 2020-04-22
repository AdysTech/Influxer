//Copyright -  Adarsha@AdysTech

namespace AdysTech.Influxer
{
    internal class PerfmonCounter
    {
        public int ColumnIndex { get; set; }
        public string CounterName { get; set; }
        public string Host { get; set; }
        public string PerformanceObject { get; set; }
        public string CounterInstance { get; set; }
    }
}