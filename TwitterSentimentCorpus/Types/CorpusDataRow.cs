using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitterSentimentCorpus.Types
{
    public class CorpusDataRow
    {
        public long Id { get; set; }
        public string Keyword { get; set; }
        public Tweet Tweet { get; set; }
        public Sentiment Sentiment { private get; set; }
        public int SentimentValue { get { return (int)Sentiment; } }
    }
}
