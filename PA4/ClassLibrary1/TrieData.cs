using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary1
{
    public class TrieData : TableEntity
    {
        public string Title { get; set; }
        public int NumTitles { get; set; }

        public TrieData() { }

        public TrieData(string title, int numTitles)
        {
            this.PartitionKey = title;
            this.RowKey = String.Format("{0:D19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks) + "_" + Guid.NewGuid().ToString();

            this.Title = title;
            this.NumTitles = numTitles;
        }
    }
}
