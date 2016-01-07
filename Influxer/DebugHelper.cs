using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    class DebugHelper : TextWriter
    {
        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public override void Write(char value)
        {
            Debug.Write (value);
        }

        public override void Write(string value)
        {
            Debug.Write (value);
        }

        public override void WriteLine(string value)
        {
            Debug.WriteLine (value);
        }

        public override void Write(string format, params object[] arg)
        {
            Debug.WriteLine (format, arg);
        }

        public override void Write(string format, object arg0, object arg1)
        { Debug.WriteLine (format, new object[] { arg0, arg1 }); }

        public override void Write(string format, object arg0, object arg1, object arg2)
        {
            Debug.WriteLine (format, new object[] { arg0, arg1, arg2 });
        }


        public override void WriteLine(string format, object arg0)
        {
            Debug.Write (format, arg0.ToString ());
        }

        public override void WriteLine(string format, object arg0, object arg1)
        {
            Debug.WriteLine (format, new object[] { arg0, arg1 });
        }

        public override void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            Debug.WriteLine (format, new object[] { arg0, arg1, arg2 });
        }
    }
}
