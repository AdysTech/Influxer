using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public interface ITransform
    {
        bool CanTransform(string content);
        string Transform(string content);
    }
}
