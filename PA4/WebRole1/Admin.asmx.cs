using ClassLibrary1;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for Admin
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class Admin : System.Web.Services.WebService
    {

        [WebMethod]
        public void BeginCrawling()
        {
            if(GetStatus() == "Idle")
            {
                CloudQueueMessage startMessage = new CloudQueueMessage("start:https://www.cnn.com/robots.txt");

                StorageManager.getCommandQueue().AddMessage(startMessage);

                CloudQueueMessage startMessage2 = new CloudQueueMessage("start:https://www.bleacherreport.com/robots.txt");

                StorageManager.getCommandQueue().AddMessage(startMessage2);
            }   
        }

        [WebMethod]
        public void StopCrawling()
        {
            CloudQueueMessage stopMessage = new CloudQueueMessage("stop");

            StorageManager.getCommandQueue().AddMessage(stopMessage);
        }

        [WebMethod]
        public string[] ReadLinksFromTableStorage()
        {
            TableQuery<WebPage> rangeQuery = new TableQuery<WebPage>()
                .Take(10);

            List<string> answers = new List<string>();

            foreach (WebPage entity in StorageManager.getTable().ExecuteQuery(rangeQuery))
            {
                answers.Add(entity.Url);
            }

            return answers.ToArray();
        }

        [WebMethod]
        public string[] GetErrors()
        {
            List<string> results = new List<string>();

            TableQuery<ExceptionUrl> query = new TableQuery<ExceptionUrl>()
                .Take(10);

            foreach (ExceptionUrl item in StorageManager.getExceptionTable().ExecuteQuery(query))
            {
                results.Add(item.Url + "/////// Error: " + item.Message);
            }

            return results.ToArray();
        }

        [WebMethod]
        public string[] GetTitleLinkDate(string url)
        {
            List<string> result = new List<string>();

            var keyBytes = System.Text.Encoding.UTF8.GetBytes(url);
            var base64 = System.Convert.ToBase64String(keyBytes);
            base64 = base64.Replace('/', '_');

            TableQuery<WebPage> query = new TableQuery<WebPage>()
                .Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, base64))
                .Take(1);

            foreach (WebPage entity in StorageManager.getTable().ExecuteQuery(query))
            {
                result.Add(entity.Title);
                result.Add(entity.Url);
                result.Add(entity.Date);
            }

            return result.ToArray();
        }

        [WebMethod]
        public string GetCPU()
        {
            var result = "";
            TableQuery<Performance> query = new TableQuery<Performance>()
                .Take(1);

            foreach (Performance cpu in StorageManager.getPerformanceTable().ExecuteQuery(query))
            {
                result = cpu.CPU;
            }

            return result.ToString();
        }

        [WebMethod]
        public string GetRAM()
        {
            var result = "";
            TableQuery<Performance> query = new TableQuery<Performance>()
                .Take(1);

            foreach (Performance ram in StorageManager.getPerformanceTable().ExecuteQuery(query))
            {
                result = ram.RAM;
            }

            return result.ToString();
        }

        [WebMethod]
        public string GetStatus()
        {
            var result = "";
            TableQuery<Performance> query = new TableQuery<Performance>()
                .Take(1);

            foreach (Performance status in StorageManager.getPerformanceTable().ExecuteQuery(query))
            {
                result = status.Status;
            }

            return result;
        }

        [WebMethod]
        public string GetSizeQueue()
        {
            var result = "";
            TableQuery<Performance> query = new TableQuery<Performance>()
                .Take(1);

            foreach (Performance queue in StorageManager.getPerformanceTable().ExecuteQuery(query))
            {
                result = queue.SizeQueue.ToString();
            }

            return result;
        }

        [WebMethod]
        public string GetSizeIndex()
        {
            var result = "";
            TableQuery<Performance> query = new TableQuery<Performance>()
                .Take(1);

            foreach (Performance index in StorageManager.getPerformanceTable().ExecuteQuery(query))
            {
                result = index.SizeIndex.ToString();
            }

            return result;
        }

        [WebMethod]
        public string GetNumCrawled()
        {
            var result = "";
            TableQuery<Performance> query = new TableQuery<Performance>()
                .Take(1);

            foreach (Performance crawl in StorageManager.getPerformanceTable().ExecuteQuery(query))
            {
                result = crawl.NumCrawled.ToString();
            }

            return result;
        }

        [WebMethod]
        public string GetNumTitles()
        {
            var result = "";
            TableQuery<TrieData> query = new TableQuery<TrieData>()
                .Take(1);

            foreach(TrieData data in StorageManager.getTrieTable().ExecuteQuery(query))
            {
                result = data.NumTitles.ToString();
            }

            return result;
        }

        [WebMethod]
        public string GetLastTitle()
        {
            var result = "";
            TableQuery<TrieData> query = new TableQuery<TrieData>()
                .Take(1);

            foreach (TrieData data in StorageManager.getTrieTable().ExecuteQuery(query))
            {
                result = data.Title.ToString();
            }

            return result;
        }
    }
}
