using HtmlAgilityPack;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ClassLibrary1
{
    public class HtmlCrawler
    {
        public HashSet<string> Visited { get; set; }

        public HashSet<string> Disallow { get; set; }

        public List<string> XMLurls { get; set; }

        public bool crawlable { get; set; }

        private XmlReaderSettings settings = new XmlReaderSettings();

        public HtmlCrawler() { }

        public HtmlCrawler(HashSet<string> disallow)
        {
            this.Disallow = disallow;
            this.Visited = new HashSet<string>();
            this.XMLurls = new List<string>();
            this.crawlable = true;
        }

        //reads through valid XML links and add their HTML links to the Url Queue
        public void readXMLUrl(string url)
        {
            settings.DtdProcessing = DtdProcessing.Parse;

            XmlReader reader = XmlReader.Create(url, settings);
            reader.MoveToContent();
            while (reader.Read())
            {
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

                Performance.insertPerformance("Loading", crawled, sizeQueue, sizeIndex);

                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "loc")
                    {
                        reader.Read();

                        if (reader.NodeType == XmlNodeType.Text && !reader.Value.Contains("xml"))
                        {
                            bool restricted = checkDisallow(reader.Value, Disallow);

                            //add html urls to list to be put into HTML queue
                            if (!restricted)
                            {
                                if (!this.Visited.Contains(reader.Value))
                                {
                                    if (!checkDisallow(reader.Value, this.Disallow))
                                    {
                                        if (!this.Visited.Contains(reader.Value))
                                        {
                                            addToUrlQueue(reader.Value);
                                        }
                                    }
                                }
                            }
                            else if ((reader.Value.Contains("xml") && (reader.Value.Contains("2018")) || reader.Value.Contains("nba.xml")))
                            {
                                //add xml urls to list to be put back into XML queue
                                this.XMLurls.Add(reader.Value);

                                CloudQueueMessage newXMLMessage = new CloudQueueMessage(reader.Value);
                                StorageManager.getXMLQueue().AddMessage(newXMLMessage);
                            }
                        }
                    }
                }
            }
        }

        //parse through HTML urls, index the information, add them to the Url table. Follow each link accompanied by it, check its validity and visit status, then add back to queue.
        public void parseHTML(string url)
        {
            if (crawlable)
            {
                //update status, increase number crawled
                var crawled = 0;
                var sizeQueue = 0;
                var sizeIndex = 0;
                TableQuery<Performance> query3 = new TableQuery<Performance>()
                    .Take(1);

                foreach (Performance item in StorageManager.getPerformanceTable().ExecuteQuery(query3))
                {
                    crawled = item.NumCrawled + 1;
                    sizeQueue = item.SizeQueue;
                    sizeIndex = item.SizeIndex;
                }

                Performance.insertPerformance("Crawling", crawled, sizeQueue, sizeIndex);
                try
                {
                    //index information
                    var Url = url;
                    var web = new HtmlWeb();
                    var doc = web.Load(url);
                    var title = doc.DocumentNode.SelectSingleNode("//head/title").InnerHtml;
                    var meta = doc.DocumentNode.SelectNodes("//meta");
                    string date = "no date found";
                    foreach (HtmlNode tag in meta)
                    {
                        string property = tag.GetAttributeValue("property", "");
                        if (property.Contains("published_time") || property.Contains("pubdate"))
                        {
                            date = tag.GetAttributeValue("content", "");
                        }
                    }

                    //add to table
                    addToTable(Url, title, date);

                    //check header links that relate to the link
                    var linksList = doc.DocumentNode.SelectNodes("//head/link");
                    if (linksList != null)
                    {
                        foreach (HtmlNode link in linksList)
                        {
                            string href = link.GetAttributeValue("href", "");
                            if (href.Contains("cnn.com") || href.Contains("bleacherreport.com/articles") || href.Contains("bleacherreport.com/nba"))
                            {
                                //Debug.WriteLine("new link: ");
                                //Debug.WriteLine(href);
                                if (!checkDisallow(href, this.Disallow))
                                {
                                    if (!this.Visited.Contains(href))
                                    {
                                        addToUrlQueue(href);
                                    }
                                }
                            }
                        }
                    }

                    //check body links that relate to the link
                    var aList = doc.DocumentNode.SelectNodes("//a[@href]");
                    if (aList != null)
                    {
                        foreach (HtmlNode a in aList)
                        {
                            string href = a.GetAttributeValue("href", "");
                            if (href.Contains("cnn.com") || href.Contains("bleacherreport.com/articles") || href.Contains("bleacherreport.com/nba"))
                            {
                                //Debug.WriteLine("new link: ");
                                //Debug.WriteLine(href);
                                if (!checkDisallow(href, this.Disallow))
                                {
                                    if (!this.Visited.Contains(href))
                                    {
                                        addToUrlQueue(href);
                                    }
                                }
                            }
                        }
                    }

                }
                catch (Exception e)
                {
                    //put in exception table with URL
                    ExceptionUrl except = new ClassLibrary1.ExceptionUrl(e.ToString(), url);

                    //add exception to table
                    TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(except);
                    StorageManager.getExceptionTable().Execute(insertOrReplaceOperation);
                }
            }
            else
            {
                return;
            }

        }

        //add to the UrlTable
        public void addToTable(string url, string title, string date)
        {
            string[] keywords = title.Split(' ');

            foreach(string keyword in keywords)
            {
                string keywordTrim = keyword.Trim(new char[] { ' ', '-', '\'', '\"', ',' }).ToLower();
                //Debug.WriteLine(keyword);
                WebPage page = new WebPage(url, title, keywordTrim, date);
                //add page to table
                if(keywordTrim != " " && keywordTrim != null && keywordTrim != "")
                {
                    TableOperation insertOperation = TableOperation.Insert(page);
                    StorageManager.getTable().Execute(insertOperation);
                }
                
                //add performance and increase index counter
                var crawled = 0;
                var sizeQueue = 0;
                var sizeIndex = 0;
                TableQuery<Performance> query3 = new TableQuery<Performance>()
                    .Take(1);

                foreach (Performance item in StorageManager.getPerformanceTable().ExecuteQuery(query3))
                {
                    crawled = item.NumCrawled;
                    sizeQueue = item.SizeQueue;
                    sizeIndex = item.SizeIndex + 1;
                }

                Performance.insertPerformance("Crawling", crawled, sizeQueue, sizeIndex);
            }
        }

        //check if the link is restricted
        public bool checkDisallow(string url, HashSet<string> disallow)
        {
            bool restricted = false;

            foreach (string restriction in Disallow)
            {
                if (url.Contains(restriction))
                {
                    restricted = true;
                    break;
                }
            }

            return restricted;
        }

        public void addToUrlQueue(string url)
        {
            this.Visited.Add(url);
            CloudQueueMessage newHTML = new CloudQueueMessage(url);
            StorageManager.getUrlQueue().AddMessage(newHTML);

            //add performance and increase queue counter
            var crawled = 0;
            var sizeQueue = 0;
            var sizeIndex = 0;
            TableQuery<Performance> query3 = new TableQuery<Performance>()
                .Take(1);

            foreach (Performance item in StorageManager.getPerformanceTable().ExecuteQuery(query3))
            {
                crawled = item.NumCrawled;
                sizeQueue = item.SizeQueue + 1;
                sizeIndex = item.SizeIndex;
            }

            Performance.insertPerformance("Crawling", crawled, sizeQueue, sizeIndex);
        }
    }
}
