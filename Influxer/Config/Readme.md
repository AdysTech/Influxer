## Overrideable Config Section
This allows the application to take configuration from a file, and allow the console to pass overrides for a specific parameters.

### Need
A console application will have (as a tredition) many command line parameters which will be supplied by user while lanching the program.
Each of those user providable settings needs to have some kind of help text, which as a convention gets printed when the application is lanuched with `application.exe /help` or ``application.exe --help` or ``application.exe /?` 
A program also may need to use many configurable settings, instead of hardcoding (don't ever do that!). These settings are usually kept in a JSON based config files or old INI based schema's.

.Net framework passes the command line arguments in a string array to main method. It also provides a standard framework classes for xml schema based configurations. (For INI schema there are no standard classes, but `WritePrivateProfileString` and `GetPrivateProfileString` win32 API can be used, or a wrapper like [An INI file handling class using C#](http://www.codeproject.com/Articles/1966/An-INI-file-handling-class-using-C))

Still there are no standard way to handle the help printing and handling cases where you want to provide a standard default settings in a xml schema, but allow the user to override them while launching.

### Approach
Create a custom attribute called `CommandLineArgAttribute` which can provide command line argument details, and will be used to decorate config elements. Later from the code, all these custom attributes will be checked, and the configuration values derived from JSON schema will be replaced with the value passed in by the commandline.

There is also another attribute `DeafualtValueAttribute` which provides a way to keep the propery default values seperate from the actual class implementations. That way if you ever need to know if the property value has chnaged since the initialization, and you need to reset to default, this attribute will be handy. The attribute also provides meta data to track how to chnage the string default value to target property type.

*Note:* Initially `OverrideableConfigSection` was implementred as an extension of [custom config sections](https://msdn.microsoft.com/en-us/library/system.configuration.configurationsection(v=vs.110).aspx) supported by .Net framework, but as it was not fully supported in .Net Core (before 2.0) moved to JSON.
