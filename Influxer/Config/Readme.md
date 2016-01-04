##Overrideable Config Section
This is a extension of [custom config sections](https://msdn.microsoft.com/en-us/library/system.configuration.configurationsection(v=vs.110).aspx) supported by .Net framework.
This allows the application to take teh configuration from xml file, and allow the console to pass overrides for a specific parameters.

###Need
A console application will have (as a tredition) many command line parameters which will be supplied by user while lanching the program.
Each of those user providable settings needs to have some kind of help text, which as a convention gets printed when the application is lanuched with `application.exe /help` or ``application.exe --help` or ``application.exe /?` 
A program also may need to use many configurable settings, instead of hardcoding (don't ever do that!). These settings are usually kept in a xml based config files or old INI based schema's.

.Net framework passes the command line arguments in a string array to main method. It also provides a standard framework classes for xml schema based configurations. (For INI schema there are no standard classes, but `WritePrivateProfileString` and `GetPrivateProfileString` win32 API can be used, or a wrapper like [An INI file handling class using C#](http://www.codeproject.com/Articles/1966/An-INI-file-handling-class-using-C))

Still there are no standard way to handle the help printing and handling cases where you want to provide a standard default settings in a xml schema, but allow the user to override them while launching.

###Approach
Create a custom attribute called `CommandLineArgAttribute` which can provide command line argument details, and will be used to decorate config elements. Later from the code, all these custom attributes will be checked, and the configuration values derived from xml schema will be replaced with the value passed in by the commandline.
e.g

```C#
[CommandLineArgAttribute ("-table", Usage = "-table <table name>", Description = "Measurement name in InfluxDB", DefaultValue = "InfluxerData")]
[ConfigurationProperty ("TableName", DefaultValue = "InfluxerData")]
public string TableName
{
    get { return (string) this["TableName"]; }
    set { this["TableName"] = value; }
}
```
The example above provides a way to opverride table name from its default value of InfluxerData to something else, without actually editing the config file.
below chunk of code uses the attibute value passed kin and passes it to `ConvertFromString` method of the converter attached to the config element. So no need to code for number parsing or enum parsing, the same code that gets triggered for XML parsing will be used to parse the commandline argument as well

```C#
if ( CommandLine.ContainsKey (cmdAttribute.Argument.ToLower ()) )
{
	found = true;
	try
	{
		this[prop.Name] = this.Properties[prop.Name].Converter.ConvertFromString (CommandLine[cmdAttribute.Argument]);
	}
	catch ( Exception e )
	{
		throw new ArgumentException ("Invalid Argument " + cmdAttribute.Argument, e);
	}
	CommandLine.Remove (cmdAttribute.Argument);
}
```
