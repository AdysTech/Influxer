namespace AdysTech.Influxer.Config
{
    public interface ITransform
    {
        string DefaultValue { get; set; }

        bool IsDefault { get; set; }

        bool CanTransform(string content);

        string Transform(string content);
    }
}