using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ClassLibrary1;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public override void Run()
        {
            RobotParser parser = new RobotParser();

            HtmlCrawler htmlCrawlerCNN = new HtmlCrawler(new HashSet<string>());

            HtmlCrawler htmlCrawlerNBA = new HtmlCrawler(new HashSet<string>());

            bool loading = false;

            bool crawling = false;

            bool idle = true;

            Trace.TraceInformation("WorkerRole1 is running");

            while(true)
            {
                Thread.Sleep(50);
                string status = "";
                if (idle == true)
                {
                    status = "Idle";
                }
                else if (crawling == true)
                {
                    status = "Crawling";
                }
                else if (loading == true)
                {
                    status = "Loading";
                }

                //add performance with no changes in queue size, index size, or number crawled
                var crawled = 0;
                var sizeQueue = 0;
                var sizeIndex = 0;
                TableQuery<Performance> query3 = new TableQuery<Performance>()
                    .Take(1);

                foreach (Performance item in StorageManager.getPerformanceTable().ExecuteQuery(query3))
                {
                    crawled = item.NumCrawled;
                    sizeQueue = item.SizeQueue;
                    sizeIndex = item.SizeIndex;
                }

                Performance.insertPerformance(status, crawled, sizeQueue, sizeIndex);


                //Handle Command Queue
                CloudQueueMessage commandMessage = StorageManager.getCommandQueue().GetMessage(TimeSpan.FromMinutes(5));

                //In the case there is no more Urls to crawl, or at the beginning, this command message will be called
                if (commandMessage != null)
                {
                    StorageManager.getCommandQueue().DeleteMessage(commandMessage);

                    //command message is stop
                    if (commandMessage.AsString == "stop")
                    {
                        //clear queue and table
                        StorageManager.deleteAllQueues();
                        StorageManager.deleteTables();
                        //reset parser and crawler
                        parser = new RobotParser("");
                        htmlCrawlerCNN.crawlable = false;
                        htmlCrawlerCNN.Visited = new HashSet<string>();
                        htmlCrawlerCNN.Disallow = new HashSet<string>();

                        htmlCrawlerNBA.crawlable = false;
                        htmlCrawlerNBA.Visited = new HashSet<string>();
                        htmlCrawlerNBA.Disallow = new HashSet<string>();

                        loading = false;
                        crawling = false;
                        idle = true;

                        //add performance, clear queue sizes

                        Performance.insertPerformance("Idle", 0, 0, 0);
                    }

                    //command message is start
                    if (commandMessage.AsString.StartsWith("start:"))
                    {
                        crawling = false;
                        idle = false;
                        loading = true;

                        //add performance with no changes in queue size, index size, or number crawled
                        TableQuery<Performance> queryStart = new TableQuery<Performance>()
                            .Take(1);

                        foreach (Performance item in StorageManager.getPerformanceTable().ExecuteQuery(queryStart))
                        {
                            crawled = item.NumCrawled;
                            sizeQueue = item.SizeQueue;
                            sizeIndex = item.SizeIndex;
                        }

                        Performance.insertPerformance("Loading", crawled, sizeQueue, sizeIndex);


                        ServicePointManager.Expect100Continue = true;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                        var robotFile = commandMessage.AsString.Substring(6);

                        string contents;
                        using (var wc = new System.Net.WebClient())
                        {
                            contents = wc.DownloadString(robotFile);
                        }

                        //create and parse through robots.txt
                        parser = new RobotParser(contents);

                        foreach (string filepath in parser.XMLFiles)
                        {
                            //only XMLs from cnn and nba
                            if (filepath.Contains("cnn") || filepath.Contains("nba"))
                            {
                                CloudQueueMessage filepathMessage = new CloudQueueMessage(filepath);
                                StorageManager.getXMLQueue().AddMessage(filepathMessage);
                            }
                        }

                        if (robotFile.Contains("cnn"))
                        {
                            htmlCrawlerCNN = new HtmlCrawler(parser.Disallow);
                        }

                        if (robotFile.Contains("bleacherreport"))
                        {
                            htmlCrawlerNBA = new HtmlCrawler(parser.Disallow);
                        }
                        //set the crawler with the disallows

                        Performance.insertPerformance("Idle", crawled, sizeQueue, sizeIndex);
                    }
                }


                //Handle XML Queue
                CloudQueueMessage XML = StorageManager.getXMLQueue().GetMessage(TimeSpan.FromMinutes(5));
                while (XML != null)
                {
                    if (XML.AsString.Contains("cnn.com"))
                    {
                        htmlCrawlerCNN.readXMLUrl(XML.AsString);
                    }
                    if (XML.AsString.Contains("bleacherreport.com"))
                    {
                        htmlCrawlerNBA.readXMLUrl(XML.AsString);
                    }

                    StorageManager.getXMLQueue().DeleteMessage(XML);
                    XML = StorageManager.getXMLQueue().GetMessage(TimeSpan.FromMinutes(5));
                }

                //Handle HTML Queue
                CloudQueueMessage HTML = StorageManager.getUrlQueue().GetMessage(TimeSpan.FromMinutes(5));
                if (HTML != null)
                {
                    //handle performance
                    if (htmlCrawlerCNN.crawlable || htmlCrawlerNBA.crawlable)
                    {
                        idle = false;
                        loading = false;
                        crawling = true;

                        //add performance, reduce queue size

                        TableQuery<Performance> queryCNN = new TableQuery<Performance>()
                            .Take(1);

                        foreach (Performance item in StorageManager.getPerformanceTable().ExecuteQuery(query3))
                        {
                            crawled = item.NumCrawled;
                            sizeQueue = item.SizeQueue - 1;
                            sizeIndex = item.SizeIndex;
                        }

                        Performance.insertPerformance("Crawling", crawled, sizeQueue, sizeIndex);
                    }
                    //handles if it is a cnn article
                    if (htmlCrawlerCNN.crawlable == true && HTML.AsString.Contains("cnn.com"))
                    {
                        htmlCrawlerCNN.parseHTML(HTML.AsString);
                        StorageManager.getUrlQueue().DeleteMessage(HTML);
                    }
                    //handles if it is a bleacher report article
                    else if (htmlCrawlerNBA.crawlable == true && HTML.AsString.Contains("bleacherreport.com"))
                    {
                        htmlCrawlerNBA.parseHTML(HTML.AsString);
                        StorageManager.getUrlQueue().DeleteMessage(HTML);
                    }

                }
            }   
        }
    

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at https://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("WorkerRole1 has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole1 has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}
