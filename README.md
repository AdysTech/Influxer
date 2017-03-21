# Influxer
A C# console application to parse log files (currently only Windows Perfmon format) and push data to Influx for later visualization. It uses [InfluxDB.Client.Net](https://github.com/AdysTech/InfluxDB.Client.Net) to interact with Influx.

[InfluxDB][1] is a very nice time series database, and is supported by many data visualizers (mainly [grafana][2]). But if you have other tools which are producing the data in csv format (mainly PerfMon in windows, or enterprise reporting tools) which are not designed for Influx era, you will have to develop own tools to pull from one tool and to push to other.

Meet Influxer, a small C# console application, which will take any text files with time series and upload it to any Influx instance.
It has special handling for Microsoft Windows Perfmon file format. It can be configured (via command lines or by configuration files) to handle any type of text files.

##### Steps to generate configuration file
1. Find out the time format for the input file
2. Run the Influxer with -timeformat <time format> -input <file path> /export /autolayout
3. Once you see the configuration output on screen, rerun with output redirect (> somefile) to save the configuration
     
#### Build Status
[![Build status](https://ci.appveyor.com/api/projects/status/j9c7xfqj6wv9fg76?svg=true)](https://ci.appveyor.com/project/AdysTech/influxer)

#### [Download Latest](https://ci.appveyor.com/api/projects/AdysTech/Influxer/artifacts/Influxer/bin/InfluxerLatest.zip?branch=master)
	 Supported command line arguments
     --help /? or /help  shows this help text
     
     
     /export to print possible config section, pipe it to a file to edit and reuse the config
     
     -config <configuration file path> to load the config file.
     
     Any configuration entries can be overridden by command line switches shown below
     
     ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
     Required flags
     -input <file name>                                      Input file name
     -format <format>                                        Input file format. Supported: Perfmon, Generic                                                           Default:Perfmon
     ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
     InfluxDB related flags
     -influx <Url>                                           Influx DB Url including port                                                                             Default:localhost:8083
     -dbName <name>                                          Influx database Name, will be created if not present                                                     Default:InfluxerDB
     -uname <username>                                       User name for InfluxDB
     -pass <password>                                        Password for InfluxDB
     -batch <number of points>                               No of points to send to InfluxDB in one request                                                          Default:128
     ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
     Perfmon file format related flags
     -seperator <char>                                       Charecter seperating Columns                                                                             Default:,
     -splitter <regex>                                       RegEx used for splitting rows into columns                                                               Default:,(?=(?:[^"]*"[^"]*")*[^"]*$)
     -TimeFormat <format>                                    Time format used in input files                                                                          Default:MM/dd/yyyy HH:mm:ss.fff
     -Precision <precision>                                  Supported:Hours<1>,Minutes<2>,Seconds<3>,MilliSeconds<4>,MicroSeconds<5>,NanoSeconds<6>                  Default:Seconds
     -filter <filter>                                        Filter input data file, Supported:Measurement (import preexisting measurements), Field (import preexisting fields), Columns (import specified columns)
     ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
     Generic delimited file format related flags
     -table <table name>                                     Measurement name in InfluxDB                                                                             Default:InfluxerData
     -utcoffset <No of Minutes>                              Offset in minutes to UTC, each line in input will be adjusted to arrive time in UTC
     -validate <No of Rows>                                  Validates n rows for consistent column data types
     -header <Row No>                                        Indicates which row to use to get column headers
     -skip <Row No>                                          Indicates how may roaws should be skipped after header row to get data rows
     -seperator <char>                                       Charecter seperating Columns                                                                             Default:,
     -splitter <regex>                                       RegEx used for splitting rows into columns                                                               Default:,(?=(?:[^"]*"[^"]*")*[^"]*$)
     -TimeFormat <format>                                    Time format used in input files                                                                          Default:MM/dd/yyyy HH:mm:ss.fff
     -Precision <precision>                                  Supported:Hours<1>,Minutes<2>,Seconds<3>,MilliSeconds<4>,MicroSeconds<5>,NanoSeconds<6>                  Default:Seconds
     -filter <filter>                                        Filter input data file, Supported:Measurement (import preexisting measurements), Field (import preexisting fields), Columns (import specified columns)

  [1]: https://github.com/influxdb/influxdb
  [2]: https://github.com/grafana/grafana
