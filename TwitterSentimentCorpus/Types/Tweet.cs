using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitterSentimentCorpus.Types
{
    public class Tweet
    {
        public DateTime CreatedDate { get; set; }
        public string Language { get; set; }
        public string ScreenName { get; set; }
        public string Text { get; set; }
    }
}
