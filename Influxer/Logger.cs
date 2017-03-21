using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Logging
{
    public enum LogLevel
    {
        Verbose = 0,
        Info,
        Error
    }

    public static class Logger
    {
        static LogLevel? _minLevel;

        public static LogLevel MinLevel
        {
            get
            {
                //#if DEBUG
                //                return LogLevel.Info;
                //#endif
                if (_minLevel == null)
                {
                    if (Console.IsOutputRedirected)
                        _minLevel = LogLevel.Info;
                    else
                        _minLevel = LogLevel.Verbose;
                }
                return _minLevel.Value;
            }

        }

        public static void LogLine(LogLevel level, string format, params object[] arg)
        {
            if (level < MinLevel) return;
            if (Debugger.IsAttached)
            {
                if (level == LogLevel.Error)
                    Debug.WriteLine(format, arg);
                else
                    Debug.WriteLine(format, arg);
            }
            else
            {
                if (level == LogLevel.Error)
                    Console.Error.WriteLine(format, arg);
                else
                    Console.WriteLine(format, arg);
            }
        }

        public static void Log(LogLevel level, string format, params object[] arg)
        {
            if (level < MinLevel) return;
            if (Debugger.IsAttached)
            {
                if (level == LogLevel.Error)
                    if (arg?.Length > 0) Debug.WriteLine(format, arg); else Debug.WriteLine(format);
                else
                    if (arg?.Length > 0) Debug.WriteLine(format, arg); else Debug.WriteLine(format);
            }
            else
            {
                if (level == LogLevel.Error)
                    if (arg?.Length > 0) Console.Error.Write(format, arg); else Console.Error.Write(format);
                else
                    if (arg?.Length > 0) Console.Write(format, arg); else Console.Write(format);
                
            }
        }
    }
}
