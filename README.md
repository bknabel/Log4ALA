## Log4ALA

Log4Net appender fo Azure Log Analytics (ALA)... sending data to Azure Log Analytics.
The data will also be logged/sent asynchronously for high performance and to avoid blocking the caller thread.

## Get it

You can obtain this project as a [Nuget Package](https://www.nuget.org/packages/Log4ALA)

    Install-Package Log4ALA

Or reference it and use it according to the [License](./LICENSE).


## Create a Azure Log Analytics Workspace

[Chapter 2 Create a workspace](https://docs.microsoft.com/en-GB/azure/log-analytics/log-analytics-get-started)

## Get the workspace id and shared key (aka primary key)

![workspaceId/SharedKey](https://raw.githubusercontent.com/ptv-logistics/Log4ALA/master/oms.jpg)

## Use it

This example is also available as a [LoggerTests.cs](https://github.com/ptv-logistics/Log4ALA/blob/master/Log4ALA/LoggerTests.cs):

```csharp
using log4net;
using System;

namespace Log4ALATest
{
    class LoggerTests
    {

        private static ILog alaLogger1 = LogManager.GetLogger("Log4ALALogger_1");
        private static ILog alaLogger2 = LogManager.GetLogger("Log4ALALogger_2");
        private static ILog alaLogger3 = LogManager.GetLogger("Log4ALALogger_3");

        static void Main(string[] args)
        {

            //Log message as anonymous type... the properties will then be mapped to Azure Log Analytic properties/columns.
            for (int i = 0; i < 10; i++)
            {
                alaLogger1.Info(new { id = $"log-{i}", message = $"test-{i}" });
            }

            System.Console.WriteLine("done1");

            //Log messages with semicolon separated key=value strings...the keys will then be mapped to Azure Log Analytic properties/columns.
            for (int i = 0; i < 10; i++)
            {
                alaLogger2.Info($"id=log-{i}; message=test-{i}");
            }

            System.Console.WriteLine("done2");

            //Log messages with semicolon separated key=value strings and duplicate key detection... the duplicate keys in the following example 
            //will be mapped to Azur Log Analytic properties/columns message_Duplicate0 and message_Duplicate1.
            for (int i = 0; i < 10; i++)
            {
                alaLogger2.Info($"id=log-{i}; message=test-{i}; message=test-{i}; message=test-{i}");
            }

            System.Console.WriteLine("done3");

            //Log message as json string ...the json properties will then be mapped to Azure Log Analytic properties/columns.
            for (int i = 0; i < 10; i++)
            {
                alaLogger3.Info($"{{\"id\":\"log-{{i}}\", \"message\":\"test-{{i}}\"}}");
            }

            System.Console.WriteLine("done4");

            System.Threading.Thread.Sleep(new TimeSpan(0, 5, 0));
        }
    }
}

``` 

## Proxy settings

At the the moment the default proxy could only be set by config (Web.config or App.config):

Refer to this [article](https://msdn.microsoft.com/en-us/library/kd3cf2ex(v=vs.110).aspx) for more information.
```xml
<configuration>
    <system.net>
        <defaultProxy>
            <proxy
              proxyaddress="http://IP:PORT"
              bypassonlocal="true" />
        </defaultProxy>
    </system.net>
<configuration>
``` 


or by code:

```csharp

System.Net.WebRequest.DefaultWebProxy = new System.Net.WebProxy("http://IP:PORT/", true);

``` 

## Features

1. You can batch multiple log messages together in a single request by configuration with the properties batchSizeInBytes, 
batchNumItems or batchWaitInSec (described further down).
2. Auto detection/convertion of numeric, boolean, and dateTime string values to the Azure Log Analytics type _d, _b and _t.
3. Field values greater than 32 KB will be truncated (the value could be configured with maxFieldByteLength).
4. Field names greater than 500 chars will be truncated (the value could be configured with maxFieldNameLength).
5. Configurable core field names


## General Configuration 

It's possible to configure multiple log4net Azure Log Analytics appender(s). The properties (e.g. workspaceId, SharedKey...) of each appender could be configured as 
appender properties, appSettings in App.config/Web.config or as Azure Configuration Setting with fallback strategy AzureSetting=>appSetting=>appenderProperty.
If the properties will be configured as appSetting in App.config/Web.config or as Azure Configuration Settings it's important to attach the appender name as prefix 
to the property e.g. **YourAppenderName.workspaceId**


## Example App Configuration file

This configuration is also available as a [App.config](https://github.com/ptv-logistics/Log4IoTHub/blob/master/Log4IoTHubTest/App.config):


```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5.2" />
  </startup>

  <appSettings>
    <!--
    don't forget to add [assembly: log4net.Config.XmlConfigurator()] to AssemblyInfo.cs
    -->
    <add key="log4net.Config" value="log4net.config"/>
    <add key="log4net.Config.Watch" value="True"/>

	<!--ALAAppender name (prefix) dependent settings-->
    <add key="Log4ALAAppender_1.workspaceId" value=""/>
    <add key="Log4ALAAppender_1.SharedKey" value=""/>
    <add key="Log4ALAAppender_1.logType" value=""/>
    <add key="Log4ALAAppender_1.logMessageToFile" value="true"/>

    
    <add key="Log4ALAAppender_2.workspaceId" value=""/>
    <add key="Log4ALAAppender_2.SharedKey" value=""/>
    <add key="Log4ALAAppender_2.logType" value=""/>
    <add key="Log4ALAAppender_2.logMessageToFile" value="true"/>

  </appSettings>

</configuration>
``` 

## Example Log4Net Configuration file

```xml
﻿<?xml version="1.0" encoding="utf-8" ?>
<log4net>

  <appender name="Log4ALAAppender_1" type="Log4ALA.Log4ALAAppender, Log4ALA" >
    <filter type="log4net.Filter.LevelRangeFilter">
      <levelMin value="INFO" />
      <levelMax value="FATAL" />
    </filter>
  </appender>
  <appender name="Log4ALAAppender_2" type="Log4ALA.Log4ALAAppender, Log4ALA" />


  <appender name="Log4ALAAppender_3" type="Log4ALA.Log4ALAAppender, Log4ALA">

    <!--mandatory id of the Azure Log Analytics WorkspaceID -->
    <workspaceId value="" />
    <!--mandatory primary key Primary Key OMS Portal Overview/Settings/Connected Sources-->
    <SharedKey value="" />
    <!-- mandatory log type... the name of the record type that you'll be creating-->
    <logType value="" />
    <!-- optional API version of the HTTP Data Collector API (default 2016-04-01) -->
    <!--<azureApiVersion value="2016-04-01" />-->
    <!-- optional max retries if the HTTP Data Collector API request failed (default 6 retries) -->
    <!--<httpDataCollectorRetry value="6" />-->

    <!-- 
    optional debug setting which should only be used during development or on testsystem. Set 
	logMessageToFile=true to inspect your messages (in log4ALA_info.log) which will be sent to the 
	Azure Log Analytics Workspace.
    -->
    <!--<logMessageToFile value="true"/>-->

    <!-- 
    optional name of an logger defined further down with an depending appender e.g. logentries to log internal 
	errors. If the value is empty or the property isn't defined errors will only be logged to log4ALA_error.log
    -->
    <!--<errLoggerName value="Log4ALAErrors2LogentriesLogger"/>-->

    <!-- optional appendLogger to enable/disable sending the logger info 
         to Azure Log Analytics (default true)
    <appendLogger value="true"/>
	  -->

    <!-- optional appendLogLevel to enable/disable sending the log level
         to Azure Log Analytics (default true)
    <appendLogLevel value="true"/>
	  -->

    <!-- optional error log file configuration (default relative_assembly_path/log4ALA_error.log)
    <errAppenderFile value="C:\ups\errApp.log"/>
	  -->

    <!-- optional info log file configuration (default relative_assembly_path/log4ALA_info.log)
    <infoAppenderFile value="C:\ups\infoApp.log"/>
	  -->

    <!-- optional batch configuration to send a defined byte size of log messages as batch to Azure Log Analytics 
	(default 0)

    <batchSizeInBytes value="0"/>
	  -->
     <!-- optional batch configuration to send a defined number of log items as batch to Azure Log Analytics 
	     (default 1)

     <batchNumItems value="1"/>
	  -->
     <!-- optional batch configuration to send a time based collection of log messages as batch to Azure Log Analytics 
	     (default 0)

     <batchWaitInSec value="0"/>
	  -->
     <!-- optional interval after a batch process will be finished to send the collected of log messages as batch to 
	     Azure Log Analytics (default 60)

     <batchWaitMaxInSec value="60"/>
	  -->
     <!-- optional trim field values to the max allowed size of 32 KB (default 32 KB)
     <maxFieldByteLength value="32000"/>
	  -->
     <!-- optional to change the core Azure Log Analytics field names 
	     (default {'DateFieldName':'DateValue','MiscMessageFieldName':'MiscMsg','LoggerFieldName':'Logger','LevelFieldName':'Level'})
    
	 <coreFieldNames value="{'DateFieldName':'DateValue','MiscMessageFieldName':'MiscMsg','LoggerFieldName':'Logger','LevelFieldName':'Level'}"/>
	  -->
     
	 <!-- optional trim field values to the max allowed field name length of 500  (default 500)
     <maxFieldNameLength value="500"/>
	  -->

     <!-- optional priority of the background worker thread which collects and send the log messages to Azure Log Analytics
          possible values Lowest/BelowNormal/Normal/AboveNormal/Highest  (default Normal)
     <threadPriority value="Normal"/>
	  -->

  </appender>
  
  <!--
  <appender name="LeAppender" type="log4net.Appender.LogentriesAppender, LogentriesLog4net">
    <immediateFlush value="true" />
    <useSsl value="true" />
    <token value="YOUR_LOGENTRIES_TOKEN" />
    <layout type="log4net.Layout.PatternLayout">
      <param name="ConversionPattern" value="%d{yyyy-MM-dd HH:mm:ss.fff zzz};loglevel=%level%;operation=%m;" />
    </layout>
    <filter type="log4net.Filter.LevelRangeFilter">
      <levelMin value="INFO" />
      <levelMax value="FATAL" />
    </filter>
  </appender>

  <logger name="Log4ALAErrors2LogentriesLogger" additivity="false">
    <level value="ALL" />
    <appender-ref ref="LeAppender" />
  </logger>
  -->
 
  <!--<logger name="Log4ALALoggerAllInOne" additivity="false">
    <appender-ref ref="Log4ALAAppender_1" />
    <appender-ref ref="Log4ALAAppender_2" />
    <appender-ref ref="Log4ALAAppender_3" />
  </logger>-->

  <logger name="Log4ALALogger_1" additivity="false">
    <appender-ref ref="Log4ALAAppender_1" />
  </logger>
  
  <logger name="Log4ALALogger_2" additivity="false">
    <appender-ref ref="Log4ALAAppender_2" />
  </logger>

  <logger name="Log4ALALogger_3" additivity="false">
    <appender-ref ref="Log4ALAAppender_3" />
  </logger>

</log4net>
``` 

## Issues

Keep in mind that this library won't assure that your JSON payloads are being indexed, it will make sure that the HTTP Data Collection API [responds an Accept](https://azure.microsoft.com/en-us/documentation/articles/log-analytics-data-collector-api/#return-codes) typically it takes just a few seconds for the data/payload to be indexed, to know how much time does it take until the posted data has been indexed completely go to the OMS Portal
click *Usage* then scroll over to the right and you can see the Performance dashboard ... There can be Live Site issues causing some delays, hence the official SLA is longer than this see also [SLA for Log Analytics](https://azure.microsoft.com/en-gb/support/legal/sla/log-analytics/v1_1/)

## Supported Frameworks

* .Net 4.5 Full Framework
* .Net 4.5.1 Full Framework
* .Net 4.5.2 Full Framework
* .Net 4.6 Full Framework
* .Net 4.6.1 Full Framework