using ClassLibrary1;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using MoreLinq;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for Search
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class Search : System.Web.Services.WebService
    {
        private static Trie myTrie;
        private static List<string> wikiList;
        private static string finalPath;
        private static Dictionary<string, Tuple<string, DateTime>> cache;

        [WebMethod]
        public string DownloadFile()
        {
            myTrie = new Trie();
            wikiList = new List<string>();

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("vsdeploy");

            if (container.Exists())
            {
                foreach (IListBlobItem item in container.ListBlobs(null, false))
                {
                    if (item.GetType() == typeof(CloudBlockBlob))
                    {
                        CloudBlockBlob blob = (CloudBlockBlob)item;

                        finalPath = Path.Combine(System.IO.Path.GetTempPath(), "wikipedia.txt");
                        using (var fileStream = System.IO.File.OpenWrite(finalPath))
                        {
                            blob.DownloadToStream(fileStream);
                        }

                        return (finalPath);
                    }
                }
            }

            return null;
        }

        [WebMethod]
        public string BuildTrie()
        {
            try
            {
                var stream = finalPath;
                string title = "";
                int numTitles = 0;

                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        title = reader.ReadLine();

                        if (numTitles % 1000 == 0 && numTitles != 0)
                        {
                            PerformanceCounter theMemCounter = new PerformanceCounter("Memory", "Available MBytes");
                            var ram = theMemCounter.NextValue();
                            if (ram < 50)
                            {
                                break;
                            }
                        }

                        myTrie.InsertWord(title);
                        numTitles += 1;   
                    }
                }

                //add number of titles and last title to a table
                TrieData data = new ClassLibrary1.TrieData(title, numTitles);

                //add exception to table
                TableOperation insertOperation = TableOperation.Insert(data);
                StorageManager.getTrieTable().Execute(insertOperation);
            }
            catch (IOException)
            {
                return "failed";
            }
            return null;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> searchTrie(string myInput)
        {
            string input = myInput;
            input = input.ToLower().Trim(' ');
            List<string> answers = myTrie.SearchAll(input);
            return answers;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string searchUrls(string input)
        {
            if(cache == null)
            {
                cache = new Dictionary<string, Tuple<string, DateTime>>();
            }
            input = input.Trim(new char[] { ' ', '-', '\'', '\"', ',' }).ToLower();

            //check to see if results are in the cache and return them if they are
            if(cache.ContainsKey(input))
            {
                return cache[input].Item1;
            }

            var urlTable = StorageManager.getTable();

            List<WebPage> filteredUrlList = new List<WebPage>();

            var keywords = input.Split(' ');
            for (int i = 0; i < keywords.Length; i++)
            {
                TableQuery<WebPage> query = new TableQuery<WebPage>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, keywords[i].Trim(new char[] { ' ', '-', '\'', '\"', ',' })));
                foreach (WebPage page in StorageManager.getTable().ExecuteQuery(query))
                {
                    filteredUrlList.Add(page);
                }
            }

            //LINQ query, groupby URL, order by rank, then by date
            var filter = filteredUrlList.GroupBy(x => x.Url)
                .Select(x => new Tuple<string, string, string, int>(x.Key, x.First().Title, x.First().Date, x.ToList().Count))
                .OrderByDescending(x => x.Item4)
                .ThenBy(x => x.Item3)
                .DistinctBy(x => x.Item2)
                .Take(15);
                

            List<Tuple<string, string, string, int>> resultList = new List<Tuple<string, string, string, int>>();

            foreach(Tuple<string, string, string, int> result in filter)
            {
                resultList.Add(result);
            }

            

            //if the result doesn't exist in the cache, add it
            if (!cache.ContainsKey(input))
            {
                cache.Add(input, new Tuple<string, DateTime>(JsonConvert.SerializeObject(resultList), DateTime.UtcNow));
            }

            //if cached search is older than 20 minutes, remove it from the cache, and there is more than 100 items in the cache
            if(cache.Count > 100)
            {
                foreach(KeyValuePair<string, Tuple<string, DateTime>> result in cache)
                {
                    if(result.Value.Item2.AddMinutes(20) > DateTime.UtcNow)
                    {
                        cache.Remove(result.Key);
                    }
                }
            }

            return JsonConvert.SerializeObject(resultList);
        }
    }
}
