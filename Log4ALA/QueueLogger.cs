﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Log4ALA
{
    public class QueueLogger
    {
        // Error message displayed when queue overflow occurs. 
        protected const String QueueOverflowMessage = "\n\nAzure Log Analytics buffer queue overflow. Message dropped.\n\n";


        private ConcurrentDictionary<int, StringBuilder> alaBatchTypes = new ConcurrentDictionary<int, StringBuilder>();
        private ConcurrentDictionary<int, int> alaBatchByteLength = new ConcurrentDictionary<int, int>();
        private ConcurrentDictionary<int, int> alaBatchLength = new ConcurrentDictionary<int, int>();


        // Minimal delay between attempts to reconnect in milliseconds. 
        protected const int MinDelay = 100;

        // Maximal delay between attempts to reconnect in milliseconds. 
        protected const int MaxDelay = 10000;

        protected readonly BlockingCollection<string> Queue;
        protected readonly Thread WorkerThread;
        protected readonly Random Random = new Random();

        protected bool IsRunning = false;

        public string WorkspaceId { get; set; }

        private byte[] SharedKeyBytes { get; set; }
        private string sharedKey;
        public string SharedKey
        {
            set
            {
                sharedKey = value;
                SharedKeyBytes = Convert.FromBase64String(sharedKey);
            }
            get
            {
                return sharedKey;
            }
        }


        public string LogType { get; set; }

        public string AzureApiVersion { get; set; }
        public int? HttpDataCollectorRetry { get; set; }

        public bool LogMessageToFile { get; set; }
        public bool? AppendLogger { get; set; }
        public bool? AppendLogLevel { get; set; }

        private Log4ALAAppender appender;

        private Timer logQueueSizeTimer = null;

        private AlaTcpClient alaClient = null;



        // Size of the internal event queue. 
        public int? LoggingQueueSize { get; set; } = ConfigSettings.DEFAULT_LOGGER_QUEUE_SIZE;

        public QueueLogger(Log4ALAAppender appender)
        {
            Queue = new BlockingCollection<string>(LoggingQueueSize != null && LoggingQueueSize > 0 ? (int)LoggingQueueSize : ConfigSettings.DEFAULT_LOGGER_QUEUE_SIZE);

            WorkerThread = new Thread(new ThreadStart(Run));
            WorkerThread.Name = $"Azure Log Analytics Log4net Appender ({appender.Name})";
            WorkerThread.IsBackground = true;
            this.appender = appender;
            if (ConfigSettings.IsLogQueueSizeInterval)
            {
                CreateLogQueueSizeTimer();
            }
        }

        private void CreateLogQueueSizeTimer()
        {
            if (logQueueSizeTimer != null)
            {
                logQueueSizeTimer.Dispose();
                logQueueSizeTimer = null;
            }
            //create scheduler to log queue size to Azure Log Analytics start after 10 seconds and then log size each (2 minutes default)
            logQueueSizeTimer = new Timer(new TimerCallback(LogQueueSize), this, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(ConfigSettings.LogQueueSizeInterval));
        }

        private void LogQueueSize(object state)
        {
            try
            {
                QueueLogger queueLogger = (QueueLogger)state;
                string message = $"{queueLogger.appender.Name}-Size={queueLogger.Queue.Count}";
                queueLogger.appender.log.Inf(message, queueLogger.appender.logMessageToFile);

                if (alaClient != null)
                {
                    HttpRequest($"{{\"Msg\":\"{message}\",\"DateValue\":\"{DateTime.UtcNow.ToString("o")}\"}}");
                }

            }
            catch (Exception)
            {
                //continue
            }
        }


        protected virtual void Run()
        {
            try
            {
                // Open connection.
                ReopenConnection();

                // Send data in queue.
                while (true)
                {
                    // Take data from queue.
                    string line = string.Empty;
                    //string dateValue = DateTime.Now.ToUniversalTime().ToString("o");

                    //while (Queue.TryTake(out line))
                    //{
                    //    try
                    //    {
                    //        if (line != null)
                    //        {
                    //            var lineObj = JObject.Parse(line);
                    //            //lineObj["DateValue"] = dateValue;

                    //            //line = JsonConvert.SerializeObject(lineObj, Formatting.None);


                    //            int uniqueKeyHash = String.Join(".", AllTokens(lineObj).Where(t => t.Type == JTokenType.Property).Select(prop => ((JProperty)prop).Name).ToArray()).GetHashCode();
                    //            int byteLength = System.Text.Encoding.Unicode.GetByteCount(line);
                    //            int byteLengthSum = 0;
                    //            int numItems = 0;

                    //            if (alaBatchTypes.ContainsKey(uniqueKeyHash))
                    //            {
                    //                byteLengthSum = alaBatchByteLength[uniqueKeyHash] + byteLength;
                    //                numItems = ++alaBatchLength[uniqueKeyHash];

                    //                alaBatchByteLength.AddOrUpdate(uniqueKeyHash, byteLengthSum, (key, oldValue) => byteLengthSum);
                    //                alaBatchTypes[uniqueKeyHash].Append(line);
                    //                alaBatchTypes[uniqueKeyHash].Append(",");
                    //            }
                    //            else
                    //            {
                    //                byteLengthSum = byteLength;
                    //                numItems = 1;

                    //                StringBuilder buffer = new StringBuilder();
                    //                buffer.Append("[");
                    //                buffer.Append(line);
                    //                buffer.Append(",");
                    //                if (alaBatchTypes.TryAdd(uniqueKeyHash, buffer))
                    //                {
                    //                    alaBatchByteLength.TryAdd(uniqueKeyHash, byteLengthSum);
                    //                    alaBatchLength.TryAdd(uniqueKeyHash, numItems);
                    //                }
                    //            }

                    //            if (byteLengthSum < 29000000 && numItems > 10)
                    //            {
                    //                break;
                    //            }
                    //        }
                    //    }
                    //    catch (Exception)
                    //    {
                    //        continue;
                    //    }
                    //}
                    int byteLength = 0;
                    int numItems = 0;
                    StringBuilder buffer = StringBuilderCache.Acquire();
                    buffer.Append("[");

                    while ((Queue.TryTake(out line) && byteLength < 29000000) || (Queue.TryTake(out line) && numItems < 10))
                    {
                        try
                        {

                            if (line != null)
                            {
                                byteLength += System.Text.Encoding.Unicode.GetByteCount(line);

                                buffer.Append(line);
                                buffer.Append(",");
                                ++numItems;
                            }

                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }





                    // Send data, reconnect if needed.
                    //foreach (var key in alaBatchTypes.Keys)
                    //{
                    //    StringBuilder bufferRemoved;

                    //if (alaBatchTypes.TryRemove(key, out bufferRemoved))
                    //{
                    //    int byteLengthRemoved, lengthRemoved;
                    //    alaBatchByteLength.TryRemove(key, out byteLengthRemoved);
                    //    alaBatchLength.TryRemove(key, out lengthRemoved);

                    while (true)
                    {
                        try
                        {
                            string alaPayLoad = StringBuilderCache.GetStringAndRelease(buffer).TrimEnd(",".ToCharArray());
                            HttpRequest($"{alaPayLoad}]");
                            try
                            {
                                appender.log.Inf($"[{appender.Name}] - {alaPayLoad}", appender.logMessageToFile);
                            }
                            catch (Exception)
                            {
                                //continue
                            }
                            break;

                        }
                        catch (Exception ex)
                        {
                            // Reopen the lost connection.
                            appender.log.War($"[{appender.Name}] - reopen lost connection. [{ex}]");
                            ReopenConnection();
                            continue;
                        }

                        break;
                    }

                    //}
                    //}
                }
            }
            catch (ThreadInterruptedException ex)
            {
                string errMessage = $"[{appender.Name}] - Azure Log Analytics HTTP Data Collector API client was interrupted. {ex}";
                appender.log.Err(errMessage);
                appender.extraLog.Err(errMessage);
            }
        }


        private static IEnumerable<JToken> AllTokens(JObject obj)
        {
            var toSearch = new Stack<JToken>(obj.Children());
            while (toSearch.Count > 0)
            {
                var inspected = toSearch.Pop();
                yield return inspected;
                foreach (var child in inspected)
                {
                    toSearch.Push(child);
                }
            }
        }

        protected void ReopenConnection()
        {
            CloseConnection();

            var rootDelay = MinDelay;
            while (true)
            {
                try
                {
                    OpenConnection();
                    try
                    {
                        appender.log.Inf($"[{appender.Name}] - successfully reconnected to AlaClient", true);
                    }
                    catch (Exception)
                    {
                        //continue
                    }
                    return;
                }
                catch (Exception ex)
                {
                    string errMessage = $"[{appender.Name}] - Unable to connect to AlaClient => [{ex}]";
                    appender.log.Err(errMessage);
                    appender.extraLog.Err(errMessage);
                }

                rootDelay *= 2;
                if (rootDelay > MaxDelay)
                    rootDelay = MaxDelay;

                var waitFor = rootDelay + Random.Next(rootDelay);

                try
                {
                    Thread.Sleep(waitFor);
                }
                catch (Exception ex)
                {
                    string errMessage = $"[{appender.Name}] - Thread sleep exception => [{ex}]";
                    appender.log.Err(errMessage);
                    throw new ThreadInterruptedException();
                }
            }
        }

        protected virtual void OpenConnection()
        {
            try
            {
                if (alaClient == null)
                {
                    // Create AlaClient instance providing all needed parameters.
                    alaClient = new AlaTcpClient(sharedKey, WorkspaceId);
                }

                alaClient.Connect();

            }
            catch (Exception ex)
            {
                throw new IOException($"An error occurred while init/ping AlaClient.", ex);
            }
        }

        protected virtual void CloseConnection()
        {
            if (alaClient != null)
            {
                alaClient.Close();
            }
        }

        public virtual void AddLine(string line)
        {
            if (!IsRunning)
            {
                WorkerThread.Start();
                IsRunning = true;
            }


            // Try to append data to queue.
            if (!Queue.TryAdd(line))
            {
                if (!Queue.TryAdd(line))
                {
                    appender.log.War($"[{appender.Name}] - QueueOverflowMessage");
                }
            }
        }

        public void interruptWorker()
        {
            WorkerThread.Interrupt();
        }


        private void HttpRequest(string log)
        {
            string result = string.Empty;

            var utf8Encoding = new UTF8Encoding();
            Byte[] content = utf8Encoding.GetBytes(log);

            var rfcDate = DateTime.Now.ToUniversalTime().ToString("r");
            var signature = HashSignature("POST", content.Length, "application/json", rfcDate, "/api/logs");

            string alaServerAddr = $"{WorkspaceId}.ods.opinsights.azure.com";
            string alaServerContext = $"/api/logs?api-version={AzureApiVersion}";

            // Send request headers
            var builder = new StringBuilder();
            builder.AppendLine($"POST {alaServerContext} HTTP/1.1");
            builder.AppendLine($"Host: {alaServerAddr}");
            builder.AppendLine($"Content-Length: " + content.Length);   // only for POST request
            builder.AppendLine("Content-Type: application/json");
            builder.AppendLine($"Log-Type: {LogType}");
            builder.AppendLine($"x-ms-date: {rfcDate}");
            builder.AppendLine($"Authorization: {signature}");
            builder.AppendLine("time-generated-field: DateValue");
            builder.AppendLine("Connection: close");
            builder.AppendLine();
            var header = Encoding.ASCII.GetBytes(builder.ToString());

            // Send http headers
            alaClient.Write(header, 0, header.Length, true);

            // Send payload data
            string httpResultBody = alaClient.Write(content, 0, content.Length);

            if (!string.IsNullOrWhiteSpace(httpResultBody))
            {
                string errMessage = httpResultBody;
                appender.log.Err(errMessage);
                throw new Exception(errMessage);
            }
        }


        /// <summary>
        /// SHA256 signature hash
        /// </summary>
        /// <returns></returns>
        private string HashSignature(string method, int contentLength, string contentType, string date, string resource)
        {
            var stringtoHash = method + "\n" + contentLength + "\n" + contentType + "\nx-ms-date:" + date + "\n" + resource;
            var encoding = new System.Text.ASCIIEncoding();
            var bytesToHash = encoding.GetBytes(stringtoHash);
            using (var sha256 = new HMACSHA256(SharedKeyBytes))
            {
                var calculatedHash = sha256.ComputeHash(bytesToHash);
                var stringHash = Convert.ToBase64String(calculatedHash);
                return "SharedKey " + WorkspaceId + ":" + stringHash;
            }
        }


    }


 
}
