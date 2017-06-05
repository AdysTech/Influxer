namespace AdysTech.Influxer
{
    public class ProcessStatus
    {
        public ExitCode ExitCode { get; set; }
        public int PointsFailed { get; set; }
        public int PointsFound { get; set; }
        public int PointsProcessed { get; set; }
    }
}